using System;
using System.Diagnostics;
using System.IO;
using DarkLoot.Data;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Diagnostics;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
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
    private SpriteBatch _batch = null!;
    private ShapeRenderer _shapes = null!;
    private SpriteFont _font = null!;
    private GameContent _content = null!;
    private GameContent _productionContent = null!;
    private ArenaSimulation _arena = null!;
    private string? _interactionFeedback;

    public DarkLootGame(bool smokeTest)
    {
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
            _smoke = new RuntimeSmokeTest(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"));
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
        _presentation.Reset(_arena);
        FollowPlayer();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _shapes = new ShapeRenderer(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/UIFont");
        _smoke?.ValidatePresentation(_font, _productionContent);
        if (_smoke is not null)
        {
            _smoke.ValidatePresentation(_font, _content);
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
        var input = _smoke?.NextInput(
            _arena,
            _inventory,
            _content,
            GraphicsDevice.Viewport.Width,
            _presentation.SwingRemaining) ?? captured;
        ApplyInput(input, _smoke is null ? Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, .1f) : .1f);
        _smoke?.AfterUpdate(
            _arena,
            _inventory,
            _content,
            _presentation.PlayerPose,
            _presentation.GetMonsterPose,
            _presentation.SwingRemaining,
            _presentation.HitTexts);
        base.Update(gameTime);
    }

    private void ApplyInput(GameInputSnapshot input, float elapsed)
    {
        var frame = _panels.Route(input, _arena.Player, _arena.BlacksmithOperations);
        _inventory.SetOpen(frame.ActivePanel == PanelKind.Inventory);
        _blacksmith.SetOpen(frame.ActivePanel == PanelKind.Blacksmith);
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
        if (frame.HandlePanelActions)
        {
            if (frame.ActivePanel == PanelKind.Inventory)
                _inventory.Update(input, _arena.Player, _content, GraphicsDevice.Viewport.Width, _arena.EquipmentOperations);
            else if (frame.ActivePanel == PanelKind.Blacksmith)
                _blacksmith.Update(input, _arena, _content, GraphicsDevice.Viewport.Width);
        }
        if (frame.AdvanceWorld)
        {
            if (frame.RestartRequested)
            {
                _arena.ResetEncounter();
                _presentation.Reset(_arena);
            }
            var intent = new PlayerIntent(input.MoveDirection, input.AimWorldPosition,
                input.AttackHeld || input.AttackPressed, input.PickupPressed);
            _arena.Update(intent, elapsed);
            _presentation.Update(_arena, elapsed);
        }
        FollowPlayer();
    }

    private void FollowPlayer() =>
        _camera.Follow(_arena.Player.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

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
            _presentation);
        _hud.Draw(_batch, _shapes, _font, _arena, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height,
            _frameRate.FramesPerSecond, _interactionFeedback);
        _inventory.Draw(_batch, _shapes, _font, _arena.Player, _content, GraphicsDevice.Viewport.Width);
        _blacksmith.Draw(_batch, _shapes, _font, _arena, _content, GraphicsDevice.Viewport.Width);
        _batch.End();
        base.Draw(gameTime);
        if (_smoke?.AfterDraw(_arena, _presentation, _camera, GraphicsDevice, _frameRate.FramesPerSecond) == true)
        {
            Exit();
        }
    }

    protected override void UnloadContent()
    {
        _shapes?.Dispose();
        _batch?.Dispose();
        base.UnloadContent();
    }
}
