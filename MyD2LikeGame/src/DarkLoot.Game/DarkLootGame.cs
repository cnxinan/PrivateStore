using System;
using System.Diagnostics;
using System.IO;
using DarkLoot.Data;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using DarkLoot.Gameplay.Persistence;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGame = Microsoft.Xna.Framework.Game;

namespace DarkLoot.Game;

public sealed class DarkLootGame : MonoGameGame
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameInput _input = new();
    private readonly Camera2D _camera = new();
    private readonly ArenaRenderer _arenaRenderer = new();
    private readonly ActorPresentation _presentation = new();
    private readonly HudRenderer _hud = new();
    private readonly InventoryPanel _inventory = new();
    private readonly BlacksmithPanel _blacksmith = new();
    private readonly PanelController _panels = new();
    private readonly FrameRateCounter _frameRate = new();
    private readonly WindowActivityGate _activityGate = new();
    private readonly Stopwatch _renderClock = Stopwatch.StartNew();
    private readonly RuntimeSmokeTest? _smoke;
    private readonly string _savePath;
    private readonly PersistenceSmokeTest? _restoringSmoke;
    private readonly bool _persistenceOnlySmoke;
    private SaveSession? _save;
    private SpriteBatch _batch = null!;
    private ShapeRenderer _shapes = null!;
    private GothicUiRenderer _ui = null!;
    private GothicRoomRenderer _room = null!;
    private System.Numerics.Vector2 _pointer;
    private SpriteFont _font = null!;
    private GameContent _content = null!;
    private GameContent _productionContent = null!;
    private ArenaSimulation _arena = null!;
    private string? _interactionFeedback;

    internal DarkLootGame(bool smokeTest, string savePath, PersistenceSmokeTest? restoringSmoke = null,
        bool persistenceOnlySmoke = false)
    {
        _savePath = savePath;
        _restoringSmoke = restoringSmoke;
        _persistenceOnlySmoke = persistenceOnlySmoke;
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1120,
            PreferredBackBufferHeight = 720
        };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        Window.Title = GameText.Title;
        IsMouseVisible = true;
        if (smokeTest)
        {
            _smoke = new RuntimeSmokeTest(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"),
                appendLog: restoringSmoke is not null);
        }
    }

    protected override void Initialize()
    {
        _content = GameContentLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Definitions"));
        _productionContent = _content;
        var encounter = _content.Encounters["encounter.training_arena"];
        if (_smoke is not null)
        {
            (_content, encounter) = _smoke.CreateFixture(_content);
        }
        _arena = new ArenaSimulation(
            _content,
            _smoke?.CreateRandom() ?? new Random(42),
            encounter);
        _save = new SaveSession(_content, _savePath);
        _save.Load(_arena);
        _presentation.Reset(_arena);
        if (_restoringSmoke is not null)
            _inventory.SetOpen(true);
        FollowPlayer();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _shapes = new ShapeRenderer(GraphicsDevice);
        _ui = new GothicUiRenderer(GraphicsDevice, _shapes);
        _room = new GothicRoomRenderer(GraphicsDevice, _shapes);
        _font = Content.Load<SpriteFont>("Fonts/UIFont");
        _smoke?.ValidatePresentation(_font, _productionContent);
        if (_smoke is not null)
        {
            _smoke.ValidatePresentation(_font, _content);
            if (_restoringSmoke is null)
                PaperDollVisualSmoke.Run(GraphicsDevice, _shapes, _font, _productionContent, AppContext.BaseDirectory);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        // Poll while unfocused too, so returning to the window cannot replay stale clicks.
        var captured = _input.Capture(_camera);
        if (!_activityGate.CanAdvance(IsActive))
        {
            base.Update(gameTime);
            return;
        }
        if (_restoringSmoke is not null)
        {
            base.Update(gameTime);
            return;
        }
        var input = _smoke?.NextInput(
            _arena,
            _inventory,
            _blacksmith,
            _content,
            GraphicsDevice.Viewport.Width,
            _presentation.SwingRemaining) ?? captured;
        ApplyInput(input, _smoke is null ? Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, .1f) : .1f);
        _save!.SaveIfChanged(_arena);
        _smoke?.AfterUpdate(
            _arena,
            _inventory,
            _blacksmith,
            _content,
            _presentation.PlayerPose,
            _presentation.GetMonsterPose,
            _presentation.SwingRemaining,
            _presentation.HitTexts);
        base.Update(gameTime);
    }

    private void ApplyInput(GameInputSnapshot input, float elapsed)
    {
        _pointer = input.PointerScreenPosition;
        var frame = _panels.Route(input, _arena.Player, _arena.BlacksmithOperations,
            GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _inventory.SetOpen(frame.InventoryOpen);
        _blacksmith.SetOpen(frame.BlacksmithOpen);
        if (frame.Feedback is not null)
        {
            _interactionFeedback = GameText.SmithStatus(frame.Feedback.Value);
        }
        else if (_arena.Player.IsDead || _arena.BlacksmithOperations.CanInteract(_arena.Player))
        {
            _interactionFeedback = null;
        }
        if (frame.ExitRequested)
        {
            Exit();
            return;
        }
        if (frame.InventoryOpen)
            _inventory.Update(frame.HandlePanelActions ? input : input with { AttackPressed = false, ScrollDelta = 0 },
                _arena.Player, _content, GraphicsDevice.Viewport.Width, _arena.EquipmentOperations,
                _arena.Blacksmith.KnownRecipeIds);
        if (frame.HandlePanelActions)
        {
            if (frame.BlacksmithOpen)
                _blacksmith.Update(input, _arena, _content, GraphicsDevice.Viewport.Width);
        }
        if (frame.AdvanceWorld)
        {
            if (frame.RestartRequested)
            {
                _arena.ResetEncounter();
                _presentation.Reset(_arena);
                _smoke?.AfterReset(_arena, _presentation);
            }
            var intent = new PlayerIntent(frame.BlockWorldInput ? System.Numerics.Vector2.Zero : input.MoveDirection,
                input.AimWorldPosition,
                !frame.BlockWorldInput && (input.AttackHeld || input.AttackPressed),
                !frame.BlockWorldInput && input.PickupPressed);
            _arena.Update(intent, elapsed);
            _presentation.Update(_arena, elapsed);
        }
        _room.Update(elapsed, frame.AdvanceWorld);
        FollowPlayer();
    }

    private void FollowPlayer()
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = new UiLayout(width, GraphicsDevice.Viewport.Height).Console.Top;
        _camera.Follow(GothicRoomLayout.CameraFocus(_arena.Player.Position, width, height), width, height);
    }

    protected override void Draw(GameTime gameTime)
    {
        _frameRate.RecordDraw(_renderClock.Elapsed);
        GraphicsDevice.Clear(new Color(10, 15, 20));
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _arenaRenderer.Draw(
            _batch,
            _shapes,
            _font,
            _arena,
            _content,
            _camera,
            _presentation,
            _room);
        _hud.Draw(_batch, _shapes, _font, _arena, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height,
            _ui, _frameRate.FramesPerSecond, _inventory.IsOpen, _blacksmith.IsOpen, _pointer, _interactionFeedback);
        _inventory.Draw(_batch, _shapes, _font, _arena.Player, _content, GraphicsDevice.Viewport.Width, _ui);
        _blacksmith.Draw(_batch, _shapes, _font, _arena, _content, GraphicsDevice.Viewport.Width, _ui);
        _inventory.DrawTooltip(_batch, _shapes, _font, GraphicsDevice.Viewport.Width);
        _batch.End();
        base.Draw(gameTime);
        _smoke?.ValidateRoomDraw(GraphicsDevice, _camera, _room, _inventory.IsOpen || _blacksmith.IsOpen, _arena);
        _smoke?.ValidateUiDraw(GraphicsDevice, _font, _arena, _inventory, _blacksmith);
        _smoke?.ValidatePlayerDraw(GraphicsDevice, _camera, _arena, _presentation.PlayerPose,
            _restoringSmoke is not null);
        _smoke?.ValidatePanelPresentation(_font, _arena, _inventory, _blacksmith, _content);
        if (_restoringSmoke is not null)
        {
            if (_restoringSmoke.AfterRestoredDraw(_arena, _content))
                Exit();
        }
        else if (_persistenceOnlySmoke && _smoke?.ReadyForPersistenceCheck == true)
        {
            _smoke.ValidateUiAcceptance();
            Exit();
        }
        else if (_smoke?.AfterDraw(_arena, _presentation, _camera, GraphicsDevice, _frameRate.FramesPerSecond) == true)
        {
            Exit();
        }
    }

    internal void RecordSmokeProgress(PersistenceSmokeTest smoke) => smoke.RecordExpected(_arena, _content);

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _save?.SaveOnExit(_arena);
        base.OnExiting(sender, args);
    }

    protected override void UnloadContent()
    {
        _room?.Dispose();
        _ui?.Dispose();
        _shapes?.Dispose();
        _batch?.Dispose();
        base.UnloadContent();
    }
}
