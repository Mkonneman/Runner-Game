using CollisionExercise.Collisions;
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// The actual game (pushed on top of the title screen)
    /// </summary>
    public class GameplayScreen : GameScreen
    {
        private ContentManager _content;

        // The Player
        private Runner _runner;


        // Audio Handling for the game
        private Song _runnerMusic;
        private SoundEffect _crashSound;

        // Makes multiple bolders
        private List<Bolders> _bolders = new();

        // Rocks drop anywhere across the screen between these X positions
        private const float RockMinX = 80f;
        private const float RockMaxX = 720f;
        private float _spawnTimer;

        // Crystal spikes stuck in the floor that slide in from the right (jump over them)
        private Texture2D _spikeTexture;
        private Texture2D _wallTexture;
        private Texture2D _crystalWallTexture;           // same size as the stone one, so the hit box fits both
        private Texture2D _hammerTexture;

        // Dash cooldown icon (top right)
        private Texture2D _dashIcon;
        private Texture2D _smashIcon;

        // How visible the smash range ring is (0 turns it off), set in the settings menu
        private static float SmashRingOpacity => Settings.SmashRingOpacity;
        private const float IconScale = 0.75f;            // icons are 64px art, drawn at 48px

        // F1 shows the hit boxes (for checking unfair hits)
        private bool _showHitboxes;

        // The lantern he carries swings as he runs and drops embers (Makes him way easier to see)
        private Texture2D _lanternTexture;
        private Texture2D _glowTexture;
        private SparkParticleSystem _embers;
        private float _lanternTime;
        private Texture2D _pixel;
        private readonly List<(float X, bool IsWall, bool Crystal)> _spikes = new();  // X of each obstacle, whether it's a breakable wall or a crystal spike, and which wall look
        private float _spikeTimer = 3f;                   // seconds until the next one
        private const float SpikeSpeed = 260f;            // same as the floor so they look stuck in it
        private const float SpikeBottom = 471f;           // floor line where his feet touch
        private static readonly Rectangle SpikeSource = new Rectangle(27, 0, 74, 80); // top of the crystal (the rest is "in the floor")
        private const float WallWarningDistance = 267f;   // how far off screen walls start (about 0.8s of ! warning at 260px/s)
        private const float WallReach = 80f;              // how close the wall has to be for the smash to hit it
        private const float DashSmashWindow = 0.5f;       // Seconds after a dash where a smash gets the Smash Dash bonus
        private float _swipeReach = WallReach;            // the swipe keeps the reach it was swung with
        private float _dashSmashTime;                     // Smash Dash keeps the hammer breaking things for this long
        private readonly float SmashCooldown = Shop.Value(Upgrade.Hammer);           // seconds between smashes (a missed smash still uses it)
        private float _smashCooldownLeft;
        private bool _smashPressed;                       // set in HandleInput, used in the next Update

        // Swipe drawn when he smashes, an arc that sweeps down in front of him then fades
        private const float SwipeTime = 0.2f;     // how long the swipe lasts
        private float _swipeLeft;
        private const float SpawnInterval = 1.5f; // seconds between spawns

        // Bool for ending the game
        private bool _isGameOver;

        // Health (walls kill you, spikes take half and rocks take a quarter)
        private const float RockDamage = 0.25f;
        private const float SpikeDamage = 0.5f;
        private float _maxHealth;
        private float _health;
        private float _hurtTime;       // can't be hurt again until this runs out (he blinks)
        private float _flashTime;      // red flash right after a hit
        private float _flashStrength;  // bigger hits flash redder
        private static readonly Color PlayerTint = new Color(185, 205, 255); // a light blue so he matches the cave but still stands out

        // Healing upgrade lets H trade score for health (Might rebalance the cost later)
        private const int HealCost = 150;

        // Points from smashing (replaces the run timer)
        private int _score;
        private const int BoulderPoints = 100;            // falling rocks are harder to time
        private const int WallPoints = 50;

        // Paralax Scrolling for the Inside Cave
        private static readonly (string Name, float Speed, float Y)[] CaveLayerInfo =
        {
            ("Images/Cave/cave_wall", 30f, 0f),
            ("Images/Cave/cave_pillars", 80f, -52f), // pulled up so the ceiling touches the top of the screen
            ("Images/Cave/cave_floor", SpikeSpeed, 20f),  // Want to make it look like the player is walking on the floor
        };
        private Texture2D[] _caveLayers;

        // How long the cave has been scrolling (stops when he crashes)
        private float _scrollTime;

        // Lets add some "Screen" shake
        private float _shakeTime;
        private float _shakeDuration;
        private float _shakeIntensity;
        private Vector2 _shakeOffset;

        // Particle systems
        private DashParticleSystem _dash;
        private RockDustParticleSystem _rockDust;
        private CrashParticleSystem _crash;

        public GameplayScreen()
        {
            // Fades in and out over half a second
            TransitionOnTime = TimeSpan.FromSeconds(0.5);
            TransitionOffTime = TimeSpan.FromSeconds(0.5);
        }

        /// <summary>
        /// Starts a screen shake. Intensity is the max pixel offset.
        /// </summary>
        private void Shake(float intensity, float duration)
        {
            _shakeIntensity = intensity;
            _shakeDuration = duration;
            _shakeTime = duration;
        }

        private void UpdateShake(float dt)
        {
            if (_shakeTime > 0)
            {
                _shakeTime -= dt;
                float strength = _shakeIntensity * (_shakeTime / _shakeDuration); // fades out
                _shakeOffset = new Vector2(RandomHelper.NextFloat(-1, 1), RandomHelper.NextFloat(-1, 1)) * strength;
            }
            else
            {
                _shakeOffset = Vector2.Zero;
            }
        }

        public override void Activate()
        {
            _content = ScreenManager.Game.Content;

            // Loads the various cave layers
            _caveLayers = Array.ConvertAll(CaveLayerInfo, layer => _content.Load<Texture2D>(layer.Name));

            // Loads the music and sounds for the game
            _runnerMusic = _content.Load<Song>("Audio/Music/court_death");
            _crashSound = _content.Load<SoundEffect>("Audio/Sfx/crash");
            _spikeTexture = _content.Load<Texture2D>("Images/Gameplay/stalactite");
            _wallTexture = _content.Load<Texture2D>("Images/Gameplay/wall_stone");
            _crystalWallTexture = _content.Load<Texture2D>("Images/Gameplay/wall_crystal");
            _hammerTexture = _content.Load<Texture2D>("Images/Gameplay/hammer");
            _lanternTexture = _content.Load<Texture2D>("Images/Gameplay/lantern");
            _glowTexture = _content.Load<Texture2D>("Images/Particles/particle");
            _dashIcon = _content.Load<Texture2D>("Images/HUD/CooldownShiftButton");
            _smashIcon = _content.Load<Texture2D>("Images/HUD/SmashCooldownButton");
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            // Fresh player for each run
            _runner = new();
            _runner.LoadContent(_content);

            // Full health (more with the Tough Heart)
            _maxHealth = Shop.Value(Upgrade.Heart);
            _health = _maxHealth;

            // Particle systems are updated/drawn by this screen instead of Game.Components, this allows the shakes to be applyed
            _dash = new DashParticleSystem(ScreenManager.Game, _runner);
            _dash.Initialize();
            _rockDust = new RockDustParticleSystem(ScreenManager.Game);
            _rockDust.Initialize();
            _crash = new CrashParticleSystem(ScreenManager.Game, 5);
            _crash.Initialize();
            // Embers float up and back from the lantern as he runs
            _embers = new SparkParticleSystem(ScreenManager.Game, new Color(255, 150, 40), 0.8f, 0.3f, new Vector2(-90, -70), new Vector2(-40, -30));
            _embers.Initialize();

            // Start the music from the beginning
            MediaPlayer.IsRepeating = true;
            MediaPlayer.Volume = Settings.MusicVolume;  // the title may still be fading in
            MediaPlayer.Play(_runnerMusic);
        }

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

            // Shake and particles keep running so after the crash they can settle
            UpdateShake((float)gameTime.ElapsedGameTime.TotalSeconds);
            _dash.Update(gameTime);
            _rockDust.Update(gameTime);
            _crash.Update(gameTime);
            _embers.Update(gameTime);

            // Only run the game while this screen is on top and the run is going
            if (!IsActive || _isGameOver) return;

            _scrollTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
            UpdateLantern((float)gameTime.ElapsedGameTime.TotalSeconds);

            _runner.Update(gameTime);

            // Spawn a new bolder at a random X every SpawnInterval seconds
            _spawnTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                float x = RandomHelper.NextFloat(RockMinX, RockMaxX);
                SpawnBolder(x);

                // Rare double rock (about 1 in 6) on the far side so they never overlap
                if (RandomHelper.Next(0, 5) == 0)
                    SpawnBolder(x < 400 ? RandomHelper.NextFloat(x + 300, RockMaxX + 60) : RandomHelper.NextFloat(RockMinX - 60, x - 300));
            }

            // Spike every 3-6 seconds, sliding in from the right
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _spikeTimer -= dt;
            _hurtTime -= dt;
            _flashTime -= dt;
            if (_spikeTimer <= 0)
            {
                // About 1 in 3 is a wall (half of them crystal), walls start further out so the ! warning shows first
                bool isWall = RandomHelper.Next(0, 2) == 0;
                _spikes.Add((ScreenManager.GraphicsDevice.Viewport.Width + (isWall ? WallWarningDistance : SpikeSource.Width), isWall, RandomHelper.Next(0, 1) == 0));
                _spikeTimer = RandomHelper.NextFloat(3f, 6f);
            }
            // Down or S smashes a wall in reach, then cools down (even on a miss, so timing it takes skill)
            _smashCooldownLeft = MathF.Max(0, _smashCooldownLeft - dt);
            _swipeLeft = MathF.Max(0, _swipeLeft - dt);
            bool smash = _smashPressed;
            _smashPressed = false;
            if (_smashCooldownLeft == 0 && smash)
            {
                _smashCooldownLeft = SmashCooldown;
                _swipeLeft = SwipeTime;
                _swipeReach = SmashReach();

                // Smash Dash keeps the hammer out for the rest of the dash window, so the dash can carry him into the wall
                if (DashSmashReady) _dashSmashTime = DashSmashWindow;
                SmashInReach(DashSmashReady ? 2 : 1);
            }
            else if (_dashSmashTime > 0)
            {
                _dashSmashTime -= dt;
                SmashInReach(2);
            }

            for (int i = _spikes.Count - 1; i >= 0; i--)
            {
                _spikes[i] = (_spikes[i].X - SpikeSpeed * dt, _spikes[i].IsWall, _spikes[i].Crystal);
                var spikeBounds = SpikeBounds(_spikes[i]);
                if (!Phasing && _runner.Bounds.CollidesWith(spikeBounds))
                {
                    // Walls kill outright, spikes only hurt
                    if (_spikes[i].IsWall)
                    {
                        Crash(new Color(70, 96, 170));
                        return;
                    }
                    Hurt(SpikeDamage, new Color(150, 200, 255));
                    if (_isGameOver) return;
                }
                if (_spikes[i].X < -SpikeSource.Width) _spikes.RemoveAt(i);
            }

            int screenHeight = ScreenManager.GraphicsDevice.Viewport.Height;

            // Move every bolder, then remove ones that leave the screen or hit the player.
            for (int i = _bolders.Count - 1; i >= 0; i--)
            {
                _bolders[i].Update(gameTime, screenHeight);

                // Dust kicked off the top of each falling rock, in the rock's color
                // (top of the hit circle, so the dust follows the rock's size)
                if (RandomHelper.Next(0, 1) == 0)
                {
                    var rock = _bolders[i].Bounds;
                    _rockDust.PlaceDust(rock.Center - new Vector2(0, rock.Radius), _bolders[i].Tint);
                }

                if (_hurtTime <= 0 && !Phasing && _bolders[i].Bounds.CollidesWith(_runner.Bounds))
                {
                    var tint = _bolders[i].Tint;
                    Hurt(RockDamage, tint);
                    if (_isGameOver) return; // the rock stays on screen so you can see what got you

                    // Survived it so the rock breaks on him
                    _crash.PlaceCrash(_bolders[i].Bounds.Center, tint);
                    _bolders.RemoveAt(i);
                    continue;
                }

                if (_bolders[i].IsOffScreen)
                {
                    _bolders.RemoveAt(i);
                }
            }
        }

        // Where the lantern hangs, clipped to his back hip
        private Vector2 LanternHandle() => new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 - 14, _runner.Bounds.Y + 62);

        // The lantern's swing angle (radians), a small bob against his hip
        private float LanternSwing() => 0.12f * MathF.Sin(_lanternTime * 9f);

        /// <summary>
        /// Swings the lantern and moves the embers (a few new ones each second, drifting up and back as he runs)
        /// </summary>
        private void UpdateLantern(float dt)
        {
            _lanternTime += dt;

            // The flame is about 26px below the handle, so embers start there (following the swing)
            var flame = LanternHandle() + new Vector2(-MathF.Sin(LanternSwing()), MathF.Cos(LanternSwing())) * 26;
            if (RandomHelper.Next(0, 3) == 0) _embers.Emit(flame);
        }

        // Top-center of his head, where the smash reaches out from
        private Vector2 SmashPoint() => new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 + 30, _runner.Bounds.Y);

        /// <summary>
        /// Breaks the nearest wall in reach and every falling rock in reach, for points (times bonus)
        /// </summary>
        private void SmashInReach(int bonus)
        {
            float runnerRight = _runner.Bounds.Right;
            int wall = _spikes.FindIndex(s => s.IsWall && SpikeBounds(s).Left - runnerRight < SmashReach() && SpikeBounds(s).Right > _runner.Bounds.Left);
            if (wall >= 0)
            {
                _crash.PlaceCrash(new Vector2(_spikes[wall].X, SpikeBottom - 100), new Color(70, 96, 170));
                Shake(6f, 0.25f);
                _crashSound.Play();
                _spikes.RemoveAt(wall);
                _score += Points(WallPoints) * bonus;
            }

            // Also smashes any falling rock right above him
            for (int b = _bolders.Count - 1; b >= 0; b--)
            {
                var rock = _bolders[b].Bounds;
                if (InSmashRange(_bolders[b]))
                {
                    _crash.PlaceCrash(rock.Center, _bolders[b].Tint);
                    Shake(6f, 0.25f);
                    _crashSound.Play();
                    _bolders.RemoveAt(b);
                    _score += Points(BoulderPoints) * bonus;
                }
            }
        }

        // A rock can be smashed when its hit circle comes within reach of his head
        private bool InSmashRange(Bolders bolder) => Vector2.Distance(bolder.Bounds.Center, SmashPoint()) < bolder.Bounds.Radius + SmashReach();

        // Smash Dash (Power Dash 1) makes a smash right after a dash reach twice as far and score double
        private bool DashSmashReady => Shop.Level(Upgrade.PowerDash) >= 1 && _runner.DashCooldownLeft > 0 && _runner.DashCooldown - _runner.DashCooldownLeft < DashSmashWindow;
        private float SmashReach() => DashSmashReady ? WallReach * 2 : WallReach;

        // Phase Dash (Power Dash 2) nothing can hurt him mid dash, even walls!
        private bool Phasing => Shop.Level(Upgrade.PowerDash) >= 2 && _runner.IsDashing;

        // Circle outline made of small square dots
        private void DrawSwipe()
        {
            // 0 to 1 over the swipe, the arc grows from above his head down to in front of his feet
            float t = 1 - _swipeLeft / SwipeTime;
            float start = -MathHelper.PiOver2;
            float end = start + MathHelper.Pi * 0.85f * MathF.Min(1, t * 2);
            float fade = MathF.Min(1, (1 - t) * 2);   // fades out over the second half
            var center = SmashPoint();

            // The hammer swings from his shoulder along the arc (art points up-right, about -56 degrees)
            var shoulder = new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + 40);
            var tip = center + new Vector2(MathF.Cos(end), MathF.Sin(end)) * _swipeReach;
            float swing = MathF.Atan2(tip.Y - shoulder.Y, tip.X - shoulder.X) + MathHelper.ToRadians(56);
            ScreenManager.SpriteBatch.Draw(_hammerTexture, shoulder, null, Color.White * fade * TransitionAlpha, swing, new Vector2(6, 92), 1f, SpriteEffects.None, 0f);

            // Thick dots along the arc, thinner and dimmer toward the tail
            for (float a = start; a <= end; a += 0.04f)
            {
                float along = (a - start) / (end - start + 0.001f);
                int size = (int)(4 + 6 * along);
                var dot = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * _swipeReach;
                ScreenManager.SpriteBatch.Draw(_pixel, new Rectangle((int)dot.X - size / 2, (int)dot.Y - size / 2, size, size), Color.White * along * fade * TransitionAlpha);
            }
        }

        private void DrawDottedCircle(Vector2 center, float radius, Color color)
        {
            for (int a = 0; a < 32; a++)
            {
                var dot = center + new Vector2(MathF.Cos(a * MathHelper.TwoPi / 32), MathF.Sin(a * MathHelper.TwoPi / 32)) * radius;
                ScreenManager.SpriteBatch.Draw(_pixel, new Rectangle((int)dot.X - 2, (int)dot.Y - 2, 4, 4), color);
            }
        }

        /// <summary>
        /// Draws an ability icon, dim while recharging and refilling from the bottom with the seconds left on it, its key under it when ready
        /// </summary>
        private void DrawCooldownIcon(Texture2D icon, Vector2 position, float cooldownLeft, float cooldown, string key)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            float size = icon.Width * IconScale;

            int fillHeight = (int)(icon.Height * (1 - cooldownLeft / cooldown));
            var fill = new Rectangle(0, icon.Height - fillHeight, icon.Width, fillHeight);
            spriteBatch.Draw(icon, position, null, Color.White * 0.3f * TransitionAlpha, 0f, Vector2.Zero, IconScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(icon, position + new Vector2(0, fill.Y * IconScale), fill, Color.White * TransitionAlpha, 0f, Vector2.Zero, IconScale, SpriteEffects.None, 0f);

            string label = cooldownLeft > 0 ? cooldownLeft.ToString("0.0") : key;
            float labelScale = cooldownLeft > 0 ? 0.45f : 0.35f;
            Vector2 labelSize = font.MeasureString(label) * labelScale;
            float labelY = cooldownLeft > 0 ? position.Y + (size - labelSize.Y) / 2 : position.Y + size + 2;
            spriteBatch.DrawString(font, label, new Vector2(position.X + (size - labelSize.X) / 2, labelY), Color.White * TransitionAlpha, 0f, Vector2.Zero, labelScale, SpriteEffects.None, 0f);
        }

        // Where a pillar is drawn, stretched from the top of the screen to the floor
        private Rectangle WallRectangle(float x) => new Rectangle((int)(x - _wallTexture.Width / 2f), 0, _wallTexture.Width, (int)SpikeBottom + 8);

        /// <summary>
        /// Hit box of a ground obstacle (the whole wall, or just the thin middle of a crystal)
        /// </summary>
        private BoundingRectangle SpikeBounds((float X, bool IsWall, bool Crystal) spike)
        {
            return spike.IsWall
                ? new BoundingRectangle(WallRectangle(spike.X).X, 0, _wallTexture.Width, SpikeBottom)
                : new BoundingRectangle(spike.X - 16, SpikeBottom - 66, 32, 66);
        }

        /// <summary>
        /// Drops a rock at an X position, unless a rock close beside it is still near the top (so they don't stack)
        /// </summary>
        private void SpawnBolder(float x)
        {
            x = MathHelper.Clamp(x, RockMinX, RockMaxX);
            var ahead = _bolders.FindLast(b => MathF.Abs(b.Position.X - x) < 130);
            if (ahead != null && ahead.Position.Y < 250) return;

            // Never faster than the rock ahead in its path, so it can't catch up and overlap
            var bolder = new Bolders(x, ahead?.FallSpeed ?? float.MaxValue);
            bolder.LoadContent(_content);
            _bolders.Add(bolder);
        }

        /// <summary>
        /// Takes damage (unless still blinking from the last hit) and flashes red by how much it hurt, ending the run at 0
        /// </summary>
        private void Hurt(float damage, Color color)
        {
            if (_hurtTime > 0) return;
            _health -= damage;
            if (_health <= 0.001f)
            {
                Crash(color);
                return;
            }
            _hurtTime = 1f;
            _flashTime = 0.4f;
            _flashStrength = MathHelper.Clamp(damage * 2, 0.3f, 1f);
            Shake(4f + 8f * damage, 0.3f);
            _crashSound.Play();
        }

        // Lucky Gem gives more points per smash
        private static int Points(int points) => (int)(points * Shop.Value(Upgrade.Points));

        /// <summary>
        /// Ends the run with a burst in the color of what hit him, shake, crash sound and music off
        /// </summary>
        private void Crash(Color color)
        {
            _crash.PlaceCrash(_runner.Position - new Vector2(0, 60), color);
            _dash.IsEmitting = false; // Stop the trail when he's hit
            Shake(12f, 0.5f);         // Big shake on crash
            _isGameOver = true;       // End the game
            _crashSound.Play();       // Play Crash sound
            MediaPlayer.Stop();       // Stop the music
            _health = 0;              // walls kill outright
            _flashTime = 1f;          // stays red while frozen
            _flashStrength = 1f;
            Shop.AddGems(Shop.GemsFromScore(_score));
            ScreenManager.AddScreen(new GameOverScreen(this, _score));
        }

        public override void HandleInput(GameTime gameTime, InputState input)
        {
            if (input.IsNewKeyPress(Keys.F1)) _showHitboxes = !_showHitboxes;

            // Down or S smashes (Update uses it)
            if (input.IsNewKeyPress(Keys.Down) || input.IsNewKeyPress(Keys.S)) _smashPressed = true;

            // H heals a quarter for some score (only if bought, hurt, and there's enough score)
            if (input.IsNewKeyPress(Keys.H) && Shop.Has(Upgrade.Heal) && !_isGameOver && _health < _maxHealth && _score >= HealCost)
            {
                _score -= HealCost;
                _health = MathF.Min(_health + Shop.Value(Upgrade.Heal), _maxHealth);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var font = ScreenManager.MenuFont;
            var viewport = ScreenManager.GraphicsDevice.Viewport;

            // The world that will get shaked
            var shake = Matrix.CreateTranslation(_shakeOffset.X, _shakeOffset.Y, 0);
            spriteBatch.Begin(transformMatrix: shake);

            // Cave layers scroll left so he looks like he's running deeper in. The screen is still a lil big to allow for shake to look good
            for (int i = 0; i < _caveLayers.Length; i++)
            {
                var texture = _caveLayers[i];
                var color = (i == 0 ? Color.White * 0.6f : Color.White) * TransitionAlpha;
                float offset = _scrollTime * CaveLayerInfo[i].Speed % texture.Width;
                for (float x = -MathF.Floor(offset) - 16; x < viewport.Width + 16; x += texture.Width)
                {
                    spriteBatch.Draw(texture, new Vector2(x, CaveLayerInfo[i].Y), color);
                }
            }

            // Draw the ground obstacles, walls on the floor and crystals tinted blue to match the cave
            foreach (var spike in _spikes)
            {
                if (spike.IsWall)
                {
                    spriteBatch.Draw(spike.Crystal ? _crystalWallTexture : _wallTexture, WallRectangle(spike.X), Color.White * TransitionAlpha);
                }
                else
                {
                    spriteBatch.Draw(_spikeTexture, new Vector2(spike.X - SpikeSource.Width / 2f, SpikeBottom - SpikeSource.Height), SpikeSource, new Color(150, 200, 255) * TransitionAlpha);
                }
            }

            // Blinking red ! at the right edge while a wall is still off screen
            foreach (var spike in _spikes)
            {
                if (spike.IsWall && WallRectangle(spike.X).Left > viewport.Width && (int)(_scrollTime * 10) % 2 == 0)
                {
                    spriteBatch.DrawString(font, "!", new Vector2(viewport.Width - 40, 200), Color.Red * TransitionAlpha, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
                }
            }

            // Smash swipe in front of him
            if (_swipeLeft > 0) DrawSwipe();

            // Smash range ring around his head (bright when ready), rocks inside it get a yellow ring
            if (!_isGameOver && SmashRingOpacity > 0)
            {
                var head = SmashPoint();
                float ready = (_smashCooldownLeft > 0 ? 0.25f : 0.8f) * SmashRingOpacity;
                DrawDottedCircle(head, SmashReach(), (DashSmashReady ? Color.Cyan : Color.White) * ready * TransitionAlpha);
                foreach (var bolder in _bolders)
                {
                    if (InSmashRange(bolder))
                        DrawDottedCircle(bolder.Bounds.Center, bolder.Bounds.Radius, Color.Yellow * ready * TransitionAlpha);
                }
            }

            // Draw Runner (aka the player)
            // He gets his own batch with PointClamp so his pixel art stays sharp (the rest stays smooth)
            spriteBatch.End();
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shake);

            // The lantern hangs on his back hip, drawn first so he covers part of it
            spriteBatch.Draw(_lanternTexture, LanternHandle(), null, Color.White * TransitionAlpha, LanternSwing(), new Vector2(_lanternTexture.Width / 2f, 0), 1f, SpriteEffects.None, 0f);

            // Red flash after a hit (stronger for bigger hits), then blinks while he can't be hurt
            if (_flashTime > 0) _runner.Color = Color.Lerp(PlayerTint, Color.Red, _flashStrength) * TransitionAlpha;
            else if (_hurtTime > 0 && (int)(_hurtTime * 10) % 2 == 0) _runner.Color = PlayerTint * 0.35f * TransitionAlpha;
            else _runner.Color = PlayerTint * TransitionAlpha;
            _runner.Draw(_isGameOver ? new GameTime() : gameTime, spriteBatch); // zero time after a crash freezes his animation
            spriteBatch.End();
            spriteBatch.Begin(transformMatrix: shake);

            // Health bar over his head while he's hurt (green to red as it drops, longer with the Tough Heart)
            if (_health < _maxHealth)
            {
                int barWidth = (int)(60 * _maxHealth);
                float barAlpha = TransitionAlpha;
                int barX = (int)(_runner.Bounds.X + _runner.Bounds.Width / 2 - barWidth / 2f);
                int barY = (int)_runner.Bounds.Y - 16;
                spriteBatch.Draw(_pixel, new Rectangle(barX - 2, barY - 2, barWidth + 4, 9), Color.Black * 0.7f * barAlpha);
                spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * _health / _maxHealth), 5), Color.Lerp(Color.Red, Color.LimeGreen, _health / _maxHealth) * barAlpha);
            }

            // Draw the bolders
            foreach (var bolder in _bolders)
            {
                bolder.Draw(gameTime, spriteBatch);
            }

            if (_showHitboxes)
            {
                var r = _runner.Bounds;
                spriteBatch.Draw(_pixel, new Rectangle((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height), Color.Lime * 0.4f);
                foreach (var bolder in _bolders)
                {
                    DrawDottedCircle(bolder.Bounds.Center, bolder.Bounds.Radius, Color.Red);
                }
                foreach (var spike in _spikes)
                {
                    var b = SpikeBounds(spike);
                    spriteBatch.Draw(_pixel, new Rectangle((int)b.X, (int)b.Y, (int)b.Width, (int)b.Height), Color.Red * 0.4f);
                }
            }

            spriteBatch.End();

            // Lantern light, a flickering warm halo around the flame (looks really cool in the dark)
            spriteBatch.Begin(blendState: BlendState.Additive, transformMatrix: shake);
            var flame = LanternHandle() + new Vector2(-MathF.Sin(LanternSwing()), MathF.Cos(LanternSwing())) * 26;
            float flicker = 0.85f + 0.1f * MathF.Sin(_lanternTime * 13) + 0.05f * MathF.Sin(_lanternTime * 29);
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 150, 50) * 0.6f * flicker * TransitionAlpha, 0f, new Vector2(8), 14f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 190, 90) * flicker * TransitionAlpha, 0f, new Vector2(8), 6f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_glowTexture, flame, null, new Color(255, 235, 150) * flicker * TransitionAlpha, 0f, new Vector2(8), 2.5f, SpriteEffects.None, 0f);
            spriteBatch.End();

            _embers.TransformMatrix = shake;
            _embers.Opacity = TransitionAlpha;
            _embers.Draw(gameTime);

            // Lit by the lantern, draws him again as added light so he pops off the cave (zero time so his animation doesnt run twice)
            if (_flashTime <= 0 && !(_hurtTime > 0 && (int)(_hurtTime * 10) % 2 == 0))
            {
                spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp, transformMatrix: shake);
                _runner.Color = new Color(110, 100, 80) * TransitionAlpha;
                _runner.Draw(new GameTime(), spriteBatch);
                spriteBatch.End();
            }

            // Partical Stuff
            _rockDust.TransformMatrix = shake;
            _dash.TransformMatrix = shake;
            _crash.TransformMatrix = shake;

            _rockDust.Draw(gameTime); // AlphaBlend first
            _dash.Draw(gameTime);     // Additive on top
            _crash.Draw(gameTime);

            // Text dosnt need to shake
            spriteBatch.Begin();

            // Ability icons in the top right
            int iconSize = (int)(_dashIcon.Width * IconScale);
            DrawCooldownIcon(_dashIcon, new Vector2(viewport.Width - 2 * iconSize - 30, 62), _runner.DashCooldownLeft, _runner.DashCooldown, "Shift");
            DrawCooldownIcon(_smashIcon, new Vector2(viewport.Width - iconSize - 16, 62), _smashCooldownLeft, SmashCooldown, "S");
            spriteBatch.DrawString(font, $"Score: {_score}", new Vector2(25, 20), Color.Yellow * TransitionAlpha);

            // Heal hint under the score (green when a heal is possible)
            if (Shop.Has(Upgrade.Heal))
            {
                var healColor = _score >= HealCost && _health < _maxHealth ? Color.LimeGreen : Color.Gray;
                spriteBatch.DrawString(font, $"H: heal (-{HealCost})", new Vector2(25, 62), healColor * TransitionAlpha, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }
    }
}
