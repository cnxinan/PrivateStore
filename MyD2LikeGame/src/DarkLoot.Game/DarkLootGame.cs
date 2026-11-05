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
    private readonly QuestPanel _journal = new();
    private readonly PanelController _panels = new();
    private readonly FrameRateCounter _frameRate = new();
    private readonly WindowActivityGate _activityGate = new();
    private readonly Stopwatch _renderClock = Stopwatch.StartNew();
    private readonly RuntimeSmokeTest? _smoke;
    private readonly string _savePath;
    private readonly PersistenceSmokeTest? _restoringSmoke;
    private readonly bool _persistenceOnlySmoke;
    private readonly DungeonWindowSmoke? _dungeonSmoke;
    private readonly QuestWindowSmoke? _questSmoke;
    private SaveSession? _save;
    private SpriteBatch _batch = null!;
    private ShapeRenderer _shapes = null!;
    private GothicUiRenderer _ui = null!;
    private GothicRoomRenderer _room = null!;
    private DungeonRoomRenderer? _dungeonRoom;
    private DungeonSession? _adventure;
    private System.Numerics.Vector2 _pointer;
    private SpriteFont _font = null!;
    private GameContent _content = null!;
    private GameContent _productionContent = null!;
    private PixelContent _pixels = null!;
    private ArenaSimulation _arena = null!;
    private string? _interactionFeedback;

    internal DarkLootGame(bool smokeTest, string savePath, PersistenceSmokeTest? restoringSmoke = null,
        bool persistenceOnlySmoke = false, DungeonWindowSmoke? dungeonSmoke = null, QuestWindowSmoke? questSmoke = null)
    {
        _savePath = savePath;
        _restoringSmoke = restoringSmoke;
        _persistenceOnlySmoke = persistenceOnlySmoke;
        _dungeonSmoke = dungeonSmoke;
        _questSmoke = questSmoke;
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
        if (_dungeonSmoke is not null)
            _content = _dungeonSmoke.CreateContent(_content);
        if (_questSmoke is not null)
            _content = _questSmoke.CreateContent(_content);
        var encounter = _content.Encounters["encounter.training_arena"];
        if (_smoke is not null)
        {
            (_content, encounter) = _smoke.CreateFixture(_content);
        }
        if (_smoke is null)
        {
            _adventure = new DungeonSession(_content, new Random(42));
            _arena = _adventure.Arena;
        }
        else
            _arena = new ArenaSimulation(_content, _smoke.CreateRandom(), encounter);
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
        _pixels = new PixelContent(Content);
        _arenaRenderer.LoadEffects(GraphicsDevice, _pixels);
        _ui = new GothicUiRenderer(GraphicsDevice, _shapes);
        _room = new GothicRoomRenderer(_pixels, _shapes);
        if (_adventure is not null)
            _dungeonRoom = new DungeonRoomRenderer(_pixels, _shapes);
        _font = Content.Load<SpriteFont>("Fonts/UIFont");
        _smoke?.ValidatePresentation(_font, _productionContent);
        _dungeonSmoke?.ValidateFont(_font, _productionContent);
        _questSmoke?.ValidateFont(_font);
        if (_dungeonSmoke?.Restoring == false)
            NewEnemyVisualSmoke.Run(GraphicsDevice, _shapes, _font, _pixels);
        if (_smoke is not null)
        {
            _smoke.ValidatePresentation(_font, _content);
            if (_restoringSmoke is null)
            {
                PaperDollVisualSmoke.Run(GraphicsDevice, _shapes, _font, _productionContent, AppContext.BaseDirectory, _pixels);
                AttackEffectVisualSmoke.Run(GraphicsDevice, _shapes, _font, _arenaRenderer.Effects, _pixels);
                PixelActorVisualSmoke.Run(GraphicsDevice, _shapes, _pixels, _font);
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        // Poll while unfocused too, so returning to the window cannot replay stale clicks.
        var captured = _input.Capture(_camera);
        if (!_activityGate.CanAdvance(IsActive || _dungeonSmoke is not null || _questSmoke is not null))
        {
            base.Update(gameTime);
            return;
        }
        if (_restoringSmoke is not null || _dungeonSmoke?.Restoring == true ||
            _questSmoke?.Restoring == true || _smoke?.WaitingForCorpseDraw == true)
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
            _presentation.SwingRemaining) ??
            _dungeonSmoke?.NextInput(_adventure!, _inventory, _blacksmith, _content,
                _presentation.PlayerPose, _dungeonRoom!.AnimationTime) ??
            _questSmoke?.NextInput(_adventure!, _panels, _blacksmith, _content,
                _presentation.PlayerPose, _adventure!.InDungeon ? _dungeonRoom!.AnimationTime : _room.AnimationTime) ?? captured;
        ApplyInput(input, _smoke is null && _dungeonSmoke is null && _questSmoke is null ?
            Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, .1f) : .1f);
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
            if (frame.JournalOpen)
                _journal.Update(input, GraphicsDevice.Viewport.Width);
        }
        if (frame.AdvanceWorld)
        {
            if (frame.RestartRequested)
            {
                if (_adventure is null)
                    _arena.ResetEncounter();
                else
                    _adventure.Restart();
                _presentation.Reset(_arena);
                _smoke?.AfterReset(_arena, _presentation);
            }
            var intent = new PlayerIntent(frame.BlockWorldInput ? System.Numerics.Vector2.Zero : input.MoveDirection,
                input.AimWorldPosition,
                !frame.BlockWorldInput && (input.AttackHeld || input.AttackPressed),
                !frame.BlockWorldInput && input.PickupPressed);
            if (_adventure is null)
                _arena.Update(intent, elapsed);
            else
                _adventure.Update(intent, elapsed);
            if (_adventure is not null && (!ReferenceEquals(_arena, _adventure.Arena) || _adventure.TryTravel()))
            {
                _arena = _adventure.Arena;
                _presentation.Reset(_arena);
                _interactionFeedback = null;
            }
            else
                _presentation.Update(_arena, elapsed);
        }
        _room.Update(elapsed, frame.AdvanceWorld);
        _dungeonRoom?.Update(elapsed, frame.AdvanceWorld);
        FollowPlayer();
    }

    private void FollowPlayer()
    {
        var width = GraphicsDevice.Viewport.Width;
        var height = new UiLayout(width, GraphicsDevice.Viewport.Height).Console.Top;
        var focus = GothicRoomLayout.CameraFocus(_arena.Player.Position, width, height);
        if (_adventure?.InDungeon == true)
            focus.X = Math.Clamp(_arena.Player.Position.X, -80 + width / 2f, 3280 - width / 2f);
        _camera.Follow(focus, width, height);
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
            _room,
            _dungeonRoom,
            _adventure);
        _hud.Draw(_batch, _shapes, _font, _arena, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height,
            _ui, _frameRate.FramesPerSecond, _inventory.IsOpen, _blacksmith.IsOpen, _pointer, _interactionFeedback,
            _adventure is null ? null : !_adventure.InDungeon ? GameText.HubTitle :
                _arena.Player.Position.X < 1120 ? GameText.CryptEntry :
                _arena.Player.Position.X < 2240 ? GameText.CryptBattle : GameText.CryptBoss,
            _adventure is null ? null : !_adventure.InDungeon ? GameText.HubObjective :
                _adventure.Completed ? GameText.DungeonComplete :
                _arena.Map!.BossGateOpen ? GameText.DungeonGateOpen : GameText.DungeonObjective,
            _panels.JournalOpen);
        _inventory.Draw(_batch, _shapes, _font, _arena.Player, _content, GraphicsDevice.Viewport.Width, _ui);
        _blacksmith.Draw(_batch, _shapes, _font, _arena, _content, GraphicsDevice.Viewport.Width, _ui);
        if (_panels.JournalOpen)
            _journal.Draw(_batch, _font, _arena, _content, GraphicsDevice.Viewport.Width, _ui);
        _inventory.DrawTooltip(_batch, _shapes, _font, GraphicsDevice.Viewport.Width);
        _batch.End();
        base.Draw(gameTime);
        _smoke?.ValidateRoomDraw(GraphicsDevice, _camera, _room, _inventory.IsOpen || _blacksmith.IsOpen, _arena);
        _smoke?.ValidateUiDraw(GraphicsDevice, _font, _arena, _inventory, _blacksmith);
        _smoke?.ValidatePlayerDraw(GraphicsDevice, _camera, _arena, _presentation.PlayerPose,
            _restoringSmoke is not null);
        _smoke?.ValidateAttackEffectDraw(GraphicsDevice, _camera, _presentation,
            _inventory.IsOpen || _blacksmith.IsOpen);
        _smoke?.ValidatePanelPresentation(_font, _arena, _inventory, _blacksmith, _content);
        if (_dungeonSmoke is not null && _adventure!.InDungeon)
            _dungeonSmoke.ValidateEnemyDraw(GraphicsDevice, _camera, _arena);
        if (_dungeonSmoke?.AfterDraw(GraphicsDevice, _camera, _adventure!, _inventory.IsOpen) == true)
            Exit();
        if (_questSmoke?.AfterDraw(GraphicsDevice, _camera, _adventure!, _panels, _blacksmith) == true)
            Exit();
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
    internal void RecordDungeonProgress(DungeonWindowSmoke smoke) => smoke.RecordExpected(_arena, _content);
    internal void RecordQuestProgress(QuestWindowSmoke smoke) => smoke.RecordExpected(_arena, _content);

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        _save?.SaveOnExit(_arena);
        base.OnExiting(sender, args);
    }

    protected override void UnloadContent()
    {
        _arenaRenderer.UnloadEffects();
        _dungeonRoom?.Dispose();
        _room?.Dispose();
        _ui?.Dispose();
        _shapes?.Dispose();
        _batch?.Dispose();
        base.UnloadContent();
    }
}
