using System;
using System.Collections.Generic;
using CollisionExercise.Collisions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Runner_Game.StateManagement;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Past the door, a narrow grey rock passage (about a third of a cave) with boulders rolling in from the right at 3 heights
    /// The floor dips down and humps up smoothly, crouch in a dip to hide. Make it to the arena to clear it (boss goes there later)
    /// Same cave art greyed out with the same parallax, the top and bottom of the screen are cut off so the passage is narrow
    /// </summary>
    public class ArenaScreen : GameScreen
    {
        private ContentManager _content;
        private Texture2D _wall, _pillars, _arena, _rock, _boulder, _skull, _pixel;

        // His weapon from the run, tap the smash button to swing (holding it is still the crouch)
        private Texture2D _hammer, _pickaxe, _smashIcon;
        private ItemId? _weapon;
        private float _smashCooldown, _smashLeft, _swipeLeft, _swipeReach;
        private const float SwipeTime = 0.2f;
        private Tilemap _floor;
        private static readonly Color CeilingColor = new(32, 33, 36);  // solid rock under the floor
        private RockDustParticleSystem _dust;
        private SkinFx _skinFx;

        // The boss waiting in the arena
        private readonly Golem _golem = new();
        private SoundEffect _laserSound, _thudSound;
        private bool _gotHit;               // for the flawless golem achievement
        private bool _arrived;                // done auto running in, now it's his fight
        private bool _smashed;
        private CrashParticleSystem _crash;  // rock bits when one smashes into him
        private SoundEffect _crashSound, _rollSound, _notReadySound;
        private Runner _runner;
        private int _score;

        // Hallway scrolls as he runs, then stops with the arena filling the screen
        private const float HallLength = 864 * 10;     // ~30s of running, a third of a colored cave
        private const float CameraPush = 427f;          // middle of the screen, past this he scrolls the hallway
        private float _scroll;
        private bool InArena => _scroll >= HallLength;

        // The passage is cut down to a band in the middle of the screen, floor about in the middle
        private const float BandTop = 60f, BandBottom = 420f;
        private const float FloorY = 300f;              // top of the floor tiles on flat ground
        private const float Ground = 323f;              // his feet on flat ground (same 23px under the tile top as the caves)
        private const float Half = 58f;                 // half of him, dips go this deep and humps this high
        private const float Ramp = 70f;                 // how long the smooth slope in/out of a dip or hump is
        private readonly List<(float X, float Width, float Depth)> _bumps = new();  // + = dip, - = hump (world x)
        private float _hintTime;
        private bool _hintShown;                        // the faint crouch hint fades out over this

        // Boulders rolling left, Lane 0 = on the floor (jump it or hide), 1 = chest high (hide!), 2 = up high (just don't jump)
        private readonly List<(Vector2 Pos, float Radius, float Speed, float Spin)> _boulders = new();
        private readonly List<Color> _boulderTints = new();  // same rock colors as the caves
        private static readonly (float Height, float Radius, float Speed)[] Lanes = { (30, 30, 380), (100, 34, 330), (205, 34, 300) };
        private float _spawnTimer = 2f, _time, _clearTimer;
        private float Progress => _scroll / HallLength;  // 0 at the door, 1 at the arena

        private float _health = 1f, _hurtTime, _shakeTime;

        // His ability from Gear comes back for the golem fight (dash is handled by Runner)
        private ItemId? _ability;
        private Texture2D _abilityIcon, _rocketTexture;
        private float _abilityCooldown, _abilityLeft, _bubbleLeft, _slowLeft;
        private readonly List<(Vector2 Pos, float Angle)> _rockets = new();
        private readonly List<(float X, int Dir)> _quakes = new();  // Quake Hammer shockwaves along the arena floor
        private ItemId? _armor;
        private float _maxHealth = 1f;
        private bool _revived;
        private float _ringAlpha;
        private bool _f3Held;
        private float _mirrorFlash;  // the laser bouncing off the Mirror Helm back into him
        private SoundEffect _bubbleSound;
        private bool Fighting => _arrived && !_golem.Dead;
        private bool _done;

        public ArenaScreen(int score)
        {
            _score = score;
            TransitionOnTime = TimeSpan.FromSeconds(0.6);
            TransitionOffTime = TimeSpan.FromSeconds(0.4);
        }

        public override void Activate()
        {
            _content ??= new ContentManager(ScreenManager.Game.Services, "Content");
            _wall = _content.Load<Texture2D>("Images/Arena/cave_wall_grey");
            _pillars = _content.Load<Texture2D>("Images/Arena/cave_pillars_grey");
            _arena = _content.Load<Texture2D>("Images/Arena/arena");
            _rock = _content.Load<Texture2D>("Images/Gameplay/spike_rock");
            _boulder = _content.Load<Texture2D>("Images/Arena/boulder");
            _skull = _content.Load<Texture2D>("Images/Arena/skull");
            _hammer = _content.Load<Texture2D>("Images/Gameplay/hammer");
            _pickaxe = _content.Load<Texture2D>("Images/Gameplay/pickaxe");
            _smashIcon = _content.Load<Texture2D>("Images/HUD/SmashCooldownButton");
            _weapon = Shop.Equipped(Slot.Weapon);
            _smashCooldown = _weapon == ItemId.Drill ? 0.4f : _weapon is ItemId w ? Shop.Value(w) : 0f;  // drill is a quick jab in here  // the round one that rolls along the floor
            _floor = new Tilemap(_content, "Images/Arena/floor_tiles_grey", "Maps/cave_floor.txt");
            _dust = new RockDustParticleSystem(ScreenManager.Game);
            _dust.Initialize();
            _skinFx = new SkinFx();
            _golem.Load(_content);
            _laserSound = _content.Load<SoundEffect>("Audio/Sfx/laser4");
            _thudSound = _content.Load<SoundEffect>("Audio/Sfx/shockwave");
            _crash = new CrashParticleSystem(ScreenManager.Game, 5);
            _crash.Initialize();
            _crashSound = _content.Load<SoundEffect>("Audio/Sfx/crash");
            _rollSound = _content.Load<SoundEffect>("Audio/Sfx/swing");
            _notReadySound = _content.Load<SoundEffect>("Audio/Sfx/not_ready");
            _pixel = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _golem.Pixel = _pixel;
            _golem.Font = ScreenManager.BigFont;
            _golem.RockTexture = _rock;  // the cave's falling rocks, not the rolling ones
            _golem.Landed = () => { _shakeTime = 0.35f; _thudSound.Play(0.9f, -0.4f, 0f); for (int k = 0; k < 8; k++) _dust.PlaceDust(_golem.Feet + new Vector2(RandomHelper.NextFloat(-120, 120), -4), new Color(150, 140, 130)); };
            _ability = Shop.Equipped(Slot.Ability);
            _armor = Shop.Equipped(Slot.Armor);
            _maxHealth = _health = _armor == ItemId.MinersVest ? Shop.Value(ItemId.MinersVest) : 1f;  // same armor perks as the caves
            _golem.Mirror = _armor == ItemId.MirrorHelm;
            _golem.Reflected = () => { _crash.PlaceCrash(_golem.Body.Center, new Color(120, 220, 255)); _score += 25; _mirrorFlash = 0.35f; };
            _golem.FloorAt = sx => Ground + Stepped(_scroll + sx);  // follows the hills
            _golem.Knock = dir => _runner.Knock(dir * 650);
            _golem.Glow = _content.Load<Texture2D>("Images/Particles/particle");
            _abilityCooldown = _ability switch { ItemId.PatchUp => 20f, ItemId.BubbleShield => 15f, ItemId.TimeSlow => 20f, ItemId.RocketLauncher => MathF.Abs(Shop.Value(ItemId.RocketLauncher)), _ => 1f };
            _abilityIcon = _ability == null ? null : _ability == ItemId.Dash ? _content.Load<Texture2D>("Images/HUD/CooldownShiftButton") : _content.Load<Texture2D>("Images/HUD/ability_" + System.IO.Path.GetFileName(Shop.Get(_ability.Value).Icon));
            _rocketTexture = _content.Load<Texture2D>("Images/Gameplay/rocket");
            _bubbleSound = _content.Load<SoundEffect>("Audio/Sfx/bubble");

            _runner = new Runner();
            _runner.LoadContent(_content);
            _runner.GroundY = Ground;
            _runner.IdleWhenStill = true;
            _runner.Position = new Vector2(120, Ground);
            if (Settings.TestStart == 5) { _scroll = HallLength; _runner.Position = new Vector2(300, Ground + ArenaDrop); _runner.Scale = 3f; _arrived = true; _golem.Wake(); }  // testing: straight to the boss
            _runner.CanDash = Shop.Equipped(Slot.Ability) == ItemId.Dash;
            _runner.DashCooldown = Shop.Value(ItemId.Dash);
            if (Shop.Equipped(Slot.Armor) == ItemId.SpringBoots) _runner.JumpBoost = Shop.Value(ItemId.SpringBoots);

            // Arena hills, a little different every run
            _hillHeight = RandomHelper.NextFloat(40, 64);
            _hillLength = RandomHelper.NextFloat(300, 400);

            // Dips and humps along the hallway, a little random so every run is different (mostly dips since those are the cover)
            for (float x = 700; x < HallLength - 1000; x += RandomHelper.NextFloat(420, 760))
            {
                float width = RandomHelper.NextFloat(130, 220);
                _bumps.Add((x, width, RandomHelper.Next(0, 3) == 0 ? -Half * 0.7f : Half));
                x += width;
            }
        }

        public override void Unload() => _content.Unload();

        // How far the floor is pushed down (+) or up (-) at a world x, smoothstep ramps so there are no sharp corners
        private float FloorOffset(float worldX)
        {
            foreach (var (x, width, depth) in _bumps)
            {
                if (worldX < x - Ramp || worldX > x + width + Ramp) continue;
                float t = worldX < x ? (worldX - (x - Ramp)) / Ramp : worldX > x + width ? (x + width + Ramp - worldX) / Ramp : 1f;
                return depth * t * t * (3 - 2 * t) + ArenaDrop * Opening(worldX);
            }
            return ArenaDrop * Opening(worldX);
        }

        // Blocky version of the floor, each tile column sits flat and snaps to 16px steps (staircase dips instead of smooth ramps)
        // Hallway as is, the arena floor rolls in smooth hills (flat at the entrance so he lands clean)
        private float Stepped(float worldX)
        {
            float x = worldX - HallLength - 128;
            if (x <= 0) return FloorOffset(worldX);
            return ArenaDrop - _hillHeight * (0.5f - 0.5f * MathF.Cos(x / _hillLength * MathHelper.TwoPi));
        }
        private float _hillHeight, _hillLength;

        // 0 in the narrow passage, 1 in the arena, the floor drops back down to cave height over the last stretch
        private const float OpenLength = 600f;
        private static float Opening(float worldX)
        {
            float t = MathHelper.Clamp((worldX - (HallLength - OpenLength)) / OpenLength, 0, 1);
            return t * t * (3 - 2 * t);
        }
        private const float ArenaDrop = 152f;  // passage floor (300) down to the cave floor (452)

        // Where the swing reaches out from (top-center of his head, or his middle for the big quake swing)
        private Vector2 SmashPoint() => _weapon == ItemId.QuakeHammer
            ? new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 + 10 * _runner.Facing, _runner.Bounds.Y + _runner.Bounds.Height / 3)
            : new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2 + 30 * _runner.Facing, _runner.Bounds.Y);

        // Same swing as the caves, the weapon sweeps along a dotted arc
        private void DrawSwipe(SpriteBatch spriteBatch)
        {
            float t = 1 - _swipeLeft / (_weapon == ItemId.QuakeHammer ? 0.35f : SwipeTime), start = -MathHelper.PiOver2;
            bool around = _weapon == ItemId.QuakeHammer, pick = _weapon == ItemId.Pickaxe;
            float end = start + MathHelper.Pi * (around ? 2f : 0.85f) * MathF.Min(1, t * 2), fade = MathF.Min(1, (1 - t) * 2);
            var center = SmashPoint();
            var shoulder = around ? center : new Vector2(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + 40);
            int f = _runner.Facing;  // swings the way he's facing, everything mirrored when he's turned left
            var tip = center + new Vector2(f * MathF.Cos(end), MathF.Sin(end)) * _swipeReach;
            var tex = pick ? _pickaxe : _hammer;
            var grip = pick ? new Vector2(9, 90) : new Vector2(6, 92);
            float swing = MathF.Atan2(tip.Y - shoulder.Y, f * (tip.X - shoulder.X)) + MathHelper.ToRadians(56);
            if (f < 0) { swing = -swing; grip.X = tex.Width - grip.X; }
            spriteBatch.Draw(tex, shoulder, null, Color.White * fade * TransitionAlpha, swing, grip, around ? _swipeReach / 92f : 1f, f < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            for (float a = start; a <= end; a += 0.04f)
            {
                float along = (a - start) / (end - start + 0.001f);
                int size = (int)(4 + 6 * along);
                var dot = center + new Vector2(f * MathF.Cos(a), MathF.Sin(a)) * _swipeReach;
                spriteBatch.Draw(_pixel, new Rectangle((int)dot.X - size / 2, (int)dot.Y - size / 2, size, size), Color.White * along * fade * TransitionAlpha);
            }
        }

        // None of the cave map's deadly holes in here
        private static int FloorTile(int column, int id) => id >= 9 ? 1 : id;

        public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
        {
            base.Update(gameTime, otherScreenHasFocus, false);
            if (coveredByOtherScreen || _done) return;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Floor follows the dips and humps, on the ground he sticks to it so slopes are smooth instead of little falls
            _runner.MinX = 60; _runner.MaxX = 794;
            _runner.GroundY = Ground + Stepped(_runner.Position.X + _scroll);
            _runner.Scale = MathHelper.Lerp(4f, 3f, Opening(_runner.Position.X + _scroll));  // smaller in the big room
            if (_runner.IsOnGround) _runner.Position = new Vector2(_runner.Position.X, _runner.GroundY);
            bool inFight = Opening(_runner.Position.X + _scroll) > 0.99f;
            _runner.Crouching = !inFight && _runner.IsOnGround && Controls.IsDown(PlayerAction.Smash);  // Down by default, hallway only (Down swings in the fight)
            _runner.FlipToFacing = _arrived;
            _runner.CanDash = _ability == ItemId.Dash;
            if (!_arrived && Opening(_runner.Position.X + _scroll) > 0.3f)  // committed, he runs himself into the arena
            {
                _runner.AutoWalkTo = 520; _runner.Crouching = false;
                if (InArena && MathF.Abs(_runner.Position.X - 520) < 4) { _arrived = true; _runner.AutoWalkTo = null; _golem.Wake(); }  // then it's go time
            }
            _golem.Feet = new Vector2(HallLength - _scroll + _golem.X, Ground + Stepped(_scroll + _golem.X));  // stands on whatever step he's on

            // Smash, breaks every rock in reach in front of him
            _smashLeft = MathF.Max(0, _smashLeft - dt);
            _swipeLeft = MathF.Max(0, _swipeLeft - dt);
            bool inPit = Opening(_runner.Position.X + _scroll) > 0.99f;  // weapon only comes back down in the arena pit
            if (inPit && _weapon != null && _smashLeft > 0 && Controls.IsPressed(PlayerAction.Smash))
            {
                MenuButton.Trigger(new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width - 64, 12, 48, 48));  // still squashes, but bonk
                _notReadySound.Play(0.6f, 0f, 0f);
            }
            if (inPit && _weapon != null && _smashLeft == 0 && Controls.IsPressed(PlayerAction.Smash))
            {
                _smashLeft = _smashCooldown;
                _smashed = true;
                MenuButton.Trigger(new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width - 64, 12, 48, 48));  // squash the HUD icon
                _swipeLeft = _weapon == ItemId.QuakeHammer ? 0.35f : SwipeTime;
                _swipeReach = _weapon switch { ItemId.Pickaxe => 68f, ItemId.QuakeHammer => 120f, _ => 80f };
                _rollSound.Play(0.8f, RandomHelper.NextFloat(-0.15f, 0.15f), 0f);
                var point = SmashPoint();
                for (int b = _boulders.Count - 1; b >= 0; b--)
                {
                    if (Vector2.Distance(_boulders[b].Pos, point) > _swipeReach + _boulders[b].Radius) continue;
                    if (_weapon != ItemId.QuakeHammer && (_boulders[b].Pos.X - _runner.Position.X) * _runner.Facing < -20) continue;  // only in front, the quake hits all around
                    _crash.PlaceCrash(_boulders[b].Pos, _boulderTints[b]);
                    _crashSound.Play(0.6f, 0f, 0f);
                    _score += 50;
                    _boulders.RemoveAt(b); _boulderTints.RemoveAt(b);
                }

                if (_weapon == ItemId.QuakeHammer && _runner.IsOnGround)  // shockwave only when his feet are planted, runs the way he faces
                {
                    _quakes.Add((_runner.Position.X + _runner.Facing * 20, _runner.Facing));
                    _thudSound.Play(0.8f, 0f, 0f);
                    _shakeTime = 0.3f;
                }

                // The golem, hammers do 1, the Quake Hammer does 2, armor just clinks
                int hit = _golem.Smash(point, _swipeReach, _weapon == ItemId.QuakeHammer ? 2 : 1, FistBroken);
                if (hit == 1)
                {
                    _crash.PlaceCrash(_golem.Body.Center, new Color(150, 140, 130));
                    _crashSound.Play(0.8f, -0.3f, 0f);
                    _shakeTime = 0.15f;
                    _score += 100;
                }
                else if (hit == 0) _notReadySound.Play(0.7f, -0.4f, 0f);
            }
            _runner.Update(gameTime);
            if (Fighting) UpdateAbility(dt);
            float bossDt = _slowLeft > 0 ? dt * 0.5f : dt;  // time slow drags the golem down
            if (InArena) _golem.Update(bossDt, _runner, kind => HurtPlayer(new Color(80, 200, 255), kind), FistBroken, () => _rollSound.Play(0.8f, -0.6f, 0f), () => _laserSound.Play(0.7f, -0.2f, 0f));
            if (_done) return;
            _skinFx.Update(dt, _runner, _runner.GroundY, _smashed);
            _smashed = false;
            _dust.Update(gameTime);
            _crash.Update(gameTime);

            // In the hallway he pushes the camera, running past the middle scrolls it instead (stops once the arena lines up)
            if (!InArena && _runner.Position.X > CameraPush)
            {
                float push = MathF.Min(_runner.Position.X - CameraPush, HallLength - _scroll);
                _scroll += push;
                _runner.Position -= new Vector2(push, 0);
                for (int b = 0; b < _boulders.Count; b++) _boulders[b] = (_boulders[b].Pos - new Vector2(push, 0), _boulders[b].Radius, _boulders[b].Speed, _boulders[b].Spin);  // they move with the floor too
            }

            _hurtTime -= dt; _shakeTime -= dt; _mirrorFlash -= dt;
            if (Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3) && !_f3Held) Settings.ShowHitboxes = !Settings.ShowHitboxes;
            _f3Held = Microsoft.Xna.Framework.Input.Keyboard.GetState().IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3);

            _time += dt;
            _hintTime -= dt;

            // Boulders roll in from the right, faster and more often the closer you get (none once the arena is in view)
            _spawnTimer -= dt;
            if (_spawnTimer <= 0 && _scroll < HallLength - 900)
            {
                var (height, radius, speed) = Lanes[RandomHelper.Next(0, Lanes.Length - 1)];
                float rush = 1 + Progress * 0.4f;
                _boulders.Add((new Vector2(ScreenManager.GraphicsDevice.Viewport.Width + radius + 10, Ground - height), radius, speed * rush, 0));
                _boulderTints.Add(Bolders.RockColors[RandomHelper.Next(0, Bolders.RockColors.Length - 1)]);
                _rollSound.Play(0.35f, -0.8f, 0.7f);
                _spawnTimer = RandomHelper.NextFloat(MathHelper.Lerp(1.8f, 0.9f, Progress), MathHelper.Lerp(2.4f, 1.3f, Progress));
            }

            for (int b = _boulders.Count - 1; b >= 0; b--)
            {
                var (pos, radius, speed, spin) = _boulders[b];
                pos.X -= speed * dt;
                spin -= speed * dt / radius;  // rolls, not slides
                if (radius < 32) pos.Y = Ground - radius - 10 + Stepped(pos.X + _scroll);  // floor ones follow the ground (rolling a bit high so a crouch clears it), up humps and down into dips
                _boulders[b] = (pos, radius, speed, spin);
                if (RandomHelper.Next(0, 1) == 0) _dust.PlaceDust(pos + new Vector2(radius, 0), _boulderTints[b]);  // trails off the back (right side, middle)
                if (pos.X < -radius - 20) { _boulders.RemoveAt(b); _boulderTints.RemoveAt(b); continue; }
                if (_hurtTime <= 0 && new BoundingCircle(pos, radius * (radius < 32 ? 0.85f : 1f)).CollidesWith(_runner.Bounds))
                {
                    _health -= _armor == ItemId.HazardHat ? Shop.Value(ItemId.HazardHat) * 1.36f : 0.34f;  // hat softens rocks like the caves
                    _hurtTime = 1f; _gotHit = true;
                    _shakeTime = 0.3f;
                    _crashSound.Play(0.8f, 0f, 0f);
                    _crash.PlaceCrash(pos, _boulderTints[b]);
                    _boulders.RemoveAt(b); _boulderTints.RemoveAt(b);
                    if (_health <= 0.001f) { End(DeathCause.Rock); return; }
                }
            }

            // Golem's down, a beat to watch it crumble then the run counts as cleared
            if (InArena) Achievements.Unlock("arena");
            if (_golem.Dead && (_clearTimer += dt) > 0.8f)
            {
                Achievements.Unlock("golem");
                if (!_gotHit) Achievements.Unlock("flawless");
                _score += 2000; End(DeathCause.Door);
            }
        }

        // Golem attacks land here (same damage as a boulder, then the blink so it can't double hit)
        private void HurtPlayer(Color tint, string kind = "hit")
        {
            if (_hurtTime > 0 || _done) return;
            if (kind == "ground" && _armor == ItemId.HoverBoots) return;  // floats over the shockwaves
            if (_bubbleLeft > 0)  // bubble pops instead
            {
                _bubbleLeft = 0; _hurtTime = 0.6f;
                _crash.PlaceCrash(new Vector2(_runner.Position.X, _runner.Bounds.Y + _runner.Bounds.Height / 2), new Color(120, 220, 255));
                _crashSound.Play(0.6f, 0.4f, 0f);
                return;
            }
            // Same armor perks as the caves (cave rock hit is 0.25 of health, a spike 0.5, golem hits are a bit harder at 0.34)
            float damage = 0.34f;
            if (kind == "rock" && _armor == ItemId.HazardHat) damage = Shop.Value(ItemId.HazardHat) * 1.36f;
            if (kind == "ground" && _armor == ItemId.CrystalPlate) damage = Shop.Value(ItemId.CrystalPlate) * 0.68f;
            if (damage <= 0) return;
            _health -= damage;
            _hurtTime = 1f; _gotHit = true;
            _shakeTime = 0.3f;
            _crashSound.Play(0.8f, 0f, 0f);
            _crash.PlaceCrash(new Vector2(_runner.Position.X, _runner.Bounds.Y + _runner.Bounds.Height / 2), tint);
            if (_health <= 0.001f && _armor == ItemId.PhoenixHeart && !_revived)  // comes back once, like the caves
            {
                _revived = true; _hurtTime = 1.5f;
                _health = MathF.Max(0.05f, _maxHealth * Shop.Value(ItemId.PhoenixHeart));
                _crash.PlaceCrash(new Vector2(_runner.Position.X, _runner.Bounds.Y), new Color(255, 160, 60));
                return;
            }
            if (_health <= 0.001f) End(DeathCause.Rock);
        }

        // Same abilities as the caves, but aimed at the golem
        private void UpdateAbility(float dt)
        {
            _abilityLeft = MathF.Max(0, _abilityLeft - dt);
            _bubbleLeft -= dt; _slowLeft -= dt;
            for (int r = _rockets.Count - 1; r >= 0; r--)
            {
                var (pos, angle) = _rockets[r];
                if (Shop.Value(ItemId.RocketLauncher) < 0)  // homing, turns toward the golem
                {
                    float want = MathF.Atan2(_golem.Body.Center.Y - pos.Y, _golem.Body.Center.X - pos.X);
                    float diff = MathF.Atan2(MathF.Sin(want - angle), MathF.Cos(want - angle));
                    angle += MathHelper.Clamp(diff, -4f * dt, 4f * dt);
                }
                pos += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 700 * dt;
                _rockets[r] = (pos, angle);
                _dust.PlaceDust(pos - new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 28, new Color(170, 160, 150));  // smoke trail
                int hit = _golem.Smash(pos, 12, 2, FistBroken);
                if (hit >= 0 || pos.X < -40 || pos.X > 900 || pos.Y < -40 || pos.Y > 500)
                {
                    if (hit >= 0) { _crash.PlaceCrash(pos, new Color(255, 150, 40)); _crashSound.Play(0.9f, -0.2f, 0f); _shakeTime = 0.2f; if (hit == 1) _score += 150; }
                    _rockets.RemoveAt(r);
                }
            }
            for (int q = _quakes.Count - 1; q >= 0; q--)
            {
                var (x, dir) = _quakes[q];
                x += dir * 500 * dt;
                _quakes[q] = (x, dir);
                if (RandomHelper.Next(0, 1) == 0) _dust.PlaceDust(new Vector2(x, Ground + Stepped(_scroll + x)), new Color(150, 170, 220));
                if (MathF.Abs(x - _golem.Feet.X) < 30)  // rolls into his feet
                {
                    int hit = _golem.Smash(new Vector2(_golem.Feet.X, _golem.Body.Center.Y), 1, 1, FistBroken);
                    if (hit == 1) { _crash.PlaceCrash(new Vector2(x, Ground + Stepped(_scroll + x) - 20), new Color(150, 170, 220)); _score += 100; }
                    _quakes.RemoveAt(q); continue;
                }
                if (x < -40 || x > 900) _quakes.RemoveAt(q);
            }
            if (_ability == null || _ability == ItemId.Dash || !Controls.IsPressed(PlayerAction.Dash)) return;
            MenuButton.Trigger(AbilityRect);
            if (_abilityLeft > 0) { _notReadySound.Play(0.6f, 0f, 0f); return; }
            switch (_ability)
            {
                case ItemId.PatchUp:
                    if (_health >= _maxHealth) return;
                    _health = MathF.Min(_maxHealth, _health + Shop.Value(ItemId.PatchUp) * _maxHealth);
                    for (int k = 0; k < 14; k++) _dust.PlaceDust(_runner.Position + new Vector2(RandomHelper.NextFloat(-25, 25), RandomHelper.NextFloat(-90, -10)), new Color(120, 255, 140));
                    break;
                case ItemId.BubbleShield:
                    _bubbleLeft = Shop.Value(ItemId.BubbleShield);
                    _bubbleSound.Play(0.7f, 0f, 0f);
                    break;
                case ItemId.TimeSlow:
                    _slowLeft = Shop.Value(ItemId.TimeSlow);
                    break;
                case ItemId.RocketLauncher:
                    _rockets.Add((_runner.Muzzle, _runner.Facing > 0 ? 0f : MathF.PI));
                    _runner.ShootTime = Runner.ShootPose;
                    _rollSound.Play(0.9f, -0.6f, 0f);
                    break;
            }
            _abilityLeft = _abilityCooldown;
        }
        private Rectangle AbilityRect => new Rectangle(ScreenManager.GraphicsDevice.Viewport.Width - 120, 12, 48, 48);

        private void FistBroken(Vector2 where)
        {
            _crash.PlaceCrash(where, new Color(150, 140, 130));
            _thudSound.Play(0.6f, 0.3f, 0f);
            _score += 50;
        }

        private void End(DeathCause cause)
        {
            _done = true;
            Shop.AddGems(Shop.GemsFromScore(_score));
            ScreenManager.AddScreen(new GameOverScreen(this, _score, cause));
        }

        public override void Draw(GameTime gameTime)
        {
            var spriteBatch = ScreenManager.SpriteBatch;
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            var shake = _shakeTime > 0 ? Matrix.CreateTranslation(RandomHelper.NextFloat(-5, 5), RandomHelper.NextFloat(-5, 5), 0) : Matrix.Identity;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: shake);

            // Grey cave layers with the same parallax as the caves (back wall slow, pillars faster), raised with the floor
            float wallOffset = _scroll * 30f / 260f % _wall.Width, pillarOffset = _scroll * 80f / 260f % _pillars.Width;
            float lift = FloorY - 452f;  // the cave art is drawn for a floor at 452
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), CeilingColor * TransitionAlpha);
            for (float x = -wallOffset; x < viewport.Width; x += _wall.Width)
                spriteBatch.Draw(_wall, new Rectangle((int)x, (int)(lift + 60), _wall.Width, viewport.Height - (int)(lift + 60)), Color.White * 0.6f * TransitionAlpha);  // stretched down to the bottom
            for (float x = -pillarOffset; x < viewport.Width; x += _pillars.Width)
                spriteBatch.Draw(_pillars, new Rectangle((int)x, (int)(lift + 60), _pillars.Width, viewport.Height - (int)(lift + 60)), Color.White * TransitionAlpha);

            // No separate lair art, the grey cave just keeps going into the arena

            // Floor tiles in thin slices, each slice moved up/down with the floor so the slopes are smooth
            const int Slice = 4;
            for (int sx = 0; sx < viewport.Width; sx += Slice)
            {
                float worldX = sx + _scroll, y = FloorY + Stepped(worldX);
                int id = _floor.TileAtLooping(0, _scroll, sx, FloorTile);
                int inTile = (int)(worldX % Tilemap.TileSize) / Slice * Slice;
                spriteBatch.Draw(_floor.Tileset, new Vector2(sx, y), new Rectangle(id * Tilemap.TileSize + inTile, 0, Slice, Tilemap.TileSize), Color.White * TransitionAlpha);
                spriteBatch.Draw(_pixel, new Rectangle(sx, (int)y + Tilemap.TileSize, Slice, viewport.Height), CeilingColor * TransitionAlpha);
            }


            if (HallLength - _scroll + _golem.X < viewport.Width + 200) _golem.Draw(spriteBatch, TransitionAlpha);

            // Him (blinks while he can't get hurt again)
            if (_hurtTime <= 0 || (int)(_hurtTime * 10) % 2 == 0)
            {
                _skinFx.DrawBehind(spriteBatch, _runner, TransitionAlpha);
                _runner.Color = (_mirrorFlash > 0 ? new Color(120, 210, 255) : Color.White) * TransitionAlpha;  // laser blue while the helm is bouncing it
                _runner.Draw(_done ? new GameTime() : gameTime, spriteBatch);
            }
            _skinFx.Draw(spriteBatch, _pixel, TransitionAlpha);
            foreach (var (pos, angle) in _rockets)  // same look as the caves, nose first
                spriteBatch.Draw(_rocketTexture, pos, null, Color.White * TransitionAlpha, angle, new Vector2(_rocketTexture.Width, _rocketTexture.Height / 2f), 1.3f, SpriteEffects.None, 0f);
            foreach (var (x, qd) in _quakes)  // Quake shockwaves, same jagged bump as the caves
                for (int k = 0; k < 5; k++)
                    spriteBatch.Draw(_pixel, new Rectangle((int)x - qd * k * 14 - 6, (int)(Ground + Stepped(_scroll + x - qd * k * 14)) - 22 + k * 4, 10, 22 - k * 4), new Color(190, 210, 255) * (0.5f - k * 0.09f) * TransitionAlpha);

            // Smash range ring around his head like the caves (dim while recharging, fades up when ready)
            if (Fighting && _weapon != null && Settings.SmashRingOpacity > 0)
            {
                float target = _smashLeft > 0 ? 0.15f : 1f;
                _ringAlpha = target < _ringAlpha ? target : MathHelper.Min(target, _ringAlpha + (float)gameTime.ElapsedGameTime.TotalSeconds * 1.5f);
                var head = SmashPoint();
                float reach = _weapon switch { ItemId.Pickaxe => 68f, ItemId.QuakeHammer => 120f, _ => 80f };
                for (float a = 0; a < MathHelper.TwoPi; a += 0.12f)
                {
                    var p = head + new Vector2(MathF.Cos(a), MathF.Sin(a)) * reach;
                    spriteBatch.Draw(_pixel, new Rectangle((int)p.X - 1, (int)p.Y - 1, 2, 2), Color.White * _ringAlpha * Settings.SmashRingOpacity * TransitionAlpha);
                }
            }
            if (_mirrorFlash > 0)  // the bounced beam, from his helmet into the golem
            {
                var from = new Vector2(_runner.Position.X, _runner.Bounds.Y + 12); var to = _golem.Body.Center;
                float len = Vector2.Distance(from, to), ang = MathF.Atan2(to.Y - from.Y, to.X - from.X);
                spriteBatch.Draw(_pixel, from, null, new Color(120, 210, 255) * 0.9f * TransitionAlpha, ang, new Vector2(0, 0.5f), new Vector2(len, 6), SpriteEffects.None, 0f);
                spriteBatch.Draw(_pixel, from, null, Color.White * TransitionAlpha, ang, new Vector2(0, 0.5f), new Vector2(len, 2), SpriteEffects.None, 0f);
            }
            if (_golem.ShowHitboxes)  // his own hitbox in green
            {
                var hb = _runner.Bounds;
                spriteBatch.Draw(_pixel, new Rectangle((int)hb.X, (int)hb.Y, (int)hb.Width, 2), Color.Lime); spriteBatch.Draw(_pixel, new Rectangle((int)hb.X, (int)(hb.Y + hb.Height) - 2, (int)hb.Width, 2), Color.Lime);
                spriteBatch.Draw(_pixel, new Rectangle((int)hb.X, (int)hb.Y, 2, (int)hb.Height), Color.Lime); spriteBatch.Draw(_pixel, new Rectangle((int)(hb.X + hb.Width) - 2, (int)hb.Y, 2, (int)hb.Height), Color.Lime);
            }
            if (_bubbleLeft > 0 && (_bubbleLeft > 1 || (int)(_bubbleLeft * 8) % 2 == 0))
            {
                var mid = new Vector2(_runner.Position.X, _runner.Bounds.Y + _runner.Bounds.Height / 2);
                for (float a = 0; a < MathHelper.TwoPi; a += 0.12f)
                {
                    var p = mid + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (_runner.Bounds.Height * 0.65f);
                    spriteBatch.Draw(_pixel, new Rectangle((int)p.X - 2, (int)p.Y - 2, 4, 4), new Color(140, 220, 255) * TransitionAlpha);
                }
            }

            // Boulders, the rock sprite spun as they roll
            for (int b = 0; b < _boulders.Count; b++)
            {
                var (pos, radius, _, spin) = _boulders[b];
                var tex = radius < 32 ? _boulder : _rock;
                spriteBatch.Draw(tex, pos, null, _boulderTints[b] * TransitionAlpha, spin, new Vector2(tex.Width / 2f, tex.Height / 2f), radius * 2 / tex.Width, SpriteEffects.None, 0f);
            }
            if (_swipeLeft > 0) DrawSwipe(spriteBatch);
            spriteBatch.End();
            _dust.TransformMatrix = shake;
            _dust.Draw(gameTime);
            _crash.TransformMatrix = shake;
            _crash.Draw(gameTime);

            // Cut the top and bottom off so the passage is a narrow band (HUD sits on the top bar)
            spriteBatch.Begin();
            // Ceiling and floor bars in thin slices that ramp open with the floor, rock tiles (flipped) along the ceiling edge
            for (int sx = 0; sx < viewport.Width; sx += 4)
            {
                float open = Opening(sx + _scroll);
                int top = (int)MathHelper.Lerp(BandTop, 26, open), bottom = (int)MathHelper.Lerp(BandBottom, viewport.Height, open);
                spriteBatch.Draw(_pixel, new Rectangle(sx, 0, 4, top), Color.Black * TransitionAlpha);
                {
                    int id = _floor.TileAtLooping(0, _scroll, sx, FloorTile);
                    int inTile = (int)((sx + _scroll) % Tilemap.TileSize) / 4 * 4;
                    spriteBatch.Draw(_floor.Tileset, new Vector2(sx, top - Tilemap.TileSize + 8), new Rectangle(id * Tilemap.TileSize + inTile, 0, 4, Tilemap.TileSize), Color.White * TransitionAlpha, 0f, Vector2.Zero, 1f, SpriteEffects.FlipVertically, 0f);
                }
                spriteBatch.Draw(_pixel, new Rectangle(sx, bottom, 4, viewport.Height - bottom), Color.Black * TransitionAlpha);
            }
            spriteBatch.End();

            // HUD, score, health and how close the arena is
            spriteBatch.Begin();
            var big = ScreenManager.BigFont;
            spriteBatch.DrawString(big, $"Score: {_score}", new Vector2(25, 20), Color.Yellow * TransitionAlpha, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, new Rectangle(25, 64, 120, 10), Color.Black * 0.6f * TransitionAlpha);
            spriteBatch.Draw(_pixel, new Rectangle(25, 64, (int)(120 * MathF.Max(0, _health / _maxHealth)), 10), Color.Lerp(Color.Red, Color.LimeGreen, _health / _maxHealth) * TransitionAlpha);
            // How far to the arena, a bar across the top
            int barW = 300, barX = viewport.Width / 2 - barW / 2;
            spriteBatch.Draw(_pixel, new Rectangle(barX - 2, 16, barW + 4, 12), Color.White * TransitionAlpha);  // white outline
            spriteBatch.Draw(_pixel, new Rectangle(barX, 18, barW, 8), Color.Black * TransitionAlpha);
            spriteBatch.Draw(_skull, new Vector2(barX + barW + 4, 22 - 10.5f), null, Color.White * TransitionAlpha, 0f, Vector2.Zero, 3f, SpriteEffects.None, 0f);  // the lair
            spriteBatch.Draw(_pixel, new Rectangle(barX, 18, (int)(barW * Progress), 8), new Color(90, 150, 90) * TransitionAlpha);
            if (InArena)  // boss health over the progress bar
            {
                spriteBatch.Draw(_pixel, new Rectangle(barX, 18, barW, 8), Color.Black * TransitionAlpha);
                spriteBatch.Draw(_pixel, new Rectangle(barX, 18, barW * _golem.Hp / Golem.MaxHp, 8), (_golem.Immune ? new Color(120, 170, 255) : new Color(200, 50, 40)) * TransitionAlpha);
            }
            string goal = InArena ? (_golem.Dying ? "The golem crumbles!" : _golem.Immune ? "Stone Golem (armored!)" : "Stone Golem") : "Reach the arena";
            var goalSize = ScreenManager.MenuFont.MeasureString(goal) * 0.4f;
            spriteBatch.DrawString(ScreenManager.MenuFont, goal, new Vector2(viewport.Width / 2 - goalSize.X / 2, 32), Color.White * TransitionAlpha, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
            if (_weapon != null && Opening(_runner.Position.X + _scroll) > 0.99f)  // shows up once the weapon is back
            {
                var iconPos = new Vector2(viewport.Width - 64, 12);
                float iconScale = 48f / _smashIcon.Width, ready = _smashCooldown > 0 ? 1 - _smashLeft / _smashCooldown : 1;
                int fillH = (int)(_smashIcon.Height * ready);
                var sq = MenuButton.Squash(new Rectangle((int)iconPos.X, (int)iconPos.Y, 48, 48), false);
                var sqScale = new Vector2(sq.Width / (float)_smashIcon.Width, sq.Height / (float)_smashIcon.Height);
                spriteBatch.Draw(_smashIcon, sq.Location.ToVector2(), null, Color.White * 0.3f * TransitionAlpha, 0f, Vector2.Zero, sqScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(_smashIcon, sq.Location.ToVector2() + new Vector2(0, (_smashIcon.Height - fillH) * sqScale.Y), new Rectangle(0, _smashIcon.Height - fillH, _smashIcon.Width, fillH), Color.White * TransitionAlpha, 0f, Vector2.Zero, sqScale, SpriteEffects.None, 0f);
            }

            if (Fighting && _abilityIcon != null)
            {
                float ready = _ability == ItemId.Dash ? (_runner.DashCooldown > 0 ? 1 - _runner.DashCooldownLeft / _runner.DashCooldown : 1) : 1 - _abilityLeft / _abilityCooldown;
                var sq = MenuButton.Squash(AbilityRect, false);
                int fillH = (int)(_abilityIcon.Height * ready);
                var sc = new Vector2(sq.Width / (float)_abilityIcon.Width, sq.Height / (float)_abilityIcon.Height);
                spriteBatch.Draw(_abilityIcon, sq.Location.ToVector2(), null, Color.White * 0.3f * TransitionAlpha, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
                spriteBatch.Draw(_abilityIcon, sq.Location.ToVector2() + new Vector2(0, (_abilityIcon.Height - fillH) * sc.Y), new Rectangle(0, _abilityIcon.Height - fillH, _abilityIcon.Width, fillH), Color.White * TransitionAlpha, 0f, Vector2.Zero, sc, SpriteEffects.None, 0f);
            }

            // Crouch hint along the bottom for the first half of the hallway, fades out as you pass the middle
            _hintTime = InArena ? 0 : MathHelper.Clamp((0.5f - Progress) / 0.1f, 0, 1);
            if (_hintTime > 0)
            {
                string tip = $"Hold {Controls.Label(PlayerAction.Smash)} to crouch";
                var tipSize = ScreenManager.MenuFont.MeasureString(tip) * 0.45f;
                float fade = _hintTime * 0.8f;
                spriteBatch.DrawString(ScreenManager.MenuFont, tip, new Vector2(viewport.Width / 2 - tipSize.X / 2, viewport.Height - tipSize.Y - 16), Color.White * fade * TransitionAlpha, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            }
            spriteBatch.End();
        }
    }
}
