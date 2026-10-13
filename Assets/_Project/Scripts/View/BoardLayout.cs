using Arrows.Model;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>Maps grid coordinates to board-local positions. The grid is centered on the board origin.</summary>
    public readonly struct BoardLayout
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        private readonly Vector2 _origin;

        public BoardLayout(int width, int height, float cellSize)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            _origin = new Vector2(-(width - 1) * 0.5f * cellSize, -(height - 1) * 0.5f * cellSize);
        }

        public Vector2 ToLocal(GridPos p) => _origin + new Vector2(p.X, p.Y) * CellSize;
        public Vector2 ToLocal(Vector2 gridPoint) => _origin + gridPoint * CellSize;

        /// <summary>Continuous grid coordinates (cell centers are integers).</summary>
        public Vector2 ToGrid(Vector2 local) => (local - _origin) / CellSize;

        /// <summary>Local-space rectangle covering all cells.</summary>
        public Rect Bounds
        {
            get
            {
                var min = _origin - Vector2.one * (CellSize * 0.5f);
                return new Rect(min, new Vector2(Width, Height) * CellSize);
            }
        }
    }
}
