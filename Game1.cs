using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Runner_Game;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // The Player
    private Runner _runner;

    // Text for the game
    private SpriteFont enjoy;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        _runner = new();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // TODO: use this.Content to load your game content here\
        _runner.LoadContent(Content);

    }

    protected override void Update(GameTime gameTime)
    {
        _runner.Update(gameTime);

        // Loads the font from the content folder
        enjoy = Content.Load<SpriteFont>("EnjoyFont"); 

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // TODO: Add your drawing code here
        _spriteBatch.Begin();
        _runner.Draw(gameTime, _spriteBatch);

        // Draw instructions and Game Timer 
        _spriteBatch.DrawString(enjoy, "Use A or D / Left or Right to dodge!", new Vector2(25, 25), Color.Yellow);

        _spriteBatch.DrawString(enjoy, $"{gameTime.TotalGameTime:c}", new Vector2(50, 75), Color.Yellow);

        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
