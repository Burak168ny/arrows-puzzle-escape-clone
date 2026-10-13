using System.Collections.Generic;
using Arrows.Model;
using Arrows.Utils;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// The grid is invisible at rest. Cells a moving arrow passes over briefly reveal a small dot,
    /// which holds for a moment after the arrow has gone and then fades out.
    /// One mesh with a quad per cell; only vertex alphas change, and only while dots are visible.
    /// </summary>
    public sealed class DotTrailView : MonoBehaviour
    {
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;

        private int _width;
        private int _height;
        private float[] _age;       // seconds since last covered, or < 0 when hidden
        private Color32[] _colors;  // 4 per cell
        private readonly List<int> _active = new List<int>();
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();

        private Color32 _color;
        private float _hold;
        private float _fade;

        public static DotTrailView Create(Transform parent, Material baseMaterial)
        {
            var go = new GameObject("DotTrail");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DotTrailView>();
            view._mesh = new Mesh { name = "DotTrail", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            view._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = view._mesh;
            view._renderer = go.AddComponent<MeshRenderer>();
            view._renderer.sortingOrder = 1; // above the board, below arrows
            view._material = new Material(baseMaterial) { name = "DotTrail (Instance)", mainTexture = ProceduralSprites.Circle.texture };
            view._renderer.sharedMaterial = view._material;
            view.enabled = false;
            return view;
        }

        public void Build(in BoardLayout layout, BoardTheme theme)
        {
            _width = layout.Width;
            _height = layout.Height;
            _color = VertexColor.FromSrgb(theme.TrailDotColor);
            _hold = Mathf.Max(0f, theme.TrailHold);
            _fade = Mathf.Max(0.01f, theme.TrailFade);
            gameObject.SetActive(theme.TrailDots);

            int cells = _width * _height;
            if (_age == null || _age.Length != cells)
            {
                _age = new float[cells];
                _colors = new Color32[cells * 4];
            }
            for (int i = 0; i < cells; i++) _age[i] = -1f;
            System.Array.Clear(_colors, 0, _colors.Length);
            _active.Clear();

            _verts.Clear();
            _uvs.Clear();
            _tris.Clear();
            float h = theme.TrailDotSize * 0.5f * layout.CellSize;
            for (int y = 0; y < _height; y++)
            for (int x = 0; x < _width; x++)
            {
                Vector3 p = layout.ToLocal(new GridPos(x, y));
                int v = _verts.Count;
                _verts.Add(p + new Vector3(-h, -h));
                _verts.Add(p + new Vector3(-h, h));
                _verts.Add(p + new Vector3(h, h));
                _verts.Add(p + new Vector3(h, -h));
                _uvs.Add(new Vector2(0, 0));
                _uvs.Add(new Vector2(0, 1));
                _uvs.Add(new Vector2(1, 1));
                _uvs.Add(new Vector2(1, 0));
                _tris.Add(v); _tris.Add(v + 1); _tris.Add(v + 2);
                _tris.Add(v); _tris.Add(v + 2); _tris.Add(v + 3);
            }

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetUVs(0, _uvs);
            _mesh.colors32 = _colors;
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
            enabled = false;
        }

        /// <summary>Marks the cells an arrow currently covers. <paramref name="start"/>/<paramref name="end"/> are path arcs.</summary>
        public void MarkArc(ArrowModel arrow, float start, float end)
        {
            if (!gameObject.activeSelf || _age == null) return;

            var cells = arrow.Cells;
            int last = cells.Count - 1;
            var head = arrow.Head;
            var dir = arrow.Direction;

            int from = Mathf.Max(0, Mathf.CeilToInt(start));
            int to = Mathf.FloorToInt(end);
            for (int t = from; t <= to; t++)
            {
                var c = t <= last ? cells[t] : head + dir * (t - last);
                if (c.X < 0 || c.Y < 0 || c.X >= _width || c.Y >= _height)
                {
                    if (t > last) break; // past the edge: the rest of the ray is outside too
                    continue;
                }

                int i = c.Y * _width + c.X;
                if (_age[i] < 0f) _active.Add(i);
                _age[i] = 0f;
            }

            if (_active.Count > 0) enabled = true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int k = _active.Count - 1; k >= 0; k--)
            {
                int i = _active[k];
                _age[i] += dt;
                float alpha = _age[i] <= _hold ? 1f : 1f - (_age[i] - _hold) / _fade;

                if (alpha <= 0f)
                {
                    _age[i] = -1f;
                    alpha = 0f;
                    _active[k] = _active[_active.Count - 1];
                    _active.RemoveAt(_active.Count - 1);
                }

                var c = _color;
                c.a = (byte)(_color.a * alpha);
                int v = i * 4;
                _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = c;
            }

            _mesh.colors32 = _colors;
            if (_active.Count == 0) enabled = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
