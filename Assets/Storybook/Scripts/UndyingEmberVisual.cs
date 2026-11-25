using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    internal sealed class UndyingEmberVisual : MonoBehaviour
    {
        const float WaveDuration = .24f;
        readonly Vector3[] points = new Vector3[64];
        MonsterHealth target;
        ParticleSystem particles;
        LineRenderer wave;
        Material waveMaterial;
        double waveStarted;
        float waveRadius;
        Vector3 waveCenter;
        bool removed;

        internal bool IsFinishing { get; private set; }

        internal static UndyingEmberVisual Create(MonsterHealth target, Material particleMaterial)
        {
            var effect = new GameObject("Undying Ember fire").AddComponent<UndyingEmberVisual>();
            effect.transform.SetParent(target.transform, false);
            effect.target = target;
            target.Died += effect.Remove;
            effect.MakeParticles(particleMaterial);
            return effect;
        }

        void MakeParticles(Material material)
        {
            if (material == null) return;
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.24f, .55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.06f, .20f);
            main.startSize = new ParticleSystem.MinMaxCurve(.06f, .12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, .25f, .07f, .80f), new Color(1f, .67f, .18f, .77f));
            main.maxParticles = 16;
            var emission = particles.emission;
            emission.rateOverTime = 10f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.position = new Vector3(0f, .48f, 0f);
            shape.radius = .37f;
            if (target.TryGetComponent(out CharacterController body))
            {
                shape.position = body.center;
                shape.radius = body.radius * 1.35f;
            }
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = .45f;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .18f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 3;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            particles.Emit(new ParticleSystem.EmitParams { applyShapeToPosition = true }, 4);
        }

        internal void Pulse(Vector3 center, float radius)
        {
            if (wave == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                waveMaterial = new Material(shader);
                var root = new GameObject("Ember splash ring");
                root.transform.SetParent(transform, false);
                wave = root.AddComponent<LineRenderer>();
                wave.sharedMaterial = waveMaterial;
                wave.loop = true;
                wave.useWorldSpace = true;
                wave.sortingOrder = 2;
                wave.alignment = LineAlignment.TransformZ;
                root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                wave.shadowCastingMode = ShadowCastingMode.Off;
                wave.receiveShadows = false;
                wave.positionCount = points.Length;
            }

            waveCenter = center + Vector3.up * .038f;
            waveRadius = radius;
            waveStarted = Time.timeAsDouble;
            wave.enabled = true;
            DrawWave(0f);
        }

        void Update()
        {
            if (wave == null || !wave.enabled) return;
            float age = (float)(Time.timeAsDouble - waveStarted);
            if (age >= WaveDuration)
            {
                wave.enabled = false;
                if (IsFinishing) Remove();
                return;
            }
            DrawWave(age);
        }

        void DrawWave(float age)
        {
            float progress = age / WaveDuration;
            float currentRadius = Mathf.Lerp(waveRadius * .22f, waveRadius, Mathf.Clamp01(progress * 3f));
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * (2f * Mathf.PI / points.Length);
                points[i] = waveCenter + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * currentRadius;
            }

            wave.SetPositions(points);
            wave.widthMultiplier = Mathf.Lerp(.075f, .018f, progress);
            wave.startColor = wave.endColor = new Color(1f, .45f, .12f, .78f * (1f - progress));
        }

        internal void Finish()
        {
            IsFinishing = true;
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (wave == null || !wave.enabled) Remove();
        }

        internal void Remove()
        {
            if (removed) return;
            removed = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (target != null) target.Died -= Remove;
            if (waveMaterial != null) Destroy(waveMaterial);
        }
    }
}
