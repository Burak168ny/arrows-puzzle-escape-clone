using System;

namespace Arrows.Model.Generation
{
    public enum ShapeKind
    {
        Rectangle,
        Circle,
        Diamond,
        Heart,
        Ring,
    }

    /// <summary>Which grid cells may hold arrows, so levels can draw a silhouette.</summary>
    public static class LevelShape
    {
        public static bool[] CreateMask(ShapeKind shape, int width, int height)
        {
            var mask = new bool[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                // Normalized cell center in [-1, 1].
                double u = width == 1 ? 0 : (x + 0.5) / width * 2 - 1;
                double v = height == 1 ? 0 : (y + 0.5) / height * 2 - 1;
                mask[y * width + x] = Contains(shape, u, v);
            }
            return mask;
        }

        private static bool Contains(ShapeKind shape, double u, double v)
        {
            switch (shape)
            {
                case ShapeKind.Circle:
                    return u * u + v * v <= 1.0;
                case ShapeKind.Diamond:
                    return Math.Abs(u) + Math.Abs(v) <= 1.05;
                case ShapeKind.Ring:
                {
                    double r = u * u + v * v;
                    return r <= 1.0 && r >= 0.18;
                }
                case ShapeKind.Heart:
                {
                    // (x²+y²-1)³ - x²y³ <= 0, scaled to fill the square.
                    double x = u * 1.25, y = v * 1.25 + 0.2;
                    double a = x * x + y * y - 1;
                    return a * a * a - x * x * y * y * y <= 0;
                }
                default:
                    return true;
            }
        }
    }
}
