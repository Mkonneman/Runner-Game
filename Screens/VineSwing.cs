using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game.Screens
{
    // A huge pit (every other loop of the floor map) with a vine hanging over it, too far to jump
    // Jump toward it and press Jump again near the end of the vine to grab on, he swings across and lets go on the far side
    // Nothing else spawns while it's coming up so the swing gets the whole screen
    public partial class GameplayScreen
    {
        private int _swingCol = -1, _swingCols;          // where the pit sits in the floor map (13 edge, 12s, 14 edge)
        private bool _swinging;
        private float _swingT, _swingFrom, _swingAngle;
        private float _swingDoneAt = float.NaN;          // pit already crossed (its world x), so it can't be grabbed twice
        private const float SwingTop = 8f, SwingLen = 282f;    // anchor tucked deep in the vine clump, rope length
        private const float SwingRest = -0.7f, SwingEnd = 0.9f, SwingTime = 0.9f, GrabRange = 105f;

        // Finds the pit in the map once the floor is loaded
        private void FindSwingPit()
        {
            int cols = _floor.Width / Tilemap.TileSize;
            for (int c = 0; c < cols && _swingCol < 0; c++)
                if (_floor.TileAtLooping(0, 0, c * Tilemap.TileSize) == 13) _swingCol = c;
            for (int c = _swingCol; _swingCol >= 0 && c < cols; c++)
                if (_floor.TileAtLooping(0, 0, c * Tilemap.TileSize) == 14) { _swingCols = c - _swingCol + 1; break; }
        }

        // Pit only opens on odd loops of the map, otherwise its tiles are plain floor
        private bool SwingLoop(int column) => Settings.OnlySpawn switch
        {
            Settings.Obstacle.Off => column / (_floor.Width / Tilemap.TileSize) % 2 == 1,
            Settings.Obstacle.VineSwing => true,  // testing, every loop
            _ => false,
        };

        // World x of the next open swing pit that isn't behind him yet
        private (float From, float To)? NextSwingPit()
        {
            if (_swingCol < 0) return null;
            int cols = _floor.Width / Tilemap.TileSize;
            int loop = (int)MathF.Floor(FloorScroll / _floor.Width) - 1;
            for (int k = Math.Max(0, loop); k <= loop + 2; k++)
            {
                int c0 = k * cols + _swingCol;
                float from = c0 * Tilemap.TileSize, to = from + _swingCols * Tilemap.TileSize;
                if (to < FloorScroll - 100) continue;
                if (FloorTile(c0 + _swingCols / 2, 12) != GapTile) continue;  // filled in this loop (or near the start / the door)
                return (from, to);
            }
            return null;
        }

        private static Vector2 Rope(float angle) => new Vector2(MathF.Sin(angle), MathF.Cos(angle)) * SwingLen;
        private Vector2 SwingAnchor((float From, float To) pit) => new((pit.From + pit.To) / 2 - FloorScroll, SwingTop);

        private void UpdateSwing(float dt)
        {
            _swingHold = false;
            if (NextSwingPit() is not { } pit) { _swinging = false; return; }
            float width = ScreenManager.GraphicsDevice.Viewport.Width;
            float left = pit.From - FloorScroll, right = pit.To - FloorScroll;

            // Clear runway: nothing new shows up from a bit before the pit until it's behind him
            if (_endPhase == 0 && left < width + 650 && right > -100)
            {
                _swingHold = true;
                _spawnTimer = 0;
                _spikeTimer = MathF.Max(_spikeTimer, 0.5f); _laserTimer = MathF.Max(_laserTimer, 0.5f); _gemTimer = MathF.Max(_gemTimer, 0.5f); _diamondTimer = MathF.Max(_diamondTimer, 0.5f);
                _iceTimer = MathF.Max(_iceTimer, 0.5f); _vineTimer = MathF.Max(_vineTimer, 0.5f); _mushTimer = MathF.Max(_mushTimer, 0.5f);
            }

            var anchor = SwingAnchor(pit);
            var body = _runner.Bounds;
            var center = new Vector2(body.X + body.Width / 2, body.Y + body.Height / 2);
            if (!_swinging)
            {
                _swingAngle = SwingRest + 0.1f * MathF.Sin(_scrollTime * 2.5f);  // sways a little, leaning toward him
                if (_swingDoneAt == pit.From || _falling || _isGameOver) return;
                if (Controls.IsPressed(PlayerAction.Jump) && Vector2.Distance(center, anchor + Rope(_swingAngle)) < GrabRange)
                {
                    _swinging = true; _swingT = 0;
                    var rel = center - anchor;
                    _swingFrom = MathHelper.Clamp(MathF.Atan2(rel.X, rel.Y), -1.1f, 0.2f);  // starts from wherever he caught it
                    _runner.Held = 99;  // the vine moves him now
                    Sfx.Play("double_jump", 0.5f, -0.3f);
                    Rumble.Play("vine_grab");
                }
                return;
            }

            // Fastest at the bottom, slows at the ends like a real swing
            _swingT += dt;
            float k = MathHelper.Clamp(_swingT / SwingTime, 0, 1);
            k = k * k * (3 - 2 * k);
            _swingAngle = MathHelper.Lerp(_swingFrom, SwingEnd, k);
            _runner.Position = anchor + Rope(_swingAngle) + new Vector2(0, body.Height - 6);  // hanging by his hands
            if (k >= 1 || (_swingAngle > 0.3f && Controls.IsPressed(PlayerAction.Jump)))  // lets go at the end, or early if you jump off
            {
                _swinging = false;
                _swingDoneAt = pit.From;
                _runner.Held = 0;
                _runner.Bounce(0.55f);
                _score += Points(BouncePoints * 2);
                Sfx.Play("jump", 0.3f);
            }
        }

        // The vine clump in the roof and the long vine hanging from it, plus the "press jump" nudge
        private void DrawSwing()
        {
            if (NextSwingPit() is not { } pit) return;
            var sb = ScreenManager.SpriteBatch;
            var anchor = SwingAnchor(pit);
            var tip = anchor + Rope(_swingAngle);
            if (anchor.X < -300 || anchor.X > ScreenManager.GraphicsDevice.Viewport.Width + 300) return;

            var dir = tip - anchor;
            var side = new Vector2(-dir.Y, dir.X) / dir.Length();
            const int N = 18;
            var pts = new Vector2[N + 1];
            for (int i = 0; i <= N; i++)
            {
                float t = i / (float)N;
                pts[i] = anchor + dir * t + side * MathF.Sin(t * MathHelper.Pi) * (_swinging ? 6f : 10f) * MathF.Sin(_scrollTime * 3f + t * 4f);  // bows a little as it sways
            }
            foreach (var (thick, color) in new[] { (8f, new Color(10, 30, 12)), (5f, VineColor), (1.5f, new Color(160, 240, 120)) })
                for (int i = 0; i < N; i++) Segment(pts[i], pts[i + 1], thick, color * TransitionAlpha);
            for (int i = 2; i < N; i += 3)  // leaves
                Segment(pts[i], pts[i] + (i % 2 == 0 ? side : -side) * 8 + (pts[i + 1] - pts[i]) * 0.3f, 4f, new Color(50, 150, 45) * TransitionAlpha);
            sb.Draw(_vineTexture, new Vector2(anchor.X - _vineTexture.Width / 2f, VineTop), Color.White * TransitionAlpha);  // clump hides the top of the rope

            // A loop at the end to grab, and a nudge while he's close enough to go for it
            if (!_swinging) Segment(tip - new Vector2(7, 0), tip + new Vector2(7, 0), 5f, new Color(160, 240, 120) * TransitionAlpha);
            var body = _runner.Bounds;
            var center = new Vector2(body.X + body.Width / 2, body.Y + body.Height / 2);
            if (!_swinging && _swingDoneAt != pit.From && !_falling && MathF.Abs(center.X - tip.X) < 320)
            {
                bool inReach = Vector2.Distance(center, tip) < GrabRange;
                Hud.KeyLine(sb, ScreenManager.MenuFont, "", Controls.Label(PlayerAction.Jump), "to grab!", new Vector2(tip.X, tip.Y + 18), 0.4f, inReach ? Color.Yellow : Color.White, TransitionAlpha);
            }
            if (Settings.ShowHitboxes) DrawDottedCircle(tip, GrabRange, Color.Lime * TransitionAlpha);  // grab reach
        }
    }
}
