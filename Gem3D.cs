using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Runner_Game
{
    /// <summary>
    /// A real 3D diamond (CIS 580 Basic 3D Rendering): a brilliant cut gem built by hand from triangles, stored in a
    /// vertex buffer, lit and drawn with BasicEffect. Each diamond pickup in the caves is one of these spinning in place
    /// </summary>
    public class Gem3D
    {
        private readonly GraphicsDevice _graphics;
        private readonly BasicEffect _effect;
        private readonly VertexBuffer _vertices;
        private readonly int _triangles;

        // Gem shape (in "gem units", scaled up to pixels when drawn): a flat table on top, a ring of crown facets, a pointy pavilion below
        private const int Sides = 8;
        private const float TableRadius = 0.55f, TableY = 0.45f, GirdleRadius = 1f, GirdleY = 0.1f, PointY = -1.1f;

        public Gem3D(GraphicsDevice graphics)
        {
            _graphics = graphics;

            // 1. Corner points: the table ring, the girdle ring (widest part), and the two tips
            var table = new Vector3[Sides];
            var girdle = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * MathHelper.TwoPi / Sides, b = a + MathHelper.Pi / Sides;  // table ring is turned half a step so facets zigzag
                table[i] = new Vector3(MathF.Cos(b) * TableRadius, TableY, MathF.Sin(b) * TableRadius);
                girdle[i] = new Vector3(MathF.Cos(a) * GirdleRadius, GirdleY, MathF.Sin(a) * GirdleRadius);
            }
            var top = new Vector3(0, TableY, 0);
            var point = new Vector3(0, PointY, 0);

            // 2. Triangles. Every triangle gets its own 3 vertices with the face's normal, so each facet is lit flat (that's what makes it sparkle)
            var verts = new List<VertexPositionNormalTexture>();
            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                var n = Vector3.Normalize(Vector3.Cross(b - a, c - a));
                if (Vector3.Dot(n, (a + b + c) / 3) < 0) { (b, c) = (c, b); n = -n; }  // make sure it faces outward
                verts.Add(new VertexPositionNormalTexture(a, n, Vector2.Zero));
                verts.Add(new VertexPositionNormalTexture(b, n, Vector2.Zero));
                verts.Add(new VertexPositionNormalTexture(c, n, Vector2.Zero));
            }
            for (int i = 0; i < Sides; i++)
            {
                int j = (i + 1) % Sides;
                Tri(top, table[i], table[j]);              // flat table on top
                Tri(table[i], girdle[j], table[j]);        // crown facets between the table and girdle
                Tri(table[i], girdle[i], girdle[j]);
                Tri(girdle[i], point, girdle[j]);          // pavilion down to the point
            }

            // 3. Into a vertex buffer on the GPU
            _vertices = new VertexBuffer(graphics, typeof(VertexPositionNormalTexture), verts.Count, BufferUsage.WriteOnly);
            _vertices.SetData(verts.ToArray());
            _triangles = verts.Count / 3;

            // 4. BasicEffect with its built-in 3 light setup plus a strong shine, so facets flash as they turn toward the light
            _effect = new BasicEffect(graphics) { DiffuseColor = new Vector3(0.45f, 0.85f, 1f), SpecularColor = Vector3.One, SpecularPower = 12, EmissiveColor = new Vector3(0.1f, 0.25f, 0.35f) };
            _effect.EnableDefaultLighting();

            // 5. Camera: orthographic and matched to the screen in pixels, so a gem drawn at (x, y) lands right where the 2D one was
            _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, Game1.VirtualWidth, Game1.VirtualHeight, 0, -200, 200);
        }

        /// <summary>
        /// Draws one gem at a screen spot. size = pixels from the center to the girdle, spin = radians around its up axis
        /// shake is the screen shake matrix so the gems shake with everything else
        /// </summary>
        public void Draw(Vector2 at, float size, float spin, Matrix shake, float alpha)
        {
            _graphics.DepthStencilState = DepthStencilState.Default;
            _graphics.RasterizerState = RasterizerState.CullNone;  // the y-down screen camera flips the winding, depth testing hides the back facets anyway
            _graphics.BlendState = BlendState.AlphaBlend;

            // World: spin around its own axis, tip it toward the camera a bit so you see the table, flip y (screen y goes down), size it, place it
            _effect.World = Matrix.CreateRotationY(spin) * Matrix.CreateRotationX(0.45f) * Matrix.CreateScale(size, -size, size) * Matrix.CreateTranslation(at.X, at.Y, 0) * shake;
            _effect.Alpha = alpha;
            _graphics.SetVertexBuffer(_vertices);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _graphics.DrawPrimitives(PrimitiveType.TriangleList, 0, _triangles);
            }
        }
    }
}
