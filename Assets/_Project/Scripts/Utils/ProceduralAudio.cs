using UnityEngine;

namespace Arrows.Utils
{
    /// <summary>
    /// Synthesizes short placeholder sound effects so the game has audio feedback
    /// before real clips are added. Each clip is generated once and cached by the caller.
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        public enum Kind
        {
            Tick,
            Swoosh,
            Thud,
            Fanfare,
            Sad,
            Chime,
        }

        public static AudioClip Create(Kind kind)
        {
            switch (kind)
            {
                case Kind.Tick: return Synth("Tick", 0.045f, (t, d) => Tone(1400f, t) * Decay(t, 60f));
                case Kind.Swoosh: return Synth("Swoosh", 0.2f, (t, d) => Sweep(520f, 1500f, t, d) * Envelope(t, d, 0.01f) * 0.55f);
                case Kind.Thud:
                    return Synth("Thud", 0.18f, (t, d) =>
                        (Sweep(220f, 90f, t, d) * 0.9f + Noise() * 0.25f * Decay(t, 50f)) * Decay(t, 18f));
                case Kind.Fanfare: return Notes("Fanfare", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.1f, 0.35f);
                case Kind.Sad: return Notes("Sad", new[] { 392f, 349.23f, 293.66f }, 0.16f, 0.3f);
                case Kind.Chime: return Notes("Chime", new[] { 880f, 1318.5f }, 0.08f, 0.25f);
                default: return null;
            }
        }

        private delegate float Generator(float t, float duration);

        private static AudioClip Synth(string name, float duration, Generator gen)
        {
            int n = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(gen((float)i / SampleRate, duration), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Notes(string name, float[] freqs, float noteLength, float tail)
        {
            float duration = freqs.Length * noteLength + tail;
            return Synth(name, duration, (t, d) =>
            {
                float v = 0f;
                for (int i = 0; i < freqs.Length; i++)
                {
                    float local = t - i * noteLength;
                    if (local < 0f) break;
                    // Sine with a soft second harmonic, plucked envelope.
                    v += (Tone(freqs[i], local) + 0.3f * Tone(freqs[i] * 2f, local)) * Decay(local, 9f) * Attack(local);
                }
                return v * 0.35f;
            });
        }

        private static float Tone(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

        private static float Sweep(float from, float to, float t, float duration)
        {
            // Integrated exponential sweep keeps the phase continuous.
            float k = Mathf.Log(to / from) / duration;
            float phase = from * (Mathf.Exp(k * t) - 1f) / k;
            return Mathf.Sin(2f * Mathf.PI * phase);
        }

        private static float Decay(float t, float rate) => Mathf.Exp(-rate * t);
        private static float Attack(float t) => Mathf.Clamp01(t / 0.004f);

        private static float Envelope(float t, float duration, float attack) =>
            Mathf.Clamp01(t / attack) * Mathf.Clamp01((duration - t) / (duration * 0.6f));

        private static float Noise() => Random.value * 2f - 1f;
    }
}
