using System;
using Arrows.Model;
using Arrows.Utils;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// Draws one arrow as a thick rounded LineRenderer plus a triangular head mesh.
    /// One motion at a time (exit or bump) plus independent overlay effects
    /// (spawn pop, tap punch, red flash, hint pulse). Update only runs while something animates.
    /// </summary>
    public sealed class ArrowView : MonoBehaviour
    {
        private enum Motion
        {
            None,
            Exiting,
            BumpForward,
            BumpBack,
        }

        private const float Inactive = -1f;

        private LineRenderer _line;
        private Transform _head;
        private MeshRenderer _headRenderer;
        private Mesh _headMesh;
        private readonly Color[] _headColors = new Color[3];
        private readonly Vector3[] _headVertices = new Vector3[3];
        private static readonly int[] HeadTriangles = { 0, 2, 1 };
        private Vector3[] _points = Array.Empty<Vector3>();

        private ArrowModel _model;
        private BoardLayout _layout;
        private ArrowVisualSettings _settings;
        private Color _baseColor;
        private Vector3 _pivot;

        // Motion
        private Motion _motion;
        private float _offset;
        private float _from;
        private float _to;
        private float _elapsed;
        private float _duration;
        private float _alpha = 1f;
        private float _exitShrink = 1f;

        // Overlay effects: elapsed time, or Inactive.
        private float _spawnDelay;
        private float _spawnElapsed = Inactive;
        private float _punchElapsed = Inactive;
        private float _flashElapsed = Inactive;
        private float _highlightElapsed = Inactive;

        public ArrowModel Model => _model;
        public Color BaseColor => _baseColor;

        /// <summary>True while moving or popping in; busy arrows ignore taps.</summary>
        public bool IsBusy => _motion != Motion.None || _spawnElapsed >= 0f;

        /// <summary>Raised when the exit animation is done and the view can be recycled.</summary>
        public event Action<ArrowView> ExitFinished;

        /// <summary>Raised when a blocked arrow touches its blocker (before bouncing back).</summary>
        public event Action<ArrowView> Impact;

        /// <summary>Raised when a blocked arrow is back at rest.</summary>
        public event Action<ArrowView> BumpFinished;

        /// <summary>Raised every frame while moving with the covered arc [start, end] in path units.</summary>
        public event Action<ArrowView, float, float> Moved;

        public static ArrowView Create(Transform parent, Material material)
        {
            var go = new GameObject("Arrow");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ArrowView>();
            view.Build(material);
            return view;
        }

        private void Build(Material material)
        {
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.sharedMaterial = material;
            _line.alignment = LineAlignment.TransformZ;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            var headGo = new GameObject("Head");
            _head = headGo.transform;
            _head.SetParent(transform, false);
            _headMesh = new Mesh { name = "ArrowHead" };
            _headMesh.MarkDynamic();
            headGo.AddComponent<MeshFilter>().sharedMesh = _headMesh;
            _headRenderer = headGo.AddComponent<MeshRenderer>();
            _headRenderer.sharedMaterial = material;
            _headRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _headRenderer.receiveShadows = false;
        }

        public void Bind(ArrowModel model, in BoardLayout layout, Color color, ArrowVisualSettings settings)
        {
            _model = model;
            _layout = layout;
            _settings = settings;
            _baseColor = color;

            if (_points.Length < model.Length + 1) _points = new Vector3[model.Length + 1];

            // Pivot at the bounding-box center so pop-in scales around the arrow, not the board.
            Vector2 min = layout.ToLocal(model.Cells[0]), max = min;
            foreach (var c in model.Cells)
            {
                var p = layout.ToLocal(c);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            _pivot = (min + max) * 0.5f;
            transform.localPosition = _pivot;

            float cell = layout.CellSize;
            _line.numCornerVertices = settings.CornerVertices;
            _line.numCapVertices = settings.CapVertices;

            _headVertices[0] = new Vector3(settings.HeadLength * cell, 0f, 0f);
            _headVertices[1] = new Vector3(-0.04f * cell, settings.HeadWidth * 0.5f * cell, 0f);
            _headVertices[2] = new Vector3(-0.04f * cell, -settings.HeadWidth * 0.5f * cell, 0f);
            _headMesh.Clear();
            _headMesh.vertices = _headVertices;
            _headMesh.triangles = HeadTriangles;
            _headMesh.RecalculateBounds();

            var dir = model.Direction;
            _head.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.Y, dir.X) * Mathf.Rad2Deg);

            _motion = Motion.None;
            _offset = 0f;
            _alpha = 1f;
            _exitShrink = 1f;
            _spawnElapsed = _punchElapsed = _flashElapsed = _highlightElapsed = Inactive;
            SetSorting(settings.SortingOrder);
            gameObject.name = $"Arrow {model.Id}";

            ApplyScale();
            ApplyColor();
            Redraw();
            enabled = false;
        }

        // ---- Motions ----

        /// <summary>Slides out along the path, accelerating, shrinking slightly and fading.</summary>
        public void PlayExit(int freeCells)
        {
            float distance = freeCells + (_model.Length - 1) + _settings.ExitExtraCells;
            BeginMotion(Motion.Exiting, _offset, distance,
                _settings.ExitBaseDuration + distance * _settings.ExitDurationPerCell);
            SetSorting(_settings.SortingOrder + _settings.MovingSortingOrderBoost);
            Punch();
        }

        /// <summary>Moves up to the blocker, hits it and bounces back to rest.</summary>
        public void PlayBump(int freeCells)
        {
            float distance = freeCells + _settings.BumpOvershoot;
            BeginMotion(Motion.BumpForward, 0f, distance, Mathf.Max(0.06f, distance / _settings.BumpSpeed));
            SetSorting(_settings.SortingOrder + _settings.MovingSortingOrderBoost);
            Punch();
        }

        private void BeginMotion(Motion motion, float from, float to, float duration)
        {
            _motion = motion;
            _from = from;
            _to = to;
            _elapsed = 0f;
            _duration = Mathf.Max(0.01f, duration);
            enabled = true;
        }

        // ---- Effects ----

        /// <summary>Pops in from zero scale after a delay (level start).</summary>
        public void PlaySpawn(float delay)
        {
            _spawnDelay = delay;
            _spawnElapsed = 0f;
            ApplyScale();
            ApplyColor();
            enabled = true;
        }

        /// <summary>Short thickness/brightness bump as tap feedback.</summary>
        public void Punch() => StartTimer(ref _punchElapsed);

        /// <summary>Pulses red, used on the arrow that was hit.</summary>
        public void Flash() => StartTimer(ref _flashElapsed);

        /// <summary>Pulses the thickness a few times, used by the hint.</summary>
        public void Highlight() => StartTimer(ref _highlightElapsed);

        private void StartTimer(ref float timer)
        {
            timer = 0f;
            enabled = true;
        }

        // ---- Update ----

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_motion != Motion.None) StepMotion(dt);

            bool overlays = Advance(ref _spawnElapsed, dt, _spawnDelay + _settings.SpawnDuration)
                          | Advance(ref _punchElapsed, dt, _settings.PunchDuration)
                          | Advance(ref _flashElapsed, dt, _settings.FlashDuration)
                          | Advance(ref _highlightElapsed, dt, _settings.HighlightDuration);

            ApplyScale();
            ApplyColor();

            if (_motion == Motion.None && !overlays) enabled = false;
        }

        /// <summary>Advances a timer; returns true while it is still running.</summary>
        private static bool Advance(ref float timer, float dt, float duration)
        {
            if (timer < 0f) return false;
            timer += dt;
            if (timer < duration) return true;
            timer = Inactive;
            return false;
        }

        private void StepMotion(float dt)
        {
            _elapsed += dt;
            float t = Mathf.Clamp01(_elapsed / _duration);

            switch (_motion)
            {
                case Motion.Exiting:
                    _offset = Mathf.LerpUnclamped(_from, _to, Mathf.Lerp(t, Easing.InQuad(t), _settings.ExitEaseIn));
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t));
                    _alpha = 1f - fade;
                    _exitShrink = Mathf.Lerp(1f, _settings.ExitEndWidth, fade);
                    break;
                case Motion.BumpForward:
                    _offset = Mathf.LerpUnclamped(_from, _to, Easing.InQuad(t));
                    break;
                case Motion.BumpBack:
                    _offset = Mathf.LerpUnclamped(_from, _to, Easing.OutElastic(t));
                    break;
            }

            Redraw();
            Moved?.Invoke(this, _offset, _offset + _model.Length - 1);
            if (t < 1f) return;

            switch (_motion)
            {
                case Motion.Exiting:
                    _motion = Motion.None;
                    ExitFinished?.Invoke(this);
                    break;
                case Motion.BumpForward:
                    Impact?.Invoke(this);
                    BeginMotion(Motion.BumpBack, _offset, 0f, _settings.BounceBackDuration);
                    break;
                case Motion.BumpBack:
                    _motion = Motion.None;
                    _offset = 0f;
                    Redraw();
                    SetSorting(_settings.SortingOrder);
                    BumpFinished?.Invoke(this);
                    break;
            }
        }

        // ---- Rendering ----

        private float SpawnProgress
        {
            get
            {
                if (_spawnElapsed < 0f) return 1f;
                return Mathf.Clamp01((_spawnElapsed - _spawnDelay) / _settings.SpawnDuration);
            }
        }

        private void ApplyScale()
        {
            float spawn = Easing.OutBack(SpawnProgress, 2.2f);
            transform.localScale = new Vector3(spawn, spawn, 1f);

            float width = _exitShrink;
            if (_punchElapsed >= 0f)
                width *= 1f + _settings.PunchScale * Mathf.Sin(_punchElapsed / _settings.PunchDuration * Mathf.PI);
            if (_highlightElapsed >= 0f)
                width *= 1f + _settings.HighlightScale *
                    Mathf.Abs(Mathf.Sin(_highlightElapsed / _settings.HighlightDuration * Mathf.PI * _settings.HighlightPulses));

            // LineRenderer width ignores the transform scale, so apply the spawn scale by hand.
            _line.widthMultiplier = _settings.LineWidth * _layout.CellSize * width * Mathf.Max(0f, spawn);
            _head.localScale = new Vector3(width, width, 1f);
        }

        private void ApplyColor()
        {
            var c = _baseColor;
            if (_punchElapsed >= 0f)
                c = Color.Lerp(c, Color.white, _settings.PunchBrighten * Mathf.Sin(_punchElapsed / _settings.PunchDuration * Mathf.PI));
            if (_flashElapsed >= 0f)
            {
                // Two quick pulses.
                float p = Mathf.Abs(Mathf.Sin(_flashElapsed / _settings.FlashDuration * Mathf.PI * 2f));
                c = Color.Lerp(c, _settings.FlashColor, p);
            }

            c.a *= _alpha * Mathf.Clamp01(SpawnProgress * 3f);
            _line.startColor = c;
            _line.endColor = c;
            // The line color is converted to linear by Unity; the head mesh needs the same conversion
            // or it renders lighter than the body.
            var headColor = VertexColor.FromSrgb(c);
            for (int i = 0; i < _headColors.Length; i++) _headColors[i] = headColor;
            _headMesh.colors = _headColors;
        }

        private void Redraw()
        {
            int count = ArrowPath.Sample(_model.Cells, _model.Direction, _offset, _layout, _points);
            for (int i = 0; i < count; i++) _points[i] -= _pivot;
            _line.positionCount = count;
            _line.SetPositions(_points);
            _head.localPosition = _points[count - 1];
        }

        private void SetSorting(int order)
        {
            _line.sortingOrder = order;
            _headRenderer.sortingOrder = order + 1;
        }

        private void OnDestroy()
        {
            if (_headMesh != null) Destroy(_headMesh);
        }
    }
}
