using System;
using System.Collections.Generic;
using Arrows.Model;
using Arrows.Utils;
using UnityEngine;
using UnityEngine.Pool;

namespace Arrows.View
{
    /// <summary>
    /// Builds and owns the visuals of a level: pooled arrow views and a single-mesh dot grid.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private float _cellSize = 1f;

        private ObjectPool<ArrowView> _pool;
        private readonly List<ArrowView> _views = new List<ArrowView>();
        private BoardTheme _theme;
        private ArrowVisualSettings _arrowSettings;

        private HintPathView _hintPath;
        private MeshRenderer _dotsRenderer;
        private Mesh _dotsMesh;
        private Material _dotsMaterial;
        private readonly List<Vector3> _dotVerts = new List<Vector3>();
        private readonly List<Vector2> _dotUvs = new List<Vector2>();
        private readonly List<Color> _dotColors = new List<Color>();
        private readonly List<int> _dotTris = new List<int>();

        public BoardLayout Layout { get; private set; }

        public event Action<ArrowView> ArrowExitFinished;
        public event Action<ArrowView> ArrowImpact;
        public event Action<ArrowView> ArrowBumpFinished;

        /// <summary>Board bounds in world space.</summary>
        public Rect WorldBounds
        {
            get
            {
                var local = Layout.Bounds;
                Vector2 min = transform.TransformPoint(local.min);
                Vector2 max = transform.TransformPoint(local.max);
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }

        public bool AnyBusy
        {
            get
            {
                foreach (var v in _views)
                    if (v != null && v.IsBusy) return true;
                return false;
            }
        }

        public void Build(LevelModel level, BoardTheme theme, ArrowVisualSettings arrowSettings, bool animate = true)
        {
            _theme = theme;
            _arrowSettings = arrowSettings;
            EnsurePool();
            Clear();

            Layout = new BoardLayout(level.Grid.Width, level.Grid.Height, _cellSize);
            BuildDots(level.Grid);

            var palette = theme.Palette;
            foreach (var arrow in level.Arrows)
            {
                if (arrow.IsRemoved)
                {
                    _views.Add(null);
                    continue;
                }

                var view = _pool.Get();
                view.Bind(arrow, Layout, ResolveColor(arrow, palette), arrowSettings);
                _views.Add(view);
            }

            if (animate) PlaySpawnRipple(arrowSettings.SpawnStagger);
        }

        /// <summary>Pops arrows in from the board center outwards.</summary>
        private void PlaySpawnRipple(float stagger)
        {
            float maxDistance = 0.01f;
            foreach (var v in _views)
                if (v != null) maxDistance = Mathf.Max(maxDistance, ((Vector2)v.transform.localPosition).magnitude);

            foreach (var v in _views)
                if (v != null) v.PlaySpawn(((Vector2)v.transform.localPosition).magnitude / maxDistance * stagger);
        }

        /// <summary>Pulses the arrow and shows a dashed line along its free exit path.</summary>
        public void ShowHint(int arrowId, int freeCells)
        {
            var view = GetView(arrowId);
            if (view == null) return;

            view.Highlight();
            if (_hintPath == null) _hintPath = HintPathView.Create(transform, _theme.SpriteMaterial);
            _hintPath.Show(view.Model, Layout, freeCells, view.BaseColor,
                _arrowSettings.LineWidth * 0.5f * _cellSize, _arrowSettings.HighlightDuration + 0.6f, 1.5f);
        }

        public void HideHint()
        {
            if (_hintPath != null) _hintPath.Hide();
        }

        public ArrowView GetView(int arrowId) =>
            arrowId >= 0 && arrowId < _views.Count ? _views[arrowId] : null;

        public Vector2 WorldToGrid(Vector3 world) => Layout.ToGrid(transform.InverseTransformPoint(world));

        public void Clear()
        {
            HideHint();
            foreach (var v in _views)
                if (v != null) _pool.Release(v);
            _views.Clear();
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new ObjectPool<ArrowView>(
                createFunc: CreateView,
                actionOnGet: v => v.gameObject.SetActive(true),
                actionOnRelease: v => v.gameObject.SetActive(false),
                actionOnDestroy: v => Destroy(v.gameObject),
                collectionCheck: false,
                defaultCapacity: 64,
                maxSize: 1024);
        }

        private ArrowView CreateView()
        {
            var view = ArrowView.Create(transform, _theme.SpriteMaterial);
            view.ExitFinished += OnExitFinished;
            view.Impact += v => ArrowImpact?.Invoke(v);
            view.BumpFinished += v => ArrowBumpFinished?.Invoke(v);
            return view;
        }

        private void OnExitFinished(ArrowView view)
        {
            int id = view.Model.Id;
            if (id < _views.Count && _views[id] == view) _views[id] = null;
            _pool.Release(view);
            ArrowExitFinished?.Invoke(view);
        }

        private static Color ResolveColor(ArrowModel arrow, Color[] palette)
        {
            if (!string.IsNullOrEmpty(arrow.Color) && ColorUtility.TryParseHtmlString(arrow.Color, out var c))
                return c;
            return palette != null && palette.Length > 0 ? palette[arrow.Id % palette.Length] : Color.black;
        }

        // Dots mark the cells arrows start on, so silhouettes (hearts, rings...) read clearly.
        private void BuildDots(GridModel grid)
        {
            if (_dotsMesh == null)
            {
                var go = new GameObject("Dots");
                go.transform.SetParent(transform, false);
                _dotsMesh = new Mesh { name = "BoardDots", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                go.AddComponent<MeshFilter>().sharedMesh = _dotsMesh;
                _dotsRenderer = go.AddComponent<MeshRenderer>();
                _dotsRenderer.sortingOrder = 0;
            }

            if (_dotsMaterial == null && _theme.SpriteMaterial != null)
            {
                _dotsMaterial = new Material(_theme.SpriteMaterial) { name = "BoardDots (Instance)" };
                _dotsMaterial.mainTexture = ProceduralSprites.Circle.texture;
            }
            _dotsRenderer.sharedMaterial = _dotsMaterial;

            _dotVerts.Clear();
            _dotUvs.Clear();
            _dotColors.Clear();
            _dotTris.Clear();

            float h = _theme.DotSize * 0.5f * _cellSize;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
            {
                var cell = new GridPos(x, y);
                if (grid.IsEmpty(cell)) continue;
                Vector3 p = Layout.ToLocal(cell);
                int i = _dotVerts.Count;
                _dotVerts.Add(p + new Vector3(-h, -h));
                _dotVerts.Add(p + new Vector3(-h, h));
                _dotVerts.Add(p + new Vector3(h, h));
                _dotVerts.Add(p + new Vector3(h, -h));
                _dotUvs.Add(new Vector2(0, 0));
                _dotUvs.Add(new Vector2(0, 1));
                _dotUvs.Add(new Vector2(1, 1));
                _dotUvs.Add(new Vector2(1, 0));
                for (int k = 0; k < 4; k++) _dotColors.Add(_theme.DotColor);
                _dotTris.Add(i);
                _dotTris.Add(i + 1);
                _dotTris.Add(i + 2);
                _dotTris.Add(i);
                _dotTris.Add(i + 2);
                _dotTris.Add(i + 3);
            }

            _dotsMesh.Clear();
            _dotsMesh.SetVertices(_dotVerts);
            _dotsMesh.SetUVs(0, _dotUvs);
            _dotsMesh.SetColors(_dotColors);
            _dotsMesh.SetTriangles(_dotTris, 0);
            _dotsMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
            if (_dotsMesh != null) Destroy(_dotsMesh);
            if (_dotsMaterial != null) Destroy(_dotsMaterial);
        }
    }
}
