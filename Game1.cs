using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Runner_Game.Screens;
using Runner_Game.StateManagement;


namespace Runner_Game;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    // Runs whichever screen is on top (title, gameplay, ...)
    private ScreenManager _screenManager;

    // The whole game is laid out for 854x480, so it draws to an 854x480 image and gets scaled up to fill the window (1080p!)
    public const int VirtualWidth = 854;
    public const int VirtualHeight = 480;
    private RenderTarget2D _scene;

    // Where the scaled game sits in the window (854x480 is 16:9 so no black bars at 1080p)
    public static Rectangle Letterbox { get; private set; } = new Rectangle(0, 0, VirtualWidth, VirtualHeight);

    private KeyboardState _lastKeyboard;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Starts fullscreen at 1080p (borderless so alt tab is quick), F11 swaps to a window
        _graphics.PreferredBackBufferWidth = 1920;
        _graphics.PreferredBackBufferHeight = 1080;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += (s, e) =>
        {
            if (_graphics.IsFullScreen || Window.ClientBounds.Width == 0) return;
            _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            _graphics.ApplyChanges();
        };

        // Make the screen manager and register it so it updates and draws itself
        _screenManager = new ScreenManager(this);
        Components.Add(_screenManager);
        Services.AddService(typeof(ScreenManager), _screenManager);
    }

    protected override void Initialize()
    {
        // The game starts on the title screen
        _screenManager.AddScreen(new TitleScreen());

        base.Initialize();
        Shop.Check();
        Shop.Load(0, null, null);  // starter gear, in case there is no save yet
        SaveSystem.Load();  // Gems, gear and settings from last time
        _scene = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight);
    }

    protected override void Update(GameTime gameTime)
    {
        // F11 flips between fullscreen and a 1280x720 window
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.F11) && _lastKeyboard.IsKeyUp(Keys.F11))
        {
            _graphics.IsFullScreen = !_graphics.IsFullScreen;
            _graphics.PreferredBackBufferWidth = _graphics.IsFullScreen ? 1920 : 1280;
            _graphics.PreferredBackBufferHeight = _graphics.IsFullScreen ? 1080 : 720;
            _graphics.ApplyChanges();
        }
        _lastKeyboard = keyboard;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        // The ScreenManager draws the screens into the 854x480 image
        GraphicsDevice.SetRenderTarget(_scene);
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);

        // Then that image gets stretched as big as it fits in the window, keeping its shape
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        var window = GraphicsDevice.PresentationParameters.Bounds;
        float scale = MathHelper.Min((float)window.Width / VirtualWidth, (float)window.Height / VirtualHeight);
        int width = (int)(VirtualWidth * scale), height = (int)(VirtualHeight * scale);
        Letterbox = new Rectangle((window.Width - width) / 2, (window.Height - height) / 2, width, height);
        _screenManager.SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _screenManager.SpriteBatch.Draw(_scene, Letterbox, Color.White);
        _screenManager.SpriteBatch.End();

        // Screens read the viewport size during Update too, so put it back to 854x480 for them (Sneaky but it works)
        GraphicsDevice.Viewport = new Viewport(0, 0, VirtualWidth, VirtualHeight);
    }
}
