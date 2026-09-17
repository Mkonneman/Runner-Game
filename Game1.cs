using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System;


namespace Runner_Game;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    // The Player
    private Runner _runner;

    // Text for the game
    private SpriteFont enjoy;

    // Makes multiple bolders
    private List<Bolders> _bolders = new();

    // Array that moves the bolder (Same as the player but simplified)
    private readonly float[] _laneX = { 100f, 200f, 300f };
    private Random _random = new();
    private float _spawnTimer;
    private const float SpawnInterval = 1.5f; // seconds between spawns (Tunned)

    // Bool for ending the game
    private bool _isGameOver;

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

        // Loads the font from the content folder
        enjoy = Content.Load<SpriteFont>("EnjoyFont");

        // TODO: use this.Content to load your game content here\
        _runner.LoadContent(Content);

    }

    protected override void Update(GameTime gameTime)
    {
        // Ends the game if true
        if (_isGameOver)
        {
            base.Update(gameTime);
            return;
        }

        _runner.Update(gameTime);

        // Spawn a new bolder in a random lane every SpawnInterval seconds.
        _spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_spawnTimer >= SpawnInterval)
        {
            _spawnTimer = 0f;
            float laneX = _laneX[_random.Next(_laneX.Length)];
            var bolder = new Bolders(laneX);
            bolder.LoadContent(Content); // safe to call per-instance - MonoGame caches the texture after the first load
            _bolders.Add(bolder);
        }

        // Move every bolder, then remove ones that leave the screen or hit the player.
        for (int i = _bolders.Count - 1; i >= 0; i--)
        {
            _bolders[i].Update(gameTime, GraphicsDevice.Viewport.Height);

            if (_bolders[i].Bounds.CollidesWith(_runner.Bounds))
            {
                _bolders.RemoveAt(i); // despawn the one that hit
                _isGameOver = true;   // and end the game
                continue;
            }

            if (_bolders[i].IsOffScreen)
            {
                _bolders.RemoveAt(i);
            }
        }

        

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
