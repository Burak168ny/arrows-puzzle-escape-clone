using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// Orthographic camera that frames the board, with bounded pan and pinch / wheel zoom.
    /// The fitted view is the most zoomed-out state; zooming in unlocks panning.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Tooltip("Empty space around the board, in world units.")]
        [SerializeField] private float _padding = 0.6f;
        [Tooltip("Fraction of the screen height reserved for the top HUD.")]
        [SerializeField, Range(0f, 0.4f)] private float _topReserve = 0.16f;
        [Tooltip("Fraction of the screen height reserved for the bottom HUD.")]
        [SerializeField, Range(0f, 0.4f)] private float _bottomReserve = 0.15f;
        [Tooltip("Smallest orthographic size (world units, 1 cell = 1 unit). Limits zoom-in.")]
        [SerializeField] private float _minSize = 3.5f;

        [Header("Shake")]
        [Tooltip("Shake amplitude as a fraction of the orthographic size.")]
        [SerializeField] private float _shakeStrength = 0.025f;
        [SerializeField] private float _shakeFrequency = 28f;

        private Camera _camera;
        private Rect _board;
        private float _fitSize = 5f;
        private Vector2 _center;
        private float _shakeElapsed = -1f;
        private float _shakeDuration;
        private float _shakeAmount;

        public Camera Camera => _camera ? _camera : _camera = GetComponent<Camera>();
        public float FitSize => _fitSize;
        public bool CanZoom => _fitSize > _minSize + 0.01f;

        public void SetBackground(Color color)
        {
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = color;
        }

        /// <summary>Fits the rect inside the screen area left free by the HUD.</summary>
        public void Fit(Rect worldRect)
        {
            _board = worldRect;
            _fitSize = FitOrthoSize(worldRect.size, Camera.aspect, _padding, _topReserve, _bottomReserve);
            Camera.orthographicSize = _fitSize;
            SetCenter(worldRect.center);
        }

        public void Pan(Vector2 screenDelta)
        {
            float worldPerPixel = Camera.orthographicSize * 2f / Mathf.Max(1, Screen.height);
            SetCenter(_center - screenDelta * worldPerPixel);
        }

        /// <summary>Scales the view size by <paramref name="multiplier"/> keeping the world point under the pivot fixed.</summary>
        public void Zoom(float multiplier, Vector2 screenPivot)
        {
            if (!CanZoom) return;

            Vector2 before = ScreenToWorld(screenPivot);
            Camera.orthographicSize = Mathf.Clamp(Camera.orthographicSize * multiplier, _minSize, _fitSize);
            Vector2 after = ScreenToWorld(screenPivot);
            SetCenter(_center + (before - after));
        }

        /// <summary>Short, decaying shake. <paramref name="intensity"/> 1 = default strength.</summary>
        public void Shake(float intensity = 1f, float duration = 0.25f)
        {
            _shakeAmount = intensity;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeElapsed = 0f;
        }

        private void LateUpdate()
        {
            if (_shakeElapsed < 0f) return;

            _shakeElapsed += Time.unscaledDeltaTime;
            float t = _shakeElapsed / _shakeDuration;
            Vector2 offset = Vector2.zero;
            if (t >= 1f)
            {
                _shakeElapsed = -1f;
            }
            else
            {
                float decay = (1f - t) * (1f - t);
                float amp = _shakeStrength * Camera.orthographicSize * _shakeAmount * decay;
                float time = _shakeElapsed * _shakeFrequency;
                offset = new Vector2(Mathf.PerlinNoise(time, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, time) - 0.5f) * (2f * amp);
            }
            transform.position = new Vector3(_center.x + offset.x, _center.y + offset.y, transform.position.z);
        }

        public Vector3 ScreenToWorld(Vector2 screen)
        {
            var p = Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -transform.position.z));
            p.z = 0f;
            return p;
        }

        private void SetCenter(Vector2 desired)
        {
            _center = ClampCenter(desired, Camera.orthographicSize, Camera.aspect, _board, _padding, _topReserve, _bottomReserve);
            transform.position = new Vector3(_center.x, _center.y, transform.position.z);
        }

        /// <summary>Orthographic size that fits a board of the given size inside the HUD-free band.</summary>
        public static float FitOrthoSize(Vector2 boardSize, float aspect, float padding, float topReserve, float bottomReserve)
        {
            float usable = Mathf.Max(0.2f, 1f - topReserve - bottomReserve);
            float sizeForHeight = (boardSize.y + padding * 2f) * 0.5f / usable;
            float sizeForWidth = (boardSize.x + padding * 2f) * 0.5f / aspect;
            return Mathf.Max(sizeForHeight, sizeForWidth);
        }

        /// <summary>
        /// Clamps the camera center so the board (plus padding) cannot be dragged past the usable band.
        /// When the view is larger than the board on an axis, the range collapses to the centered position,
        /// which at the fit size equals the fitted framing, so zooming never makes the view jump.
        /// </summary>
        public static Vector2 ClampCenter(Vector2 desired, float size, float aspect, Rect board,
            float padding, float topReserve, float bottomReserve)
        {
            float viewH = size * 2f;
            float halfW = size * aspect;

            float loX = board.xMin + halfW - padding;
            float hiX = board.xMax - halfW + padding;
            float loY = board.yMin + size - bottomReserve * viewH - padding;
            float hiY = board.yMax - size + topReserve * viewH + padding;

            return new Vector2(ClampRange(desired.x, loX, hiX), ClampRange(desired.y, loY, hiY));
        }

        private static float ClampRange(float v, float lo, float hi) =>
            lo > hi ? (lo + hi) * 0.5f : Mathf.Clamp(v, lo, hi);
    }
}
