using System;

namespace Arrows.Model
{
    /// <summary>Integer grid coordinate. Also used as a unit direction vector.</summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly GridPos Zero = new GridPos(0, 0);
        public static readonly GridPos Up = new GridPos(0, 1);
        public static readonly GridPos Down = new GridPos(0, -1);
        public static readonly GridPos Left = new GridPos(-1, 0);
        public static readonly GridPos Right = new GridPos(1, 0);

        /// <summary>The four orthogonal directions, in a fixed order.</summary>
        public static readonly GridPos[] Directions = { Up, Right, Down, Left };

        public int ManhattanLength => Math.Abs(X) + Math.Abs(Y);
        public bool IsUnitDirection => ManhattanLength == 1;

        public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);
        public static GridPos operator -(GridPos a, GridPos b) => new GridPos(a.X - b.X, a.Y - b.Y);
        public static GridPos operator -(GridPos a) => new GridPos(-a.X, -a.Y);
        public static GridPos operator *(GridPos a, int k) => new GridPos(a.X * k, a.Y * k);
        public static bool operator ==(GridPos a, GridPos b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridPos a, GridPos b) => !(a == b);

        public bool Equals(GridPos other) => this == other;
        public override bool Equals(object obj) => obj is GridPos other && this == other;
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => $"({X},{Y})";
    }
}
