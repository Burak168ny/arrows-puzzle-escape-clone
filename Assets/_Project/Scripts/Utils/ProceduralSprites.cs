using UnityEngine;

namespace Arrows.Utils
{
    /// <summary>
    /// Generates simple anti-aliased sprites at runtime so the project needs no external art.
    /// Results are cached; call once and reuse.
    /// </summary>
    public static class ProceduralSprites
    {
        public enum Shape
        {
            Circle,
            Heart,
            RoundedRect,
        }

        private static Sprite _circle;
        private static Sprite _heart;
        private static Sprite _roundedRect;

        public static Sprite Circle => _circle ? _circle : _circle = Create(Shape.Circle);
        public static Sprite Heart => _heart ? _heart : _heart = Create(Shape.Heart);

        /// <summary>9-sliced rounded rectangle, useful for buttons and panels.</summary>
        public static Sprite RoundedRect => _roundedRect ? _roundedRect : _roundedRect = Create(Shape.RoundedRect);

        private static Texture2D _dash;

        /// <summary>Horizontal dash (one dash per texture width), repeat-wrapped, for dotted lines.</summary>
        public static Texture2D DashTexture
        {
            get
            {
                if (_dash) return _dash;
                const int w = 32, h = 8;
                _dash = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    name = "Dash",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[w * h];
                for (int x = 0; x < w; x++)
                {
                    // Soft-edged dash covering the middle half.
                    float d = Mathf.Abs((x + 0.5f) / w - 0.5f);
                    byte a = (byte)(Mathf.Clamp01((0.25f - d) * w * 0.5f + 0.5f) * 255);
                    for (int y = 0; y < h; y++) pixels[y * w + x] = new Color32(255, 255, 255, a);
                }
                _dash.SetPixels32(pixels);
                _dash.Apply(false, true);
                return _dash;
            }
        }

        public static int SizeOf(Shape shape) => shape == Shape.Circle ? 64 : shape == Shape.Heart ? 128 : 96;

        /// <summary>9-slice border in pixels (left, bottom, right, top).</summary>
        public static Vector4 BorderOf(Shape shape) =>
            shape == Shape.RoundedRect ? new Vector4(40, 40, 40, 40) : Vector4.zero;

        private delegate float Sdf(Vector2 p); // p in [-1,1], negative inside

        private static Sprite Create(Shape shape)
        {
            int size = SizeOf(shape);
            var tex = CreateTexture(shape, readable: false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size,
                0, SpriteMeshType.FullRect, BorderOf(shape));
            sprite.name = shape.ToString();
            return sprite;
        }

        /// <summary>Renders the shape as white with alpha. Use readable textures to bake PNGs in the editor.</summary>
        public static Texture2D CreateTexture(Shape shape, bool readable)
        {
            int size = SizeOf(shape);
            Sdf sdf = shape == Shape.Circle ? CircleSdf : shape == Shape.Heart ? (Sdf)HeartSdf : RoundedRectSdf;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = shape.ToString(),
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float pixel = 2f / size;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float alpha = Mathf.Clamp01(0.5f - sdf(p) / pixel);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, !readable);
            return tex;
        }

        private static float CircleSdf(Vector2 p) => p.magnitude - 0.95f;

        private static float RoundedRectSdf(Vector2 p)
        {
            const float r = 0.8f;
            var q = new Vector2(Mathf.Abs(p.x) - (0.98f - r), Mathf.Abs(p.y) - (0.98f - r));
            var outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // Classic implicit heart curve (x²+y²-1)³ - x²y³, divided by its gradient length
        // to approximate a distance field for clean anti-aliased edges.
        private static float HeartSdf(Vector2 p)
        {
            const float scale = 1.2f, h = 0.002f;
            float x = p.x * scale, y = p.y * scale + 0.15f;
            float f = HeartCurve(x, y);
            float gx = (HeartCurve(x + h, y) - HeartCurve(x - h, y)) / (2f * h);
            float gy = (HeartCurve(x, y + h) - HeartCurve(x, y - h)) / (2f * h);
            float g = Mathf.Max(Mathf.Sqrt(gx * gx + gy * gy), 1e-4f);
            return f / g / scale;
        }

        private static float HeartCurve(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y;
        }
    }
}
