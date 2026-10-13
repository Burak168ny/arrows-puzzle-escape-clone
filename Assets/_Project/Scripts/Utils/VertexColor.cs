using UnityEngine;

namespace Arrows.Utils
{
    /// <summary>
    /// Unity converts SpriteRenderer/LineRenderer/particle colors to linear space automatically,
    /// but raw Mesh vertex colors are passed through unchanged. In a Linear-color-space project
    /// that makes the same sRGB color look lighter on a mesh. Use this for every mesh vertex color.
    /// </summary>
    public static class VertexColor
    {
        public static Color FromSrgb(Color c)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear) return c;
            var linear = c.linear;
            linear.a = c.a; // alpha is never gamma-encoded
            return linear;
        }
    }
}
