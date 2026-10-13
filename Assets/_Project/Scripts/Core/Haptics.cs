using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Arrows.Core
{
    public enum HapticType
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        Success = 3,
        Failure = 4,
    }

    /// <summary>
    /// Short, platform-native haptics.
    /// iOS: UIImpact/UINotificationFeedbackGenerator (Plugins/iOS/ArrowsHaptics.mm).
    /// Android: Vibrator with VibrationEffect.createOneShot (API 26+), plain vibrate(ms) below.
    /// Elsewhere (editor, desktop): no-op.
    /// </summary>
    public sealed class Haptics
    {
        public bool Enabled { get; set; } = true;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void _ArrowsHaptic(int type);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject _vibrator;
        private int _sdk;
        private bool _initialized;
#endif

        public void Play(HapticType type)
        {
            if (!Enabled) return;

#if UNITY_IOS && !UNITY_EDITOR
            _ArrowsHaptic((int)type);
#elif UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(type);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void PlayAndroid(HapticType type)
        {
            if (!_initialized) InitAndroid();

            long ms;
            int amplitude;
            switch (type)
            {
                case HapticType.Light: ms = 12; amplitude = 60; break;
                case HapticType.Medium: ms = 20; amplitude = 120; break;
                case HapticType.Heavy: ms = 35; amplitude = 220; break;
                case HapticType.Success: ms = 30; amplitude = 160; break;
                default: ms = 60; amplitude = 255; break;
            }

            try
            {
                if (_vibrator == null)
                {
                    // Last resort; long buzz, so only for strong events.
                    if (type == HapticType.Failure) Handheld.Vibrate();
                    return;
                }

                if (_sdk >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude);
                    _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Haptics] {e.Message}");
                _vibrator = null;
            }
        }

        private void InitAndroid()
        {
            _initialized = true;
            try
            {
                using var version = new AndroidJavaClass("android.os.Build$VERSION");
                _sdk = version.GetStatic<int>("SDK_INT");
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Haptics] Vibrator unavailable: {e.Message}");
                _vibrator = null;
            }
        }
#endif
    }
}
