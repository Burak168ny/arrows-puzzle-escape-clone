using System;

namespace Arrows.Model
{
    /// <summary>Occupancy grid: each cell stores the id of the arrow on it, or <see cref="Empty"/>.</summary>
    public sealed class GridModel
    {
        public const int Empty = -1;

        private readonly int[] _cells;

        public int Width { get; }
        public int Height { get; }

        public GridModel(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Grid size must be positive.");
            Width = width;
            Height = height;
            _cells = new int[width * height];
            Array.Fill(_cells, Empty);
        }

        public bool InBounds(GridPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public int Get(GridPos p) => InBounds(p) ? _cells[p.Y * Width + p.X] : Empty;

        public bool IsEmpty(GridPos p) => Get(p) == Empty;

        internal void Set(GridPos p, int arrowId) => _cells[p.Y * Width + p.X] = arrowId;

        internal GridModel Clone()
        {
            var copy = new GridModel(Width, Height);
            Array.Copy(_cells, copy._cells, _cells.Length);
            return copy;
        }
    }
}
