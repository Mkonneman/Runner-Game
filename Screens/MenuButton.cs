using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game.Screens
{
    /// <summary>
    /// Draws the stone button art at any size, the corners stay the same and only the middle stretches (9 slice)
    /// </summary>
    public static class MenuButton
    {
        private const int Border = 10;  // how many pixels of the art are the stone frame

        // Same look the title buttons use, bright when picked and a lil dim otherwise
        public static Color Tint(bool selected) => selected ? Color.White : new Color(170, 180, 200);

        public static void Draw(SpriteBatch spriteBatch, Texture2D texture, Rectangle dest, Color color)
        {
            int b = Border, w = texture.Width, h = texture.Height;
            int bx = System.Math.Min(b, dest.Width / 2), by = System.Math.Min(b, dest.Height / 2);  // tiny buttons just squish the corners
            int[] sx = { 0, b, w - b, w }, dx = { dest.Left, dest.Left + bx, dest.Right - bx, dest.Right };
            int[] sy = { 0, b, h - b, h }, dy = { dest.Top, dest.Top + by, dest.Bottom - by, dest.Bottom };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    spriteBatch.Draw(texture,
                        new Rectangle(dx[col], dy[row], dx[col + 1] - dx[col], dy[row + 1] - dy[row]),
                        new Rectangle(sx[col], sy[row], sx[col + 1] - sx[col], sy[row + 1] - sy[row]),
                        color);
        }
    }
}
