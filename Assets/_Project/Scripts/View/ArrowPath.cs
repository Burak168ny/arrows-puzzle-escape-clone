using System.Collections.Generic;
using Arrows.Model;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// Snake-style path sampling. The arrow's body is its cell polyline, extended infinitely
    /// past the head along its direction. An arrow advanced by <c>offset</c> cells occupies
    /// the arc [offset, offset + length - 1] of that path.
    /// </summary>
    public static class ArrowPath
    {
        /// <summary>Point at arc length <paramref name="t"/>, in grid units.</summary>
        public static Vector2 PointAt(IReadOnlyList<GridPos> cells, GridPos dir, float t)
        {
            int last = cells.Count - 1;
            if (t >= last)
            {
                var head = cells[last];
                float ext = t - last;
                return new Vector2(head.X + dir.X * ext, head.Y + dir.Y * ext);
            }

            if (t <= 0f)
            {
                // Extend behind the tail too, so elastic bounces can overshoot backwards.
                var tail = cells[0];
                var back = cells[0] - cells[1];
                return new Vector2(tail.X - back.X * t, tail.Y - back.Y * t);
            }

            int i = (int)t;
            float f = t - i;
            var a = cells[i];
            var b = cells[i + 1];
            return new Vector2(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f);
        }

        /// <summary>
        /// Writes the visible polyline (tail to head) into <paramref name="buffer"/>, converted with the layout.
        /// Only the start, interior body corners and the end are emitted. The buffer needs
        /// <c>cells.Count + 1</c> entries. Returns the point count.
        /// </summary>
        public static int Sample(IReadOnlyList<GridPos> cells, GridPos dir, float offset,
            in BoardLayout layout, Vector3[] buffer)
        {
            int last = cells.Count - 1;
            float start = offset;
            float end = offset + last;
            int count = 0;

            buffer[count++] = layout.ToLocal(PointAt(cells, dir, start));

            int first = Mathf.Max(1, Mathf.FloorToInt(start) + 1);
            // The head cell is never a corner: the path continues straight along dir past it.
            int lastInterior = Mathf.Min(last - 1, Mathf.CeilToInt(end) - 1);
            for (int k = first; k <= lastInterior; k++)
                if (IsCorner(cells, k))
                    buffer[count++] = layout.ToLocal(cells[k]);

            buffer[count++] = layout.ToLocal(PointAt(cells, dir, end));
            return count;
        }

        private static bool IsCorner(IReadOnlyList<GridPos> cells, int k) =>
            cells[k] - cells[k - 1] != cells[k + 1] - cells[k];
    }
}
