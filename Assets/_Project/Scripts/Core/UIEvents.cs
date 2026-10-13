using System;

namespace Arrows.Core
{
    /// <summary>
    /// Fire-and-forget UI signals that the feedback layer reacts to, so UI widgets
    /// can trigger sound/haptics without knowing about the audio or haptic systems.
    /// </summary>
    public static class UIEvents
    {
        public static event Action ButtonPressed;

        public static void RaiseButtonPressed() => ButtonPressed?.Invoke();
    }
}
