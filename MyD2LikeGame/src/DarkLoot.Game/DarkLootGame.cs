using System;
using System.IO;
using DarkLoot.Data;
using DarkLoot.Engine.Camera;
using DarkLoot.Engine.Input;
using DarkLoot.Engine.Rendering;
using DarkLoot.Gameplay.Combat;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NumericsVector = System.Numerics.Vector2;
using MonoGameGame = Microsoft.Xna.Framework.Game;

namespace DarkLoot.Game;

public sealed class DarkLootGame : MonoGameGame
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameInput _input = new();
    private readonly Camera2D _camera = new();
    private readonly ArenaRenderer _arenaRenderer = new();
    private readonly HudRenderer _hud = new();
    private readonly InventoryPanel _inventory = new();
    private readonly RuntimeSmokeTest? _smoke;
    private SpriteBatch _batch = null!;
    private ShapeRenderer _shapes = null!;
    private SpriteFont _font = null!;
    private GameContent _content = null!;
    private ArenaSimulation _arena = null!;
    private NumericsVector _aim;
    private float _attackFlash;
    private bool _hit;
    private bool _wasActive;

    public DarkLootGame(bool smokeTest)
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1120,
            PreferredBackBufferHeight = 720
        };
        Content.RootDirectory = Path.Combine(AppContext.BaseDirectory, "Content");
        Window.Title = "DarkLoot - Training Arena";
        IsMouseVisible = true;
        if (smokeTest)
        {
            _smoke = new RuntimeSmokeTest(Path.Combine(AppContext.BaseDirectory, "smoke-test.log"));
        }
    }

    protected override void Initialize()
    {
        _content = GameContentLoader.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "Definitions"));
        _arena = new ArenaSimulation(_content, new Random(42));
        FollowPlayer();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _shapes = new ShapeRenderer(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Fonts/UIFont");
    }

    protected override void Update(GameTime gameTime)
    {
        // Poll while unfocused too, so returning to the window cannot replay stale clicks.
        var captured = _input.Capture(_camera);
        if (_smoke is null && (!IsActive || !_wasActive))
        {
            _wasActive = IsActive;
            base.Update(gameTime);
            return;
        }
        _wasActive = IsActive;
        var input = _smoke?.NextInput(_arena, _inventory, GraphicsDevice.Viewport.Width) ?? captured;
        if (input.ExitPressed)
        {
            Exit();
            return;
        }
        ApplyInput(input, _smoke is null ? Math.Min((float)gameTime.ElapsedGameTime.TotalSeconds, .1f) : .1f);
        _smoke?.AfterUpdate(_arena, _inventory);
        base.Update(gameTime);
    }

    private void ApplyInput(GameInputSnapshot input, float elapsed)
    {
        _aim = input.AimWorldPosition;
        _inventory.Update(input, _arena.Player.Inventory, _content, GraphicsDevice.Viewport.Width);
        if (input.RestartPressed)
        {
            _arena.ResetEncounter();
        }
        var attacking = input.AttackPressed && !_inventory.IsOpen;
        var previousMonsterHealth = _arena.Monster.Health;
        if (!_inventory.IsOpen)
        {
            var intent = new PlayerIntent(input.MoveDirection, input.AimWorldPosition, attacking, input.PickupPressed);
            _arena.Update(intent, elapsed);
        }
        _attackFlash = Math.Max(0, _attackFlash - elapsed);
        if (attacking)
        {
            _attackFlash = .18f;
            _hit = _arena.Monster.Health < previousMonsterHealth;
        }
        FollowPlayer();
    }

    private void FollowPlayer() =>
        _camera.Follow(_arena.Player.Position, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(10, 15, 20));
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _arenaRenderer.Draw(_batch, _shapes, _font, _arena, _camera, _aim, _attackFlash, _hit);
        _hud.Draw(_batch, _shapes, _font, _arena, GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        _inventory.Draw(_batch, _shapes, _font, _arena.Player.Inventory, _content, GraphicsDevice.Viewport.Width);
        _batch.End();
        base.Draw(gameTime);
        if (_smoke?.AfterDraw() == true)
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
