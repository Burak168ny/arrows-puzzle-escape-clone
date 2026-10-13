using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Arrows.Core
{
    /// <summary>
    /// Gesture recognizer for touch and mouse (Input System).
    /// One pointer: a short press that stays within the slop radius is a tap, otherwise a pan.
    /// Two fingers: pinch zoom plus pan of the midpoint. Mouse wheel zooms around the cursor.
    /// Gestures that start over UI are ignored entirely.
    /// </summary>
    public sealed class InputHandler : MonoBehaviour
    {
        [Tooltip("Max pointer travel for a tap, in inches (converted with Screen.dpi).")]
        [SerializeField] private float _tapSlopInches = 0.1f;
        [SerializeField] private float _maxTapDuration = 0.45f;
        [Tooltip("Orthographic size change per mouse wheel notch.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _wheelZoomStep = 0.12f;

        /// <summary>Tap position in screen pixels.</summary>
        public event Action<Vector2> Tapped;

        /// <summary>Pointer movement since last frame, in screen pixels.</summary>
        public event Action<Vector2> Panned;

        /// <summary>Multiplier for the camera size (&lt; 1 zooms in) and the screen pivot to zoom around.</summary>
        public event Action<float, Vector2> Zoomed;

        // Single-pointer gesture state.
        private bool _active;
        private bool _ignored;
        private bool _tapCandidate;
        private bool _panning;
        private Vector2 _startPosition;
        private Vector2 _lastPosition;
        private float _startTime;

        // Pinch state.
        private bool _pinching;
        private float _lastPinchDistance;
        private Vector2 _lastPinchMid;

        private PointerEventData _pointerData;
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();

        private float SlopPixels
        {
            get
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
                return Mathf.Max(8f, _tapSlopInches * dpi);
            }
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();
        private void OnDisable() => EnhancedTouchSupport.Disable();

        private void Update()
        {
            if (Touchscreen.current != null && (Touch.activeTouches.Count > 0 || _pinching))
                UpdateTouches();
            else
                UpdateMouse();
        }

        private void UpdateTouches()
        {
            var touches = Touch.activeTouches;

            if (touches.Count >= 2)
            {
                UpdatePinch(touches[0].screenPosition, touches[1].screenPosition);
                return;
            }

            if (touches.Count == 0)
            {
                _pinching = false;
                ResetPointer();
                return;
            }

            var touch = touches[0];
            if (_pinching)
            {
                // Lifted one finger of a pinch: continue as a pan, never as a tap.
                _pinching = false;
                _active = true;
                _tapCandidate = false;
                _panning = true;
                _lastPosition = touch.screenPosition;
                return;
            }

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginPointer(touch.screenPosition);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    MovePointer(touch.screenPosition);
                    break;
                case TouchPhase.Ended:
                    EndPointer(touch.screenPosition);
                    break;
                case TouchPhase.Canceled:
                    ResetPointer();
                    break;
            }
        }

        private void UpdatePinch(Vector2 a, Vector2 b)
        {
            float distance = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;

            if (!_pinching)
            {
                _pinching = true;
                _tapCandidate = false;
                _ignored = _ignored || IsOverUI(mid);
                _lastPinchDistance = distance;
                _lastPinchMid = mid;
                return;
            }

            if (!_ignored && distance > 1f && _lastPinchDistance > 1f)
            {
                Zoomed?.Invoke(_lastPinchDistance / distance, mid);
                Panned?.Invoke(mid - _lastPinchMid);
            }

            _lastPinchDistance = distance;
            _lastPinchMid = mid;
        }

        private void UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 position = mouse.position.ReadValue();
            var button = mouse.leftButton;

            if (button.wasPressedThisFrame) BeginPointer(position);
            else if (button.isPressed) MovePointer(position);
            if (button.wasReleasedThisFrame) EndPointer(position);

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUI(position))
                Zoomed?.Invoke(scroll > 0f ? 1f - _wheelZoomStep : 1f + _wheelZoomStep, position);
        }

        private void BeginPointer(Vector2 position)
        {
            _active = true;
            _ignored = IsOverUI(position);
            _tapCandidate = !_ignored;
            _panning = false;
            _startPosition = _lastPosition = position;
            _startTime = Time.unscaledTime;
        }

        private void MovePointer(Vector2 position)
        {
            if (!_active || _ignored) return;

            if (!_panning)
            {
                float slop = SlopPixels;
                if ((position - _startPosition).sqrMagnitude > slop * slop)
                {
                    _panning = true;
                    _tapCandidate = false;
                }
            }

            if (_panning) Panned?.Invoke(position - _lastPosition);
            _lastPosition = position;
        }

        private void EndPointer(Vector2 position)
        {
            if (_active && !_ignored)
            {
                MovePointer(position);
                bool quick = Time.unscaledTime - _startTime <= _maxTapDuration;
                if (_tapCandidate && quick) Tapped?.Invoke(_startPosition);
            }
            ResetPointer();
        }

        private void ResetPointer()
        {
            _active = false;
            _ignored = false;
            _tapCandidate = false;
            _panning = false;
        }

        private bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;

            _pointerData ??= new PointerEventData(es);
            _pointerData.position = screenPosition;
            _raycastResults.Clear();
            es.RaycastAll(_pointerData, _raycastResults);
            return _raycastResults.Count > 0;
        }
    }
}
