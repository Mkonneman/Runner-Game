using Microsoft.Xna.Framework;
using Runner_Game.Screens;
using Runner_Game.StateManagement;


namespace Runner_Game;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;

    // Runs whichever screen is on top (title, gameplay, ...)
    private ScreenManager _screenManager;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

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
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        // The ScreenManager draws the screens
        base.Draw(gameTime);
    }
}
