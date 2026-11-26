using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game.Screens
{
    // Between caves a huge rock wall whips past right in front of the camera,
    // the next cave swaps in while it covers the whole screen (no slow color fade)
    public partial class GameplayScreen
    {
        private const float SweepSpeed = 1500f, SweepWidth = 1150f;  // px per world second, wide enough to fully cover the screen for a moment
        private int _sweptAt;  // which cave change already got its whoosh

        // Pillar center minus screen center for the nearest cave change, NaN if there isn't one (story run ends at the door after green)
        private float SweepOffset()
        {
            int next = (int)MathF.Round(_scrollTime / CaveZones.ZoneLength);
            if (next < 1 || (!CaveZones.Endless && next >= CaveZones.Count)) return float.NaN;
            return (next * CaveZones.ZoneLength - _scrollTime) * SweepSpeed;
        }

        // Quiet stretch around a cave change: nothing spawns from 5s before the wall until 1.5s after,
        // so everything already out has scrolled past him before the wall hides the screen
        private bool SweepQuiet
        {
            get { float off = SweepOffset(); return !float.IsNaN(off) && off < 5f * SweepSpeed && off > -1.5f * SweepSpeed; }
        }

        // No holes in the floor that's on screen while the wall goes by (world x of the floor)
        private bool NearSweep(float worldX)
        {
            float zonePx = CaveZones.ZoneLength * SpikeSpeed;
            int k = (int)MathF.Round(worldX / zonePx);
            if (k < 1 || (!CaveZones.Endless && k >= CaveZones.Count)) return false;
            float at = k * zonePx;  // FloorScroll right when the wall covers the screen
            return worldX > at - 500 && worldX < at + 854 + 600;
        }

        private void UpdateSweep()
        {
            float off = SweepOffset();
            int next = (int)MathF.Round(_scrollTime / CaveZones.ZoneLength);
            if (off > 0 && off < 1500 && _sweptAt != next)
            {
                _sweptAt = next;
                _swingSound.PlaySfx(0.9f, -1f, 0f);  // deep whoosh as it rushes in
                Shake(3f, 0.5f);
            }
        }

        // Dark stone columns side by side with lit edges, drawn over the cave but under the HUD
        private void DrawCaveSweep()
        {
            float off = SweepOffset();
            var viewport = ScreenManager.GraphicsDevice.Viewport;
            if (float.IsNaN(off) || MathF.Abs(off) > viewport.Width / 2f + SweepWidth / 2f + 80) return;
            var sb = ScreenManager.SpriteBatch;
            float left = viewport.Width / 2f + off - SweepWidth / 2f;
            float colW = _wallTexture.Width * 1.25f, scaleY = (viewport.Height + 40) / (float)_wallTexture.Height;
            int cols = (int)MathF.Ceiling(SweepWidth / (colW * 0.8f));

            sb.Begin(samplerState: SamplerState.PointClamp);
            sb.Draw(_pixel, new Rectangle((int)(left + colW * 0.4f), 0, (int)(SweepWidth - colW * 0.8f), viewport.Height), new Color(6, 8, 14) * TransitionAlpha);  // solid core so nothing peeks through
            for (int i = 0; i < cols; i++)
            {
                float edge = MathF.Min(i, cols - 1 - i);  // 0 on the outside columns
                var tint = edge == 0 ? new Color(70, 80, 105) : edge == 1 ? new Color(38, 44, 60) : new Color(20, 24, 34);  // lit rim, then dark
                float bob = (i * 37 % 5) * 9 - 18;  // uneven tops and bottoms so it reads as rock, not bricks
                sb.Draw(_wallTexture, new Vector2(left + i * colW * 0.8f, -20 + bob), null, tint * TransitionAlpha, 0f, Vector2.Zero, new Vector2(1.25f, scaleY), i % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0f);
            }
            sb.End();
        }
    }
}
