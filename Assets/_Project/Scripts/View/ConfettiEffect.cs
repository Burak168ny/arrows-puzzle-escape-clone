using UnityEngine;

namespace Arrows.View
{
    /// <summary>
    /// Two confetti cannons in the bottom corners of the view, built entirely in code.
    /// Speeds and sizes scale with the camera size so it looks the same on every board.
    /// </summary>
    public sealed class ConfettiEffect : MonoBehaviour
    {
        private const float ReferenceSize = 6f;

        private ParticleSystem _left;
        private ParticleSystem _right;

        public static ConfettiEffect Create(Material material, Color[] palette)
        {
            var go = new GameObject("Confetti");
            var effect = go.AddComponent<ConfettiEffect>();
            effect._left = effect.CreateCannon("Left", material, palette, 60f);
            effect._right = effect.CreateCannon("Right", material, palette, 120f);
            return effect;
        }

        public void Play(Camera cam)
        {
            float size = cam.orthographicSize;
            float halfW = size * cam.aspect;
            Vector3 c = cam.transform.position;
            float k = size / ReferenceSize;

            Fire(_left, new Vector3(c.x - halfW, c.y - size, 0f), k);
            Fire(_right, new Vector3(c.x + halfW, c.y - size, 0f), k);
        }

        private static void Fire(ParticleSystem ps, Vector3 position, float k)
        {
            ps.transform.position = position;
            var main = ps.main;
            main.startSpeedMultiplier = 15f * k;
            main.startSizeXMultiplier = 0.22f * k;
            main.startSizeYMultiplier = 0.12f * k;
            main.gravityModifierMultiplier = 1.1f * k;
            ps.Clear();
            ps.Play();
        }

        private ParticleSystem CreateCannon(string name, Material material, Color[] palette, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(-angle, 90f, 0f); // cone axis (+Z) tilted into the XY plane

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.7f, 1f);
            main.startSizeZ = 1f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = new ParticleSystem.MinMaxCurve(0.8f, 1f);
            main.maxParticles = 200;
            main.startColor = RandomColors(palette);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 80), new ParticleSystem.Burst(0.12f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.1f;

            // Air drag so the confetti floats down instead of falling like rocks.
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 100f;
            limit.drag = 1.6f;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = true;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 100;
            return ps;
        }

        private static ParticleSystem.MinMaxGradient RandomColors(Color[] palette)
        {
            // A gradient with hard steps, sampled randomly: each particle gets one palette color.
            int n = Mathf.Clamp(palette != null ? palette.Length : 0, 1, 8);
            var keys = new GradientColorKey[n];
            for (int i = 0; i < n; i++)
                keys[i] = new GradientColorKey(palette != null && palette.Length > 0 ? palette[i] : Color.white, (float)i / Mathf.Max(1, n - 1));

            var gradient = new Gradient { mode = GradientMode.Fixed };
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }
    }
}
