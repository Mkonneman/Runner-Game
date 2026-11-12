using System;
using System.Collections.Generic;
using CollisionExercise.Collisions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// The Mecha-stone Golem boss at the end of the arena. Sheet is 10x10 cells of 100px and faces right
    /// Attacks: swipes (with a ! warning) when you get close, walks over, jumps at you, fist shot, an eye laser that sweeps in
    /// from the left or right, and two shielded phases (curled armor / crystal glow) where cave rocks rain from the ceiling
    /// </summary>
    public class Golem
    {
        private enum State { Waiting, Idle, Walk, Windup, Swipe, Shoot, Laser, Armor, Glow, Jump, Pound, Dying }

        // Sheet rows
        private const int RowIdle = 0, RowGlow = 1, RowShoot = 2, RowArmor = 3, RowPunch = 4, RowLaser = 5, RowWalk = 6, RowDie = 7;
        private const int Cell = 100;
        public const float Scale = 3.5f;
        public const int MaxHp = 30;

        private Texture2D _sheet, _laser, _fist;
        public Texture2D RockTexture, Pixel;   // set by the arena (the cave's falling rock, and a white pixel)
        public SpriteFont Font;                // for the ! warning
        public Action Landed;                  // he hit the floor after a jump or a pound (the arena shakes the screen)
        public Action<int> Knock;             // his swing landed, push the player this way
        public bool Mirror;                    // Mirror Helm on, his laser bounces back into him
        public Action Reflected;               // the laser hit the helm and bounced

        // Ground pound shockwaves sliding along the floor both ways (x, direction), jump over them like the Quake Hammer's
        private readonly List<(float X, int Dir)> _waves = new();
        private const float WaveSpeed = 380f;
        private float _reflectTimer;
        private readonly List<(Vector2 Pos, float Age, float Spin, Color Color, float Size)> _chunks = new();
        private Texture2D _shard;  // same rock shard the crash particles use  // rock bits kicked up by the shockwaves
        private readonly List<(Vector2 Pos, float Age)> _motes = new();  // little blue specks drifting up off the shield
        public Texture2D Glow;
        private const float ChunkLife = 0.4f;  // quick fade
        public bool ShowHitboxes => Settings.ShowHitboxes;
        private State _state = State.Waiting;
        private float _t;
        private float _hitFlash, _bonk;  // bonk = something hit the shield
        private bool _fired;
        private State _last;
        private bool _faceLeft = true;
        public int Hp { get; private set; } = MaxHp;

        public float X = 720f;                 // where it stands in the arena (screen x, the arena doesn't scroll)
        public Vector2 Feet;                   // on screen, set by the arena every frame (plus the jump height)
        private float _jumpFrom, _jumpTo, _hop;
        public bool Dead => _state == State.Dying && _t > 2.2f;
        public bool Dying => _state == State.Dying;
        public bool Immune => (_state == State.Armor || _state == State.Glow) && _t > 0.3f && _t < RockTime + 0.5f;
        private const float RockTime = 3.4f;   // how long the rock rain lasts

        // Close enough and he swipes (unless he's busy with another move)
        private const float SwipeRange = 150f, WindupTime = 0.6f;
        private const float LingerTime = 1.2f;  // seconds up close before he bothers swiping
        private float _closeTime;
        private static readonly float[] SlamTimes = { 0.55f, 1.9f };  // second slam comes a good while after the first
        private int _shields;                   // how many health thresholds he's shielded at so far
        private const float ThrowAt = 0.95f;   // the rock throw holds its wind-up this long
        private float _push;                    // slide from getting hit
        private float _wanderX = 600;           // spot he strolls to between moves
        private bool _strolling;
        private const float SwipeHalfW = 28 * Scale, SwipeH = 38 * Scale;  // cone: full width at the floor, 30% of it at the top  // the swing's reach box, in front of him

        // Fist projectiles (position, going left?)
        private readonly List<(Vector2 Pos, Vector2 Vel)> _fists = new();
        private const float FistSpeed = 430f;
        private Vector2 _aim;
        private float SwipeX => Feet.X + (_faceLeft ? -1 : 1) * 32 * Scale;
        private static float ConeHalf(float heightUp) => SwipeHalfW * MathHelper.Lerp(1f, 0.3f, MathHelper.Clamp(heightUp / SwipeH, 0, 1));

        // Laser storm (instead of rocks in some shield phases), beams straight down at random spots, they don't chase you
        private bool _storm;
        private readonly List<(float X, float Age)> _beams = new();
        private const float BeamWarn = 0.75f, BeamOn = 0.3f;
        private Vector2 Hand => Drawn + new Vector2((_faceLeft ? -42 : 42) * Scale, -33 * Scale);  // tip of the stretched arm, pixel (92,37) of the shoot frame

        // Eye laser, comes in at an angle out of his one eye and sweeps across the floor
        private const float LaserCharge = 0.7f, LaserTime = 1.1f;
        private bool LaserOn => _state == State.Laser && _t > LaserCharge && _t < LaserCharge + LaserTime;
        private bool _laserFromRight;
        private float _laserAngle;
        private Vector2 Eye => Feet + new Vector2((_faceLeft ? -1.5f : 1.5f) * Scale, -37 * Scale - _hop);  // the cyan crystal, pixel (51,33) of the frame

        // Falling cave rocks (pos, speed, tint, warning time before it drops, spin)
        private struct Rock { public Vector2 Pos; public float Speed, Warn, Spin; public Color Color; }
        private readonly List<Rock> _rocks = new();
        private float _rockTimer;
        private const float RockSize = 64f;
        private float _floorY;
        public Func<float, float> FloorAt;      // ground height at a screen x (the arena has hills)
        private float Floor(float x) => FloorAt?.Invoke(x) ?? _floorY;

        public void Load(ContentManager content)
        {
            _sheet = content.Load<Texture2D>("Images/Boss/golem");
            _laser = content.Load<Texture2D>("Images/Boss/golem_laser");
            _fist = content.Load<Texture2D>("Images/Boss/golem_fist");
            _shard = content.Load<Texture2D>("Images/Particles/shard");
        }

        private Vector2 Drawn => Feet - new Vector2(0, _hop);
        public BoundingCircle Body => new(Drawn - new Vector2(0, 23 * Scale), 22 * Scale);
        public float Front => Feet.X - 24 * Scale;

        public void Wake() { if (_state == State.Waiting) Go(State.Idle); }

        private void Go(State s)
        {
            _last = _state; _state = s; _t = 0; _fired = false;
            if (s == State.Idle) _wanderX = RandomHelper.NextFloat(140, 760);  // somewhere new to stroll to
        }

        private static bool Hits(BoundingRectangle r, Vector2 p, float pad) =>
            p.X > r.X - pad && p.X < r.X + r.Width + pad && p.Y > r.Y - pad && p.Y < r.Y + r.Height + pad;

        public void Update(float dt, Runner runner, Action<string> hurt, Action<Vector2> fistBroken, Action shoot, Action laserSound)
        {
            _t += dt;
            _hitFlash -= dt; _bonk -= dt;
            _floorY = Feet.Y;
            float rage = 1 - Hp / (float)MaxHp;
            var me = runner.Bounds;
            float playerX = me.X + me.Width / 2, gap = playerX - Feet.X;
            bool close = MathF.Abs(gap) < SwipeRange;
            _closeTime = close ? _closeTime + dt : 0;  // how long you've been hanging around in his face
            bool lingering = _closeTime > LingerTime;

            if (_state != State.Dying && _state != State.Swipe) _faceLeft = gap < 0;  // always squared up to you (the swing itself commits)
            switch (_state)
            {
                case State.Idle:
                    // Wanders around while he thinks, keeps him on the move
                    _strolling = MathF.Abs(_wanderX - X) > 12;
                    if (_strolling) X += MathF.Sign(_wanderX - X) * MathHelper.Lerp(110, 170, rage) * dt;
                    _faceLeft = gap < 0;
                    if (lingering && _t > 0.25f) { Go(State.Windup); _closeTime = 0; break; }  // stuck around too long, swipe
                    if (_t > MathHelper.Lerp(1.1f, 0.45f, rage))
                    {
                        // Shield only when his health drops past 75%, 50% and 25% (once each), otherwise a normal move
                        bool shieldDue = _shields < 3 && Hp <= MaxHp * (0.75f - 0.25f * _shields);
                        int roll = RandomHelper.Next(0, 11);
                        State next = shieldDue ? State.Armor : roll switch
                        {
                            0 => MathF.Abs(gap) > 220 ? State.Walk : State.Shoot,
                            1 or 2 => State.Shoot,
                            3 or 4 => State.Pound,
                            >= 5 and <= 9 => State.Jump,  // his favorite, leaps all over the place
                            _ => State.Laser,
                        };
                        if (shieldDue) _shields++;
                        Go(next);
                        if (next == State.Armor || next == State.Glow) _storm = RandomHelper.Next(0, 2) == 0;  // a third of the time it's lasers instead of rocks
                        if (next == State.Laser) { _laserFromRight = gap > 0; _faceLeft = !_laserFromRight; }  // always out toward the side you're on
                        if (next == State.Jump) { _jumpFrom = X; _jumpTo = MathHelper.Clamp(playerX, 120, 760); _faceLeft = gap < 0; }
                    }
                    break;

                case State.Walk:  // stomps over until he's in swiping range
                    _faceLeft = gap < 0;
                    X += MathF.Sign(gap) * MathHelper.Lerp(90, 160, rage) * dt;
                    if (close) Go(State.Idle);  // got there, back to picking a move
                    else if (_t > 3f) Go(State.Idle);
                    break;

                case State.Windup:  // arm goes back with a ! over his head, then the swipe
                    if (_t > WindupTime * MathHelper.Lerp(1f, 0.7f, rage)) Go(State.Swipe);
                    break;

                case State.Swipe:
                    if (!_fired && _t > 0.08f)
                    {
                        _fired = true;
                        // Lowest bit of him inside the cone's height is where it's widest, check there
                        float low = MathF.Min(me.Y + me.Height, Feet.Y);
                        if (me.Y + me.Height > Feet.Y - SwipeH && MathF.Abs(playerX - SwipeX) < ConeHalf(Feet.Y - low) + me.Width / 2) { hurt("hit"); Knock?.Invoke(_faceLeft ? -1 : 1); }  // and sends you flying
                        shoot();
                    }
                    if (_t > 0.45f) Go(State.Idle);
                    break;

                case State.Jump:  // squat, big arc to where you were standing, slam
                    float air = MathHelper.Clamp((_t - 0.2f) / 0.65f, 0, 1);
                    X = MathHelper.Lerp(_jumpFrom, _jumpTo, air);
                    _hop = MathF.Sin(air * MathF.PI) * 170;
                    if (!_fired && air >= 1)
                    {
                        _fired = true; _hop = 0;
                        Landed?.Invoke();
                        if (runner.IsOnGround && MathF.Abs(playerX - Feet.X) < 140) hurt("ground");  // right under him
                    }
                    if (_t > 1.15f) Go(State.Idle);
                    break;

                case State.Pound:  // raises up, slams both fists, shockwaves run out both ways (twice when he's angry)
                    // One slam, and below half health a second one well after the first
                    int slams = rage > 0.5f ? 2 : 1;
                    for (int k = 0; k < slams; k++)
                        if (_t - dt < SlamTimes[k] && _t >= SlamTimes[k])
                        {
                            Landed?.Invoke();
                            Sfx.Play("slam", 0.7f);
                            _waves.Add((Feet.X - 40, -1)); _waves.Add((Feet.X + 40, 1));
                        }
                    if (_t > SlamTimes[slams - 1] + 0.3f) Go(State.Idle);
                    break;

                case State.Shoot:
                    if (!_fired && _t < ThrowAt - 0.25f) _aim = new Vector2(playerX, me.Y + me.Height * 0.6f);  // aim line follows you, then locks right before the throw
                    if (!_fired && _t > ThrowAt)
                    {
                        var aim = _aim - Hand;
                        _fists.Add((Hand, Vector2.Normalize(aim) * FistSpeed * (1 + rage * 0.5f))); _fired = true; shoot();
                    }
                    if (_t > ThrowAt + 0.35f) Go(State.Idle);
                    break;

                case State.Laser:
                    // Starts steep near his feet on the firing side and sweeps out flatter toward the wall
                    float sweep = MathHelper.Clamp((_t - LaserCharge) / LaserTime, 0, 1);
                    float down = MathHelper.Lerp(MathHelper.ToRadians(60), MathHelper.ToRadians(10), sweep);
                    _laserAngle = _laserFromRight ? down : MathF.PI - down;
                    if (!_fired && _t > LaserCharge) { _fired = true; laserSound(); }
                    if (LaserOn)
                    {
                        var dir = new Vector2(MathF.Cos(_laserAngle), MathF.Sin(_laserAngle));
                        var to = new Vector2(playerX, me.Y + me.Height / 2) - Eye;
                        float along = Vector2.Dot(to, dir), off = MathF.Abs(to.X * dir.Y - to.Y * dir.X);
                        if (along > 0 && off < me.Width / 2 + 8)
                        {
                            if (!Mirror) hurt("laser");
                            else if ((_reflectTimer -= dt) <= 0) { _reflectTimer = 0.3f; Reflected?.Invoke(); if (Hp > 1) { Hp--; _hitFlash = 0.12f; } }  // bounces back into him
                        }
                    }
                    if (_t > LaserCharge + LaserTime + 0.2f) Go(State.Idle);
                    break;

                case State.Armor:
                case State.Glow:
                    if (_storm)
                    {
                        if (_t > 0.4f && _t < RockTime && (_rockTimer -= dt) <= 0)
                        {
                            _beams.Add((RandomHelper.NextFloat(40, 814), 0));  // anywhere, not aimed at you
                            _rockTimer = MathHelper.Lerp(0.45f, 0.3f, rage);  // a bit more room between beams
                        }
                    }
                    else if (_t > 0.4f && _t < RockTime && (_rockTimer -= dt) <= 0)
                    {
                        // Every other one lands right where you're standing so you have to keep moving
                        float x = RandomHelper.Next(0, 2) == 0 ? playerX + RandomHelper.NextFloat(-30, 30) : RandomHelper.NextFloat(40, 814);  // a third aimed at you
                        _rocks.Add(new Rock { Pos = new Vector2(x, -RockSize), Warn = 0.85f, Color = Bolders.RockColors[RandomHelper.Next(0, Bolders.RockColors.Length - 1)], Spin = RandomHelper.NextFloat(0, 6) });
                        _rockTimer = MathHelper.Lerp(0.6f, 0.38f, rage);
                    }
                    if (_t > RockTime + 0.8f) Go(State.Idle);
                    break;
            }
            if (_state != State.Jump) _hop = 0;
            X = MathHelper.Clamp(X + _push * dt, 90, 770);
            _push *= MathF.Pow(0.03f, dt);

            for (int i = _fists.Count - 1; i >= 0; i--)
            {
                var (p, v) = _fists[i];
                p += v * dt;
                _fists[i] = (p, v);
                if (p.Y > Floor(p.X) - 20) { fistBroken(new Vector2(p.X, Floor(p.X) - 20)); _fists.RemoveAt(i); continue; }  // smashes on the floor
                if (FistBox(p).CollidesWith(me)) { hurt("hit"); _fists.RemoveAt(i); continue; }
                if (p.X < -80 || p.X > 934) _fists.RemoveAt(i);
            }

            for (int i = _waves.Count - 1; i >= 0; i--)
            {
                var (x, dir) = _waves[i];
                x += dir * WaveSpeed * dt;
                _waves[i] = (x, dir);
                if (runner.IsOnGround && MathF.Abs(x - playerX) < 22) { hurt("ground"); _waves.RemoveAt(i); continue; }
                if (x < -40 || x > 900) _waves.RemoveAt(i);
            }

            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                var (bx, age) = _beams[i];
                age += dt;
                if (age - dt < BeamWarn && age >= BeamWarn) Sfx.Play("beam", 0.35f, 0.2f);  // zap when it turns on
                _beams[i] = (bx, age);
                if (age > BeamWarn && age < BeamWarn + BeamOn && MathF.Abs(playerX - bx) < 8 + me.Width / 2)
                {
                    if (!Mirror) hurt("laser");
                    else if ((_reflectTimer -= dt) <= 0) { _reflectTimer = 0.3f; Reflected?.Invoke(); if (Hp > 1) { Hp--; _hitFlash = 0.12f; } }
                }
                if (age > BeamWarn + BeamOn) _beams.RemoveAt(i);
            }
            for (int i = _chunks.Count - 1; i >= 0; i--)
            {
                var c = _chunks[i];
                c.Age += dt; c.Pos.Y -= 30 * dt; c.Spin += dt;
                if (c.Age > ChunkLife) _chunks.RemoveAt(i); else _chunks[i] = c;
            }
            if (Immune && RandomHelper.Next(0, 2) == 0)
            {
                float a = RandomHelper.NextFloat(0, MathHelper.TwoPi);
                _motes.Add((Body.Center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * (Body.Radius + 14), 0));
            }
            for (int i = _motes.Count - 1; i >= 0; i--)
            {
                var m = _motes[i];
                m.Age += dt; m.Pos.Y -= 40 * dt;
                if (m.Age > 0.8f) _motes.RemoveAt(i); else _motes[i] = m;
            }

            for (int i = _rocks.Count - 1; i >= 0; i--)
            {
                var r = _rocks[i];
                if ((r.Warn -= dt) <= 0) { r.Speed += 1400 * dt; r.Pos.Y += r.Speed * dt; r.Spin += 4 * dt; }
                if (Hits(me, r.Pos, RockSize * 0.25f)) { hurt("rock"); fistBroken(r.Pos); _rocks.RemoveAt(i); continue; }
                if (r.Pos.Y > Floor(r.Pos.X) - RockSize * 0.3f) { fistBroken(new Vector2(r.Pos.X, Floor(r.Pos.X) - 10)); _rocks.RemoveAt(i); continue; }
                _rocks[i] = r;
            }
        }

        private static BoundingRectangle FistBox(Vector2 p) => new(p.X - 14 * 3, p.Y - 7 * 3, 28 * 3, 14 * 3);

        /// <summary>
        /// A smash from point with this reach, breaks fists and rocks in the way and hurts the golem. Returns 1 hit, 0 shielded, -1 missed
        /// </summary>
        public int Smash(Vector2 point, float reach, int damage, Action<Vector2> fistBroken)
        {
            for (int i = _fists.Count - 1; i >= 0; i--)
                if (Vector2.Distance(_fists[i].Pos, point) < reach + 30) { fistBroken(_fists[i].Pos); _fists.RemoveAt(i); }
            for (int i = _rocks.Count - 1; i >= 0; i--)
                if (_rocks[i].Warn <= 0 && Vector2.Distance(_rocks[i].Pos, point) < reach + RockSize / 2) { fistBroken(_rocks[i].Pos); _rocks.RemoveAt(i); }
            if (_state == State.Waiting || Dying || Vector2.Distance(Body.Center, point) > reach + Body.Radius) return -1;
            if (Immune) { _bonk = 0.25f; Sfx.Play("shield_bonk"); return 0; }
            Hp = Math.Max(0, Hp - damage);
            if (_state != State.Jump) _push = MathF.Sign(Body.Center.X - point.X + 0.01f) * 260 * damage;  // shoved away from the hit
            _hitFlash = 0.12f;
            Sfx.Play(Hp == 0 ? "golem_die" : "golem_hit", Hp == 0 ? 0.9f : 0.6f);
            if (Hp == 0) { Go(State.Dying); _fists.Clear(); _rocks.Clear(); _waves.Clear(); _beams.Clear(); }
            return 1;
        }

        public void Draw(SpriteBatch spriteBatch, float alpha)
        {
            int row, frame;
            switch (_state)
            {
                case State.Walk: row = RowWalk; frame = (int)(_t / 0.09f) % 10; break;
                case State.Windup: row = RowPunch; frame = Math.Min(3, (int)(_t / (WindupTime / 4))); break;
                case State.Swipe: row = RowPunch; frame = Math.Min(6, 4 + (int)(_t / 0.07f)); break;
                case State.Shoot: row = RowShoot; frame = _t < ThrowAt ? Math.Min(2, (int)(_t / 0.08f)) : Math.Min(8, 3 + (int)((_t - ThrowAt) / 0.05f)); break;  // holds the wind-up, then the arm shoots out
                case State.Laser: row = RowLaser; frame = Math.Min(6, (int)(_t / 0.1f)); break;
                case State.Pound: { bool slamming = MathF.Abs(_t - SlamTimes[0]) < 0.25f || MathF.Abs(_t - SlamTimes[1]) < 0.25f; float raise = _t < SlamTimes[0] ? _t : _t - SlamTimes[0] - 0.25f; row = RowPunch; frame = slamming ? 6 : Math.Min(3, (int)(raise / 0.1f)); break; }  // arm up, slam, arm up again
                case State.Jump: row = RowArmor; frame = _t < 0.2f || _t > 0.85f ? 2 : 4; break;  // tucks a bit in the air
                case State.Armor:
                    row = RowArmor;
                    frame = Math.Clamp(_t < 0.4f ? (int)(_t / 0.05f) : _t < RockTime + 0.4f ? 7 : 7 - (int)((_t - RockTime - 0.4f) / 0.05f), 0, 7); break;
                case State.Glow:
                    row = RowGlow;
                    frame = _t < 0.4f ? (int)(_t / 0.1f) : _t < RockTime + 0.4f ? 3 + (int)(_t / 0.12f) % 3 : Math.Min(7, 6 + (int)((_t - RockTime - 0.4f) / 0.15f)); break;
                case State.Dying:
                    int d = Math.Min(13, (int)(_t / 0.1f));
                    row = RowDie + d / 10; frame = d % 10; break;
                default: if (_state == State.Idle && _strolling) { row = RowWalk; frame = (int)(_t / 0.09f) % 10; } else { row = RowIdle; frame = (int)(_t / 0.15f) % 4; } break;
            }
            var color = _hitFlash > 0 ? new Color(255, 160, 160) : Immune ? new Color(150, 190, 255) : Color.White;
            spriteBatch.Draw(_sheet, Drawn, new Rectangle(frame * Cell, row * Cell, Cell, Cell), color * alpha, 0f, new Vector2(50, 70), Scale,
                _faceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            // Shielded: a pulsing blue bubble with a dotted rim so it's obvious hits won't land (flashes when something bonks it)
            if (Immune && Glow != null)  // faint blue bubble, brighter for a blink when something bonks it
            {
                float r = Body.Radius + 22 + MathF.Sin(_t * 5) * 3;
                spriteBatch.Draw(Glow, Body.Center, null, new Color(90, 170, 255) * (_bonk > 0 ? 0.55f : 0.3f) * alpha, 0f, new Vector2(Glow.Width / 2f, Glow.Height / 2f), r * 2.4f / Glow.Width, SpriteEffects.None, 0f);
            }
            foreach (var (p, age) in _motes)
                spriteBatch.Draw(Pixel, new Rectangle((int)p.X - 1, (int)p.Y - 1, 3, 3), new Color(150, 210, 255) * (1 - age / 0.8f) * 0.8f * alpha);

            if (ShowHitboxes)  // F3 in the arena: body (yellow), swipe reach (red), fists (orange), laser line (cyan), falling rocks (white)
            {
                void Box(Rectangle r, Color c) { spriteBatch.Draw(Pixel, new Rectangle(r.X, r.Y, r.Width, 2), c); spriteBatch.Draw(Pixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), c); spriteBatch.Draw(Pixel, new Rectangle(r.X, r.Y, 2, r.Height), c); spriteBatch.Draw(Pixel, new Rectangle(r.Right - 2, r.Y, 2, r.Height), c); }
                var b = Body;
                for (float a = 0; a < MathHelper.TwoPi; a += 0.05f)
                    spriteBatch.Draw(Pixel, new Rectangle((int)(b.Center.X + MathF.Cos(a) * b.Radius), (int)(b.Center.Y + MathF.Sin(a) * b.Radius), 2, 2), Color.Yellow);
                float reachX = Feet.X + (_faceLeft ? -1 : 1) * 32 * Scale;
                var cone = Color.Red * (_state == State.Windup || _state == State.Swipe ? 1f : 0.35f);
                for (int h = 0; h < SwipeH; h += 2)  // cone outline
                {
                    float w = ConeHalf(h);
                    spriteBatch.Draw(Pixel, new Rectangle((int)(SwipeX - w), (int)(Feet.Y - h), 2, 2), cone);
                    spriteBatch.Draw(Pixel, new Rectangle((int)(SwipeX + w), (int)(Feet.Y - h), 2, 2), cone);
                }
                spriteBatch.Draw(Pixel, new Rectangle((int)(SwipeX - ConeHalf(SwipeH)), (int)(Feet.Y - SwipeH), (int)(ConeHalf(SwipeH) * 2), 2), cone);
                foreach (var (bx, age) in _beams) if (age > BeamWarn) Box(new Rectangle((int)bx - 12, 0, 24, (int)_floorY), Color.Cyan);
                foreach (var (p, _) in _fists) { var f = FistBox(p); Box(new Rectangle((int)f.X, (int)f.Y, (int)f.Width, (int)f.Height), Color.Orange); }
                foreach (var r in _rocks) if (r.Warn <= 0) Box(new Rectangle((int)(r.Pos.X - RockSize * 0.3f), (int)(r.Pos.Y - RockSize * 0.3f), (int)(RockSize * 0.6f), (int)(RockSize * 0.6f)), Color.White);
                foreach (var (x, _) in _waves) Box(new Rectangle((int)x - 22, (int)Floor(x) - 24, 44, 24), Color.Orange);
                if (LaserOn)
                    for (int k = 0; k < 120; k++)
                        spriteBatch.Draw(Pixel, new Rectangle((int)(Eye.X + MathF.Cos(_laserAngle) * k * 8), (int)(Eye.Y + MathF.Sin(_laserAngle) * k * 8), 3, 3), Color.Cyan);
            }

            if (_state == State.Windup)  // where the swing will land, a red patch that fills in as he winds up
            {
                float fill = MathHelper.Clamp(_t / (WindupTime * 0.7f), 0, 1);
                for (int h = 0; h < SwipeH; h += 4)  // the cone in thin strips
                {
                    float w = ConeHalf(h);
                    spriteBatch.Draw(Pixel, new Rectangle((int)(SwipeX - w), (int)(Feet.Y - h - 4), (int)(w * 2), 4), Color.Red * 0.12f * alpha);
                }
                spriteBatch.Draw(Pixel, new Rectangle((int)(SwipeX - SwipeHalfW), (int)Feet.Y - 6, (int)(SwipeHalfW * 2 * fill), 6), Color.Red * 0.6f * alpha);  // floor bar fills up, swing at full
            }
            foreach (var (bx, age) in _beams)
            {
                if (age < BeamWarn)  // thin blinking line where it's about to hit
                {
                    if ((int)(age * 14) % 2 == 0) spriteBatch.Draw(Pixel, new Rectangle((int)bx - 1, 0, 3, (int)_floorY), new Color(120, 220, 255) * 0.45f * alpha);
                    continue;
                }
                float on = (age - BeamWarn) / BeamOn, w = 24 * MathF.Sin(on * MathF.PI) + 4;  // swells then thins out
                spriteBatch.Draw(Pixel, new Rectangle((int)(bx - w / 2), 0, (int)w, (int)_floorY), new Color(80, 200, 255) * 0.85f * alpha);
                spriteBatch.Draw(Pixel, new Rectangle((int)(bx - w / 6), 0, (int)(w / 3) + 1, (int)_floorY), Color.White * alpha);
            }
            // Jump: red patch where he'll come down (same reach as the landing hurt), fills in as he drops
            if (_state == State.Jump && !_fired)
            {
                float fill = MathHelper.Clamp(_t / 0.85f, 0, 1);
                for (float x = _jumpTo - 140; x < _jumpTo + 140; x += 4)
                    spriteBatch.Draw(Pixel, new Rectangle((int)x, (int)Floor(x) - 6, 4, 6), Color.Red * (0.25f + 0.45f * fill) * alpha);
                if ((int)(_t * 10) % 2 == 0 && Font != null)
                    spriteBatch.DrawString(Font, "!", new Vector2(_jumpTo - 10, Floor(_jumpTo) - 90), Color.Red * alpha, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
            }
            // Pound: ! over his head and the whole floor lights up red before each slam (the shockwaves cover it all)
            if (_state == State.Pound)
            {
                float next = _t < SlamTimes[0] ? SlamTimes[0] : SlamTimes[1], until = next - _t;
                if (until > 0 && until < 0.55f && (Hp < MaxHp / 2 || next == SlamTimes[0]))
                {
                    if ((int)(_t * 10) % 2 == 0 && Font != null)
                        spriteBatch.DrawString(Font, "!", Drawn + new Vector2(-12, -62 * Scale), Color.Red * alpha, 0f, Vector2.Zero, 1.6f, SpriteEffects.None, 0f);
                    for (float x = 0; x < 854; x += 4)
                        spriteBatch.Draw(Pixel, new Rectangle((int)x, (int)Floor(x) - 5, 4, 5), Color.Red * (0.55f - until) * alpha);
                }
            }
            if (_state == State.Shoot && !_fired)
            {
                if ((int)(_t * 10) % 2 == 0 && Font != null)
                    spriteBatch.DrawString(Font, "!", Hand + new Vector2(-10, -70), Color.Orange * alpha, 0f, Vector2.Zero, 1.3f, SpriteEffects.None, 0f);
                var dir = _aim - Hand; float len = dir.Length(); dir /= MathF.Max(1, len);
                for (float d = 30; d < len; d += 18)
                    spriteBatch.Draw(Pixel, new Rectangle((int)(Hand.X + dir.X * d) - 2, (int)(Hand.Y + dir.Y * d) - 2, 4, 4), Color.Orange * 0.6f * MathF.Min(1, _t / ThrowAt) * alpha);
            }

            // ! warning over his head before a swipe, same blinking red as the wall warning
            if (_state == State.Windup && (int)(_t * 10) % 2 == 0 && Font != null)
                spriteBatch.DrawString(Font, "!", Drawn + new Vector2(-12, -62 * Scale), Color.Red * alpha, 0f, Vector2.Zero, 1.6f, SpriteEffects.None, 0f);

            // Laser, the charge orb then the beam (sheet frames 1-8 charge, 9-14 beam), right out of the eye at the sweep angle
            if (_state == State.Laser && _t < LaserCharge + LaserTime)
            {
                int f = _t < LaserCharge ? 1 + (int)(_t / LaserCharge * 8) : 9 + (int)((_t - LaserCharge) / 0.06f) % 6;
                var scale = _t < LaserCharge ? new Vector2(1.4f) : new Vector2(3f, 1.2f);  // round orb while charging, long beam once it fires
                spriteBatch.Draw(_laser, Eye, new Rectangle(0, Math.Min(14, f) * 100, 300, 100), Color.White * alpha, _laserAngle, new Vector2(53, 33), scale, SpriteEffects.None, 0f);  // orb center on the eye
            }
            foreach (var (x, dir) in _waves)  // same jagged bump as the Quake Hammer's, riding the hills
                for (int k = 0; k < 5; k++)
                {
                    float bx = x - dir * k * 14;
                    spriteBatch.Draw(Pixel, new Rectangle((int)bx - 6, (int)Floor(bx) - 22 + k * 4, 10, 22 - k * 4), new Color(190, 210, 255) * (0.5f - k * 0.09f) * alpha);
                }
            foreach (var (p, v) in _fists)
                spriteBatch.Draw(_fist, p, null, Color.White * alpha, MathF.Atan2(v.Y, v.X), new Vector2(77, 37), 3f, SpriteEffects.None, 0f);

            // Rocks: a blinking marker on the floor first, then the cave rock comes down
            foreach (var r in _rocks)
            {
                if (r.Warn > 0)
                {
                    if ((int)(r.Warn * 12) % 2 == 0) spriteBatch.Draw(Pixel, new Rectangle((int)r.Pos.X - 26, (int)Floor(r.Pos.X) - 5, 52, 5), r.Color * 0.9f * alpha);
                    continue;
                }
                spriteBatch.Draw(RockTexture, r.Pos, null, r.Color * alpha, r.Spin, new Vector2(RockTexture.Width / 2f, RockTexture.Height / 2f), RockSize / RockTexture.Width, SpriteEffects.None, 0f);
            }
        }
    }
}
