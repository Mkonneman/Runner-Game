using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// The shop merchant as a real 3D model (CIS 580 "Basic 3D Rendering" style): his 2D sprite gets turned into
    /// chunky voxels, thicker in the middle so he's rounded, built into a vertex + index buffer and drawn with BasicEffect
    /// using world / view / projection matrices. He slowly turns side to side so you can see he's 3D
    /// </summary>
    public class Merchant3D
    {
        private const int Voxel = 4;         // one cube per 4x4 pixels of the sprite
        private const int MaxDepth = 4;      // cubes stacked front to back in his thickest spot

        private readonly GraphicsDevice _graphics;
        private readonly BasicEffect _effect;
        private readonly VertexBuffer _vertices;
        private readonly IndexBuffer _indices;
        private readonly int _triangles;
        private readonly int _width, _height;  // in voxels

        // Camera: straight on, far enough back that 1 world unit = 1 voxel = 5 screen pixels at the model
        private readonly Matrix _view, _projection;
        public Matrix World { get; private set; }

        public Merchant3D(GraphicsDevice graphics, Texture2D sprite)
        {
            _graphics = graphics;

            // 1. Read the sprite's pixels and shrink them into a voxel grid (a cell is solid if most of its pixels are)
            var pixels = new Color[sprite.Width * sprite.Height];
            sprite.GetData(pixels);
            _width = sprite.Width / Voxel; _height = sprite.Height / Voxel;
            var color = new Color?[_width, _height];
            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    int solid = 0; Vector3 sum = Vector3.Zero;
                    for (int px = 0; px < Voxel; px++)
                        for (int py = 0; py < Voxel; py++)
                        {
                            var c = pixels[(y * Voxel + py) * sprite.Width + x * Voxel + px];
                            if (c.A > 128) { solid++; sum += c.ToVector3(); }
                        }
                    if (solid > Voxel * Voxel / 2) color[x, y] = new Color(sum / solid);
                }

            // 2. How thick each column is, more the further it is from an edge so he comes out rounded instead of a flat cutout
            var depth = new int[_width, _height];
            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    if (color[x, y] == null) continue;
                    int d = 0;
                    while (d < MaxDepth && Solid(color, x - d - 1, y) && Solid(color, x + d + 1, y) && Solid(color, x, y - d - 1) && Solid(color, x, y + d + 1)) d++;
                    depth[x, y] = d;
                }
            bool Filled(int x, int y, int z) => x >= 0 && y >= 0 && x < _width && y < _height && color[x, y] != null && Math.Abs(z) <= depth[x, y];

            // 3. Build the mesh, only faces that touch empty space (a face between two cubes can never be seen)
            //    Light is baked into the colors per face direction: top bright, sides a bit darker, bottom darkest
            var verts = new List<VertexPositionColor>();
            var idx = new List<short>();
            var faces = new (Vector3 Normal, float Shade)[]
            {
                (Vector3.Backward, 1.0f), (Vector3.Forward, 0.7f), (Vector3.Up, 1.15f), (Vector3.Down, 0.55f), (Vector3.Left, 0.8f), (Vector3.Right, 0.85f),
            };
            for (int x = 0; x < _width; x++)
                for (int y = 0; y < _height; y++)
                {
                    if (color[x, y] is not Color c) continue;
                    for (int z = -depth[x, y]; z <= depth[x, y]; z++)
                        foreach (var (n, shade) in faces)
                        {
                            // Sprite y goes down, world y goes up, so flip it
                            if (Filled(x + (int)n.X, y - (int)n.Y, z + (int)n.Z)) continue;
                            var center = new Vector3(x - _width / 2f + 0.5f, _height - y - 0.5f, z);
                            AddFace(verts, idx, center, n, new Color(c.ToVector3() * shade));
                        }
                }

            // 4. Hand the mesh to the GPU in buffers (16-bit indices need under 65,536 vertices)
            if (verts.Count > short.MaxValue * 2) throw new InvalidOperationException("Merchant mesh too big, raise Voxel");
            _vertices = new VertexBuffer(graphics, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
            _vertices.SetData(verts.ToArray());
            _indices = new IndexBuffer(graphics, IndexElementSize.SixteenBits, idx.Count, BufferUsage.WriteOnly);
            _indices.SetData(idx.ToArray());
            _triangles = idx.Count / 3;

            // 5. BasicEffect is the built-in shader, it just needs the three matrices and to use our vertex colors
            _effect = new BasicEffect(graphics) { VertexColorEnabled = true };
            float distance = (Game1.VirtualHeight / 2f / Voxel) / MathF.Tan(MathHelper.PiOver4 / 2);  // so the screen is 96 units tall at z = 0
            _view = Matrix.CreateLookAt(new Vector3(0, 0, distance), Vector3.Zero, Vector3.Up);
            _projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Game1.VirtualWidth / (float)Game1.VirtualHeight, 1f, distance * 3);
        }

        private static bool Solid(Color?[,] grid, int x, int y) => x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1) && grid[x, y] != null;

        // One square face of a cube: 4 corners, 2 triangles, wound so it faces out along the normal
        private static void AddFace(List<VertexPositionColor> verts, List<short> idx, Vector3 center, Vector3 normal, Color color)
        {
            var side = MathF.Abs(normal.Y) > 0 ? Vector3.Right : Vector3.Up;
            var up = Vector3.Cross(normal, side);
            var right = Vector3.Cross(up, normal);
            var mid = center + normal * 0.5f;
            short start = (short)verts.Count;
            verts.Add(new VertexPositionColor(mid + (-right - up) * 0.5f, color));
            verts.Add(new VertexPositionColor(mid + (right - up) * 0.5f, color));
            verts.Add(new VertexPositionColor(mid + (right + up) * 0.5f, color));
            verts.Add(new VertexPositionColor(mid + (-right + up) * 0.5f, color));
            idx.AddRange(new short[] { start, (short)(start + 2), (short)(start + 1), start, (short)(start + 3), (short)(start + 2) });
        }

        /// <summary>
        /// Moves him for this frame. feet = where his feet go on screen (pixels), turn = radians around his up axis, breath = vertical stretch
        /// </summary>
        public void Update(Vector2 feet, float turn, float breath)
        {
            // Screen pixels to world units at z = 0 (screen middle is the origin, y flips)
            var at = new Vector3((feet.X - Game1.VirtualWidth / 2f) / Voxel, (Game1.VirtualHeight / 2f - feet.Y) / Voxel, 0);
            World = Matrix.CreateScale(1, breath, 1) * Matrix.CreateRotationY(turn) * Matrix.CreateTranslation(at);
        }

        public void Draw(float alpha)
        {
            // SpriteBatch leaves 3D-unfriendly states behind, so set depth testing and back face culling back on
            _graphics.DepthStencilState = DepthStencilState.Default;
            _graphics.RasterizerState = RasterizerState.CullCounterClockwise;
            _graphics.BlendState = alpha < 1 ? BlendState.AlphaBlend : BlendState.Opaque;

            _effect.World = World; _effect.View = _view; _effect.Projection = _projection;
            _effect.Alpha = alpha;
            _graphics.SetVertexBuffer(_vertices);
            _graphics.Indices = _indices;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphics.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _triangles);
            }
        }

        /// <summary>
        /// Where a pixel of the original sprite ends up on screen now (for his glowing eyes), using Viewport.Project
        /// </summary>
        public Vector2 ScreenPoint(Vector2 spritePixel)
        {
            var local = new Vector3(spritePixel.X / Voxel - _width / 2f, _height - spritePixel.Y / Voxel, MaxDepth + 0.6f);  // on his front surface
            var p = _graphics.Viewport.Project(local, _projection, _view, World);
            return new Vector2(p.X, p.Y);
        }
    }
}
