using Arrows.Model;
using Arrows.Utils;
using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// Dashed line from a hinted arrow's head to the board edge, with dashes flowing outwards.
    /// One instance is reused for every hint.
    /// </summary>
    public sealed class HintPathView : MonoBehaviour
    {
        private const int MaxPoints = 64;

        private LineRenderer _line;
        private Material _material;
        private readonly Vector3[] _points = new Vector3[MaxPoints];
        private Vector2 _start;
        private Vector2 _step;
        private int _segments;
        private Color _color;
        private float _elapsed;
        private float _duration;
        private float _flowSpeed;

        public static HintPathView Create(Transform parent, Material baseMaterial)
        {
            var go = new GameObject("HintPath");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<HintPathView>();
            view.Build(baseMaterial);
            go.SetActive(false);
            return view;
        }

        private void Build(Material baseMaterial)
        {
            _material = new Material(baseMaterial) { name = "HintPath (Instance)", mainTexture = ProceduralSprites.DashTexture };
            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.sharedMaterial = _material;
            _line.alignment = LineAlignment.TransformZ;
            _line.textureMode = LineTextureMode.RepeatPerSegment; // one dash per cell
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.sortingOrder = 5;
        }

        public void Show(ArrowModel arrow, in BoardLayout layout, int freeCells, Color color,
            float width, float duration, float flowSpeed)
        {
            _step = new Vector2(arrow.Direction.X, arrow.Direction.Y) * layout.CellSize;
            _start = layout.ToLocal(arrow.Head) + _step * 0.5f;
            _segments = Mathf.Clamp(freeCells + 1, 1, MaxPoints - 2);
            _color = color;
            _duration = duration;
            _flowSpeed = flowSpeed;
            _elapsed = 0f;
            _line.widthMultiplier = width;
            gameObject.SetActive(true);
            Redraw();
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _duration)
            {
                Hide();
                return;
            }
            Redraw();
        }

        private void Redraw()
        {
            // Shift the whole dash pattern along the ray so it appears to flow out of the board.
            float shift = (_elapsed * _flowSpeed) % 1f;
            int count = 0;
            for (int i = 0; i <= _segments; i++)
                _points[count++] = _start + _step * (i - 1 + shift);
            _points[0] = _start; // clip the first, partial segment at the head

            _line.positionCount = count;
            _line.SetPositions(_points);

            float t = _elapsed / _duration;
            float alpha = Mathf.Min(Mathf.Clamp01(t * 6f), Mathf.Clamp01((1f - t) * 4f));
            var c = _color;
            c.a *= 0.65f * alpha;
            _line.startColor = c;
            _line.endColor = new Color(c.r, c.g, c.b, 0f);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
