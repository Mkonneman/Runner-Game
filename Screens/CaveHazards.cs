using System;
using System.Collections.Generic;
using CollisionExercise.Collisions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game.Screens
{
    // Cave-only hazards: icicles in the blue cave, grabby vines and bouncy mushrooms in the green one
    public partial class GameplayScreen
    {
        private sealed class Icicle { public float X, Y = IceHangY, Wait = -1, Speed, Drop = RandomHelper.NextFloat(200, 260); }  // Wait counts up once it starts shaking, Drop = how close he gets before it lets go
        private sealed class Vine { public float X, T, Len; public int Phase; public Vector2 Aim; }  // phase 0 dangling, 1 winding up, 2 lunging, 3 holding him, 4 pulling back, 5 cut/done
        private readonly List<Icicle> _icicles = new();
        private sealed class Shard { public Vector2 Pos, Vel; public Rectangle Src; public float Rot, Spin, Life; }
        private readonly List<Shard> _shards = new();  // bits of a broken icicle, its own art cut up, faded
        private readonly List<Vine> _vines = new();
        private readonly List<(float X, float Squash)> _mushrooms = new();
        private Texture2D _icicleTexture, _vineTexture, _mushroomTexture;
        private float _iceTimer = 8f, _vineTimer = 2f, _mushTimer = 4f;
        private float _sinceHeld = 99f;  // seconds since a vine let go
        private bool _midBounce;         // launched by a mushroom and hasn't landed yet
        // Dying in a vine's grip (or right after) or mid mushroom launch gets their puns instead
        private DeathCause Blame(DeathCause cause) => cause == DeathCause.Door ? cause : _sinceHeld < 1f ? DeathCause.Vine : _midBounce ? DeathCause.Mushroom : cause;

        private const float IceHangY = -32f;       // rocky top tucked up into the roof
        private const float IceShakeTime = 0.35f;  // wobbles this long before it drops
        private const float IceGravity = 2000f;
        private const int IcePoints = 50, VinePoints = 50, BouncePoints = 25, MushroomPoints = 50;
        private const float VineWindUp = 0.35f, VineLungeSpeed = 650f, VineReach = 360f, VineGrab = 0.9f, VinePull = 320f;
        private const float VineTop = -38f;              // clump is half tucked up in the roof
        private const float VineAnchorY = VineTop + 124; // bottom of the vine clump, where the grabby one comes out
        private const float MushroomBounce = 1.1f, MushroomSquash = 0.4f;
        private static readonly Color IceColor = new(150, 220, 255), VineColor = new(70, 190, 60), MushroomColor = new(170, 90, 255);

        // Diamonds along the bounce path off a mushroom (x ahead of it, y on screen), so using it pays
        private static readonly (float, float)[] BounceArc = { (30, 213), (78, 110), (126, 95), (174, 169), (222, 332) };

        // ---- Spacing, so whatever shows up together there's always a way through ----
        // Floor = jump it, Ceiling = hangs down but he can jump under it, Low = hangs low enough to block a jump,
        // Wall = smash it, Air = a mushroom's bounce path, Grab = where a vine could be holding him
        private enum Lane { Floor, Ceiling, Low, Wall, Air, Grab }
        private const float VineHalf = 75f, GrabZone = 300f, MushroomHalf = 44f, BouncePath = 260f;
        private const float JumpClear = 155f;  // a hanging gem reaching lower than this hits his head at the top of a jump
        private bool _stackOk;                 // rolled after each spawn, lets the next one hang right over a floor spike (about 1 in 4)

        // Gap needed between two things (negative = they can overlap)
        private float Need(Lane a, Lane b)
        {
            bool Has(Lane lane) => a == lane || b == lane;
            if (Has(Lane.Air)) return Has(Lane.Ceiling) || Has(Lane.Low) ? 0 : Has(Lane.Wall) ? 60 : -999;  // nothing to bonk on the way up or fly into
            if (Has(Lane.Grab)) return Has(Lane.Wall) ? 0 : -999;  // no wall rolling up while he's stuck
            if (Has(Lane.Wall)) return 180;                         // room to land and swing
            if (Has(Lane.Floor)) return a == b ? 140 : Has(Lane.Ceiling) && _stackOk ? -999 : 60;  // land between floor stuff, sometimes a spike under something he can still jump under
            return 30;                                              // roof stuff just can't overlap
        }

        // Call after anything spawns
        private void Spawned() => _stackOk = RandomHelper.Next(0, 4) == 0;

        // Everything already out there as a strip along the floor
        private IEnumerable<(float L, float R, Lane Lane)> Placed()
        {
            foreach (var s in _spikes)
            {
                float half = (s.IsWall ? _wallTexture.Width : SpikeSource.Width) / 2f;
                yield return (s.X - half, s.X + half, s.IsWall ? Lane.Wall : Lane.Floor);
            }
            foreach (var g in _gems) yield return g.Top ? (g.X - GemHalf(g.Tip), g.X + GemHalf(g.Tip), GemLane(g.Tip)) : (g.X - 30, g.X + 30, Lane.Floor);
            foreach (var ice in _icicles) yield return (ice.X - 30, ice.X + 30, Lane.Ceiling);
            foreach (var v in _vines) { yield return (v.X - VineHalf, v.X + VineHalf, Lane.Ceiling); yield return (v.X - VineHalf, v.X + GrabZone, Lane.Grab); }
            foreach (var (x, _) in _mushrooms) { yield return (x - MushroomHalf, x + MushroomHalf, Lane.Floor); yield return (x - 40, x + BouncePath, Lane.Air); }
        }

        // Can something new go from left to right in this lane? (holeMargin = how far it stays off holes, negative skips that)
        private bool Fits(float left, float right, Lane lane, float holeMargin)
        {
            if (holeMargin >= 0 && OverGap((left + right) / 2, right - left + holeMargin * 2)) return false;
            foreach (var (l, r, other) in Placed())
                if (MathF.Max(left - r, l - right) < Need(lane, other)) return false;
            return true;
        }

        // How wide a hanging gem spike looks below the roof (the wide base is up in the rock)
        private float GemHalf(float tip) => tip / (1 - GemBuried) * _gemTexture.Width / _gemTexture.Height * 0.36f;
        private static Lane GemLane(float tip) => tip > JumpClear ? Lane.Low : Lane.Ceiling;

        private void LoadHazards()
        {
            _icicleTexture = _content.Load<Texture2D>("Images/Gameplay/icicle");
            _vineTexture = _content.Load<Texture2D>("Images/Gameplay/vines");
            _mushroomTexture = _content.Load<Texture2D>("Images/Gameplay/mushroom");
        }

        // The spikes under the rock chunk, not the rock
        private BoundingRectangle IceBounds(Icicle ice) => new(ice.X - 20, ice.Y + 22, 40, _icicleTexture.Height - 30);
        private static Vector2 Middle(BoundingRectangle box) => new(box.X + box.Width / 2, box.Y + box.Height / 2);

        private Vector2 Chest() => new(_runner.Bounds.X + _runner.Bounds.Width / 2, _runner.Bounds.Y + 40);
        private static Vector2 VineAnchor(Vine vine) => new(vine.X, VineAnchorY);
        private Vector2 VineTip(Vine vine) => vine.Phase == 3 ? Chest() : VineAnchor(vine) + SafeNormal(vine.Aim - VineAnchor(vine)) * vine.Len;
        private static Vector2 SafeNormal(Vector2 v) => v == Vector2.Zero ? Vector2.UnitY : Vector2.Normalize(v);
        private static bool TendrilOut(Vine vine) => vine.Phase < 5 && vine.Len > 2;
        private float DistToTendril(Vine vine, Vector2 p) => DistToSegment(p, VineAnchor(vine), VineTip(vine));

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab == Vector2.Zero ? 0 : MathHelper.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0, 1);
            return Vector2.Distance(p, a + ab * t);
        }

        private void UpdateHazards(float dt)
        {
            float width = ScreenManager.GraphicsDevice.Viewport.Width;
            int cave = CaveZones.Kind(CaveZones.At(_scrollTime).From);  // endless colors get their story cave's hazards
            var body = _runner.Bounds;
            float me = body.X + body.Width / 2;

            for (int s = _shards.Count - 1; s >= 0; s--)
            {
                var shard = _shards[s];
                shard.Vel.Y += 900 * dt;
                shard.Pos += (shard.Vel - new Vector2(SpikeSpeed, 0)) * dt;  // drifts back with the cave
                shard.Rot += shard.Spin * dt;
                if ((shard.Life -= dt * 1.4f) <= 0) _shards.RemoveAt(s);
            }

            // ---- Icicles, blue cave only ----
            _iceTimer -= dt;
            if (_iceTimer <= 0)
            {
                float x = width + 40;
                if (cave != 0) _iceTimer = 1f;
                else if (!Fits(x - 30, x + 30, Lane.Ceiling, -1)) _iceTimer = 0.3f;  // over holes is fine, it only drops when he's close
                else { _icicles.Add(new Icicle { X = x }); Spawned(); _iceTimer = RandomHelper.NextFloat(MathHelper.Lerp(5f, 3.5f, Difficulty * 3), MathHelper.Lerp(8f, 6f, Difficulty * 3)); }  // blue is the first third of the ramp
            }
            for (int i = _icicles.Count - 1; i >= 0; i--)
            {
                var ice = _icicles[i];
                ice.X -= SpikeSpeed * dt;
                // Starts shaking once a drop would land on him, then falls
                if (ice.Wait < 0 && ice.X - me < ice.Drop && ice.X > me - 40) { ice.Wait = 0; _crashSound.PlaySfx(0.25f, 0.9f, 0f); }  // tiny high crack
                var box = IceBounds(ice);
                if (ice.Wait >= 0)
                {
                    ice.Wait += dt;
                    if (ice.Wait > IceShakeTime)
                    {
                        ice.Speed += IceGravity * dt;
                        ice.Y += ice.Speed * dt;
                        _hoverDust.PlaceDust(new Vector2(ice.X + RandomHelper.NextFloat(-14, 14), box.Top + RandomHelper.NextFloat(0, box.Height)), IceColor);  // frost streaming off as it falls
                    }
                    else if (RandomHelper.Next(0, 3) == 0) _hoverDust.PlaceDust(new Vector2(ice.X + RandomHelper.NextFloat(-20, 20), ice.Y + 24), IceColor);  // frost sprinkles off it
                }
                else if (RandomHelper.Next(0, 20) == 0) _hoverDust.PlaceDust(new Vector2(ice.X + RandomHelper.NextFloat(-16, 16), box.Top + RandomHelper.NextFloat(10, box.Height)), Color.White);  // little twinkles while it hangs
                box = IceBounds(ice);
                if (_safeTime <= 0 && _hurtTime <= 0 && body.CollidesWith(box))
                {
                    if (Blocked()) { BreakIcicle(i, true); continue; }
                    Hurt(_armor == ItemId.HazardHat ? Shop.Value(ItemId.HazardHat) : RockDamage, IceColor, DeathCause.Icicle);
                    if (_isGameOver) return;
                    BreakIcicle(i, false);  // shatters on his head
                    continue;
                }
                if (box.Bottom >= SpikeBottom) { BreakIcicle(i, false); continue; }  // shatters on the floor
                if (ice.X < -60) _icicles.RemoveAt(i);
            }

            // ---- Vines, green cave, one grabby tendril each ----
            _vineTimer -= dt;
            if (_vineTimer <= 0)
            {
                float x = width + 80;
                if (cave != 2) _vineTimer = 1f;
                else if (!Fits(x - VineHalf, x + VineHalf, Lane.Ceiling, -1) || !Fits(x - VineHalf, x + GrabZone, Lane.Grab, -1) || OverGap(x - 100, 400)) _vineTimer = 0.3f;  // no wall coming while it holds him, no hole to drop him in
                else { _vines.Add(new Vine { X = x }); Spawned(); _vineTimer = RandomHelper.NextFloat(5f, 8f); }
            }
            for (int v = _vines.Count - 1; v >= 0; v--)
            {
                var vine = _vines[v];
                vine.X -= SpikeSpeed * dt;
                vine.T += dt;
                switch (vine.Phase)
                {
                    case 0:  // dangling, waits till he's close ahead of it
                        vine.Len = 14;
                        vine.Aim = VineAnchor(vine) + new Vector2(0, 100);
                        if (vine.X < width - 20 && vine.X - me < 230 && vine.X - me > -20) { vine.Phase = 1; vine.T = 0; }
                        break;
                    case 1:  // curls toward him (the tell), then locks on where he is
                        vine.Len = 14 + 10 * vine.T / VineWindUp;
                        vine.Aim = Chest();
                        if (vine.T > VineWindUp) { vine.Phase = 2; vine.T = 0; _swingSound.PlaySfx(0.5f, -0.4f, 0f); }  // whip
                        break;
                    case 2:  // shoots out at that spot, a lil past it
                        vine.Len += VineLungeSpeed * dt;
                        var tip = VineTip(vine);
                        if (_safeTime <= 0 && _runner.Held <= 0 && body.CollidesWith(new BoundingRectangle(tip.X - 6, tip.Y - 6, 12, 12)))
                        {
                            if (Blocked()) { BreakVine(v); break; }
                            // Got him, stuck in place for a bit (combo's gone), smash the vine to get out early
                            vine.Phase = 3; vine.T = 0;
                            _runner.Held = VineGrab;
                            RunStats.Hurt();
                            _crashSound.PlaySfx(0.4f, -0.5f, 0f);
                            Shake(4f, 0.2f);
                            Rumble.Play("vine_grab");
                        }
                        else if (vine.Len >= MathF.Min(Vector2.Distance(VineAnchor(vine), vine.Aim) + 40, VineReach)) { vine.Phase = 4; vine.T = 0; }  // missed
                        break;
                    case 3:  // reels him up and back under the clump till his time's up, then drops him
                        var feet = _runner.Position;
                        var step = new Vector2(vine.X - 30, VineAnchorY + 170) - feet;  // his feet spot with his chest just under the clump
                        if (step.Length() > VinePull * dt) step = Vector2.Normalize(step) * VinePull * dt;
                        _runner.Position = new Vector2(MathHelper.Clamp(feet.X + step.X, _runner.MinX, _runner.MaxX), feet.Y + step.Y);
                        vine.Len = Vector2.Distance(VineAnchor(vine), Chest());
                        if (_runner.Held <= 0 || vine.Len > VineReach + 120) { _runner.Held = 0; vine.Aim = Chest(); vine.Phase = 4; vine.T = 0; }
                        break;
                    case 4:  // pulls back up and goes limp
                        vine.Len = MathF.Max(0, vine.Len - 900 * dt);
                        if (vine.Len == 0) vine.Phase = 5;
                        break;
                }
                if (vine.X < -100)
                {
                    if (vine.Phase == 3) _runner.Held = 0;
                    _vines.RemoveAt(v);
                }
            }

            // ---- Mushrooms, green cave, land on the cap to get launched ----
            _sinceHeld = _runner.Held > 0 ? 0 : _sinceHeld + dt;
            if (_runner.IsOnGround) _midBounce = false;
            _mushTimer -= dt;
            if (_mushTimer <= 0)
            {
                float x = width + 60;
                if (cave != 2) _mushTimer = 1f;
                else if (!Fits(x - MushroomHalf, x + MushroomHalf, Lane.Floor, 60) || !Fits(x - 40, x + BouncePath, Lane.Air, -1)) _mushTimer = 0.3f;  // clear sky along the bounce
                else
                {
                    _mushrooms.Add((x, 0));
                    Spawned();
                    foreach (var (dx, y) in BounceArc) _diamonds.Add(new Vector2(x + dx, y));
                    _mushTimer = RandomHelper.NextFloat(6f, 10f);
                }
            }
            float capTop = SpikeBottom + 2 - _mushroomTexture.Height;
            for (int m = _mushrooms.Count - 1; m >= 0; m--)
            {
                var (x, squash) = _mushrooms[m];
                x -= SpikeSpeed * dt;
                squash = MathF.Max(0, squash - dt);
                if (_runner.VerticalSpeed > 0 && MathF.Abs(me - x) < 44 && body.Bottom > capTop - 4 && body.Bottom < capTop + 36)
                {
                    _runner.Bounce(MushroomBounce);
                    _midBounce = true;
                    squash = MushroomSquash;
                    Sfx.Play("double_jump", 0.5f, 0.05f);  // boing
                    _score += Points(BouncePoints);
                    for (int k = 0; k < 6; k++) _hoverDust.PlaceDust(new Vector2(x + RandomHelper.NextFloat(-40, 40), capTop + 8), MushroomColor);  // spores puff off the cap
                    Rumble.Play("mushroom");
                }
                _mushrooms[m] = (x, squash);
                if (x < -60) _mushrooms.RemoveAt(m);
            }
        }

        private void BreakIcicle(int i, bool smashed)
        {
            Rumble.Play("icicle");
            var center = Middle(IceBounds(_icicles[i]));
            _crash.PlaceCrash(center, IceColor);
            for (int k = 0; k < 8; k++) _hoverDust.PlaceDust(center + new Vector2(RandomHelper.NextFloat(-30, 30), RandomHelper.NextFloat(-30, 30)), Color.White);  // sparkly bits
            for (int k = 0; k < 4; k++) _rockDust.PlaceDust(center, IceColor * 0.7f);  // cold mist puff
            _crashSound.PlaySfx(0.7f, 0.6f, 0f);  // high and glassy
            Shake(smashed ? 6f : 3f, 0.2f);
            if (smashed) { _score += Points(IcePoints); Kick(center, 0.45f); Rumble.RockBreak(); }
            // Chop the icicle art into a few chunks that tumble off and fade
            for (int k = 0; k < 7; k++)
            {
                int size = RandomHelper.Next(8, 16);
                var src = new Rectangle(RandomHelper.Next(_icicleTexture.Width / 2 - 14, _icicleTexture.Width / 2 + 14 - size), RandomHelper.Next(10, _icicleTexture.Height - size), size, size);
                _shards.Add(new Shard { Pos = center + new Vector2(RandomHelper.NextFloat(-12, 12), RandomHelper.NextFloat(-20, 20)), Vel = new Vector2(RandomHelper.NextFloat(-160, 160), RandomHelper.NextFloat(-260, -60)), Src = src, Spin = RandomHelper.NextFloat(-9, 9), Life = 1f });
            }
            _icicles.RemoveAt(i);
        }

        // Cap and stem
        private BoundingRectangle MushroomBounds(float x) => new(x - 40, SpikeBottom + 2 - _mushroomTexture.Height, 80, _mushroomTexture.Height);

        private void BreakMushroom(int m)
        {
            var center = Middle(MushroomBounds(_mushrooms[m].X));
            _crash.PlaceCrash(center, MushroomColor);
            for (int k = 0; k < 10; k++) _hoverDust.PlaceDust(center + new Vector2(RandomHelper.NextFloat(-40, 40), RandomHelper.NextFloat(-40, 10)), MushroomColor);  // spore cloud
            _crashSound.PlaySfx(0.6f, 0.2f, 0f);
            Shake(5f, 0.2f);
            _score += Points(MushroomPoints);
            Kick(center, 0.4f);
            Rumble.RockBreak();
            _mushrooms.RemoveAt(m);
        }

        private void BreakVine(int v)
        {
            var vine = _vines[v];
            var a = VineAnchor(vine);
            var b = VineTip(vine);
            _crash.PlaceCrash(Vector2.Lerp(a, b, 0.6f), VineColor);
            for (int k = 0; k < 8; k++) _rockDust.PlaceDust(Vector2.Lerp(a, b, k / 7f), VineColor);  // leaf bits all along it
            _crashSound.PlaySfx(0.5f, 0.3f, 0f);
            Shake(4f, 0.2f);
            _score += Points(VinePoints);
            Kick(b, 0.3f);
            Rumble.RockBreak();
            if (vine.Phase == 3) _runner.Held = 0;  // cut loose
            vine.Phase = 5;
            vine.Len = 0;
        }

        // Smash swing: icicles and mushrooms near his head, any tendril passing through the reach
        private void SmashHazards()
        {
            var head = SmashPoint();
            bool InReach(BoundingRectangle box) => Vector2.Distance(new Vector2(MathHelper.Clamp(head.X, box.Left, box.Right), MathHelper.Clamp(head.Y, box.Top, box.Bottom)), head) < SmashReach();
            for (int i = _icicles.Count - 1; i >= 0; i--)
                if (InReach(IceBounds(_icicles[i]))) BreakIcicle(i, true);
            for (int m = _mushrooms.Count - 1; m >= 0; m--)
                if (InReach(MushroomBounds(_mushrooms[m].X))) BreakMushroom(m);
            for (int v = _vines.Count - 1; v >= 0; v--)
                if (TendrilOut(_vines[v]) && DistToTendril(_vines[v], head) < SmashReach() + 8) BreakVine(v);
        }

        // Quake shockwave along the floor gets mushrooms and icicles that are almost down
        private void QuakeHazards(float x)
        {
            for (int m = _mushrooms.Count - 1; m >= 0; m--)
                if (MathF.Abs(_mushrooms[m].X - x) < 40) BreakMushroom(m);
            for (int i = _icicles.Count - 1; i >= 0; i--)
                if (MathF.Abs(_icicles[i].X - x) < 24 && IceBounds(_icicles[i]).Bottom > FloorY - 120) BreakIcicle(i, true);
        }

        // Mirror Helm beam
        private void BreakHazardsAlong(Vector2 from, float angle)
        {
            var end = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 2000;
            for (int i = _icicles.Count - 1; i >= 0; i--)
            {
                var box = IceBounds(_icicles[i]);
                var center = Middle(box);
                if (center.X > from.X && DistToSegment(center, from, end) < box.Height / 2 + 14) BreakIcicle(i, true);
            }
            for (int m = _mushrooms.Count - 1; m >= 0; m--)
            {
                var center = Middle(MushroomBounds(_mushrooms[m].X));
                if (center.X > from.X && DistToSegment(center, from, end) < _mushroomTexture.Height / 2f + 14) BreakMushroom(m);
            }
            for (int v = _vines.Count - 1; v >= 0; v--)
            {
                if (!TendrilOut(_vines[v])) continue;
                var a = VineAnchor(_vines[v]);
                var b = VineTip(_vines[v]);
                for (int k = 0; k <= 8; k++)
                {
                    var p = Vector2.Lerp(a, b, k / 8f);
                    if (p.X > from.X && DistToSegment(p, from, end) < 14) { BreakVine(v); break; }
                }
            }
        }

        private bool RocketHitsHazard(Vector2 rocket)
        {
            int ice = _icicles.FindIndex(i => IceBounds(i).CollidesWith(new BoundingRectangle(rocket.X - 6, rocket.Y - 6, 12, 12)));
            if (ice >= 0) { BreakIcicle(ice, true); return true; }
            int mushroom = _mushrooms.FindIndex(m => MushroomBounds(m.X).CollidesWith(new BoundingRectangle(rocket.X - 6, rocket.Y - 6, 12, 12)));
            if (mushroom >= 0) { BreakMushroom(mushroom); return true; }
            int vine = _vines.FindIndex(v => TendrilOut(v) && DistToTendril(v, rocket) < 14);
            if (vine >= 0) { BreakVine(vine); return true; }
            return false;
        }

        // Drill chews straight through these, no cracking needed
        private void DrillHazards()
        {
            var band = DrillBand();
            for (int i = _icicles.Count - 1; i >= 0; i--)
                if (band.CollidesWith(IceBounds(_icicles[i]))) BreakIcicle(i, true);
            for (int m = _mushrooms.Count - 1; m >= 0; m--)
                if (band.CollidesWith(MushroomBounds(_mushrooms[m].X))) BreakMushroom(m);
            for (int v = _vines.Count - 1; v >= 0; v--)
                if (TendrilOut(_vines[v]) && DistToTendril(_vines[v], Middle(band)) < band.Width / 2) BreakVine(v);
        }

        // Hitboxes setting: icicles (cyan), the vine tip that grabs (green), mushroom bounce zone for his feet (magenta)
        private void DrawHazardHitboxes()
        {
            foreach (var ice in _icicles) DrawBox(IceBounds(ice), Color.Cyan * TransitionAlpha);
            foreach (var vine in _vines)
            {
                var tip = VineTip(vine);
                DrawBox(new BoundingRectangle(tip.X - 6, tip.Y - 6, 12, 12), (vine.Phase == 2 ? Color.Lime : Color.Lime * 0.35f) * TransitionAlpha);  // only grabs while lunging
            }
            float capTop = SpikeBottom + 2 - _mushroomTexture.Height;
            foreach (var (x, _) in _mushrooms) DrawBox(new BoundingRectangle(x - 44, capTop - 4, 88, 40), Color.Magenta * TransitionAlpha);
        }

        private void DrawHazards()
        {
            var sb = ScreenManager.SpriteBatch;
            foreach (var ice in _icicles)
            {
                float jitter = ice.Wait >= 0 && ice.Wait < IceShakeTime ? MathF.Sin(ice.Wait * 70) * 2.5f : 0;
                sb.Draw(_icicleTexture, new Vector2(ice.X - _icicleTexture.Width / 2f + jitter, ice.Y), Color.White * TransitionAlpha);
            }
            foreach (var shard in _shards)
                sb.Draw(_icicleTexture, shard.Pos, shard.Src, Color.White * 0.55f * shard.Life * TransitionAlpha, shard.Rot, new Vector2(shard.Src.Width / 2f), 1f, SpriteEffects.None, 0f);
            foreach (var vine in _vines)
            {
                float rustle = vine.Phase == 1 ? MathF.Sin(vine.T * 50) * 2f : 0;  // shivers right before it lunges
                sb.Draw(_vineTexture, new Vector2(vine.X - _vineTexture.Width / 2f + rustle, VineTop), Color.White * TransitionAlpha);
                if (TendrilOut(vine)) DrawTendril(vine);
            }
            foreach (var (x, squash) in _mushrooms)
            {
                // Squashes flat on a bounce and wobbles back
                float k = squash / MushroomSquash;
                float sy = 1 - 0.3f * k * MathF.Cos((1 - k) * 10f);
                sb.Draw(_mushroomTexture, new Vector2(x, SpikeBottom + 2), null, Color.White * TransitionAlpha, 0f, new Vector2(_mushroomTexture.Width / 2f, _mushroomTexture.Height), new Vector2(1 + (1 - sy) * 0.7f, sy), SpriteEffects.None, 0f);
            }
        }

        // Wiggly green line with a dark outline, little leaves and a hooked tip
        private void DrawTendril(Vine vine)
        {
            var a = VineAnchor(vine);
            var dir = VineTip(vine) - a;
            float len = dir.Length();
            if (len < 1) return;
            var side = new Vector2(-dir.Y, dir.X) / len;
            const int N = 14;
            float wiggle = vine.Phase == 1 ? 9f : vine.Phase == 3 ? 3f : 6f;  // tight when it's got him
            var pts = new Vector2[N + 1];
            for (int k = 0; k <= N; k++)
            {
                float t = k / (float)N;
                pts[k] = a + dir * t + side * MathF.Sin(t * 9f - _scrollTime * 14f) * wiggle * MathF.Sin(MathHelper.Pi * t);
            }
            var shades = new[] { (8f, new Color(10, 30, 12)), (5f, VineColor), (1.5f, new Color(160, 240, 120)) };
            foreach (var (thick, color) in shades)
                for (int k = 0; k < N; k++)
                    Segment(pts[k], pts[k + 1], thick * (1 - 0.45f * k / N), color * TransitionAlpha);  // thinner toward the tip
            for (int k = 2; k < N; k += 3)
                Segment(pts[k], pts[k] + (k % 2 == 0 ? side : -side) * 7 + (pts[k + 1] - pts[k]) * 0.3f, 4f, new Color(50, 150, 45) * TransitionAlpha);
            var tipDir = SafeNormal(pts[N] - pts[N - 1]);
            Segment(pts[N], pts[N] + tipDir * 6 + new Vector2(-tipDir.Y, tipDir.X) * 5, 3f, VineColor * TransitionAlpha);
        }

        private void Segment(Vector2 from, Vector2 to, float thick, Color color)
        {
            var d = to - from;
            ScreenManager.SpriteBatch.Draw(_pixel, from, null, color, MathF.Atan2(d.Y, d.X), new Vector2(0, 0.5f), new Vector2(d.Length() + thick * 0.5f, thick), SpriteEffects.None, 0f);
        }

        // Added light: cold glow in the icicles, the mushroom caps pulse, tendril tip glows while it lunges
        private void DrawHazardGlow()
        {
            var sb = ScreenManager.SpriteBatch;
            foreach (var ice in _icicles)
                sb.Draw(_glowTexture, Middle(IceBounds(ice)), null, IceColor * 0.25f * TransitionAlpha, 0f, new Vector2(8), 5f, SpriteEffects.None, 0f);
            foreach (var (x, _) in _mushrooms)
                sb.Draw(_glowTexture, new Vector2(x, SpikeBottom + 2 - _mushroomTexture.Height * 0.7f), null, MushroomColor * (0.3f + 0.1f * MathF.Sin(_scrollTime * 3 + x * 0.01f)) * TransitionAlpha, 0f, new Vector2(8), new Vector2(7f, 4f), SpriteEffects.None, 0f);
            foreach (var vine in _vines)
                if (vine.Phase is 1 or 2)
                    sb.Draw(_glowTexture, VineTip(vine), null, VineColor * 0.5f * TransitionAlpha, 0f, new Vector2(8), 2f, SpriteEffects.None, 0f);
        }
    }
}
