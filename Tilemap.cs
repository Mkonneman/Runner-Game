using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// A grid of tiles read from a text file (each number picks a square out of the tileset image, 0 is empty)
    /// </summary>
    public class Tilemap
    {
        public const int TileSize = 32;

        private Texture2D _tileset;

        // Swappable so the cave zones can draw the floor in a different color
        public Texture2D Tileset { get => _tileset; set => _tileset = value; }
        private readonly int[,] _tiles;

        public int Columns => _tiles.GetLength(1);
        public int Rows => _tiles.GetLength(0);
        public int Width => Columns * TileSize;

        /// <summary>
        /// Loads the tileset texture and the map file (lines starting with # are comments)
        /// </summary>
        public Tilemap(ContentManager content, string tilesetName, string mapPath)
        {
            _tileset = content.Load<Texture2D>(tilesetName);

            // The map file is copied next to the game, not built by the content pipeline
            using var stream = TitleContainer.OpenStream(Path.Combine(content.RootDirectory, mapPath));
            using var reader = new StreamReader(stream);
            var lines = new List<int[]>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                lines.Add(line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray());
            }

            _tiles = new int[lines.Count, lines.Max(l => l.Length)];
            for (int r = 0; r < lines.Count; r++)
                for (int c = 0; c < lines[r].Length; c++)
                    _tiles[r, c] = lines[r][c];
        }

        /// <summary>
        /// Draws one row of the map scrolled by scroll pixels, looping back to the start when it runs out
        /// </summary>
        /// pick can swap a tile based on how far along the loop it is (column counts up forever, it doesnt wrap)
        public void DrawLooping(SpriteBatch spriteBatch, int row, float scroll, float y, Color color, int screenWidth, Func<int, int, int> pick = null)
        {
            int first = (int)MathF.Floor(scroll / TileSize);
            float offset = scroll - first * TileSize;
            for (int i = -1; i * TileSize - offset < screenWidth + TileSize; i++)  // one extra each side so shake never shows an edge
            {
                int id = _tiles[row, Wrap(first + i)];
                if (pick != null) id = pick(first + i, id);
                if (id == 0) continue;
                spriteBatch.Draw(_tileset, new Vector2(MathF.Round(i * TileSize - offset), y), new Rectangle(id * TileSize, 0, TileSize, TileSize), color);
            }
        }

        /// <summary>
        /// The tile under a spot on screen when the row is scrolled like DrawLooping
        /// </summary>
        public int TileAtLooping(int row, float scroll, float screenX, Func<int, int, int> pick = null)
        {
            int column = (int)MathF.Floor((scroll + screenX) / TileSize);
            int id = _tiles[row, Wrap(column)];
            return pick != null ? pick(column, id) : id;
        }

        private int Wrap(int column) => ((column % Columns) + Columns) % Columns;

        /// <summary>
        /// Draws every tile with the top left of the map at position (only the ones on screen)
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, Vector2 position, Color color, int screenWidth)
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    int id = _tiles[r, c];
                    float x = position.X + c * TileSize;
                    if (id == 0 || x < -TileSize || x > screenWidth) continue;
                    var source = new Rectangle(id * TileSize, 0, TileSize, TileSize);
                    spriteBatch.Draw(_tileset, new Vector2(x, position.Y + r * TileSize), source, color);
                }
            }
        }
    }
}
