using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    public sealed class WindstepVisual : MonoBehaviour
    {
        static readonly Color WindColor = new Color(.48f, .93f, 1f, .8f);
        static readonly Color TrailColor = new Color(.25f, .8f, 1f, .65f);

        ParticleSystem particles;
        Material material;
        Vector3 previousPosition;
        float emitIn;
        bool emitting;

        public static WindstepVisual Create(Transform owner)
        {
            var root = new GameObject("Windstep Particles");
            root.transform.SetParent(owner, false);
            var effect = root.AddComponent<WindstepVisual>();
            effect.Configure();
            return effect;
        }

        void Configure()
        {
            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 38;
            main.startSpeed = 0f;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = GetComponent<ParticleSystemRenderer>();
            var shader = Resources.Load<Shader>("WindstepParticle");
            material = new Material(shader);
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = .05f;
            renderer.sortingOrder = 3;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        public void Begin()
        {
            previousPosition = transform.position;
            emitIn = 0f;
            emitting = true;
            particles.Play();
            for (int i = 0; i < 14; i++)
            {
                float angle = i * Mathf.PI * 2f / 14;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Emit(previousPosition + Vector3.up * .10f + outward * .11f,
                    outward * 2.4f + Vector3.up * .16f, .38f, .16f, WindColor);
            }
        }

        void LateUpdate()
        {
            if (!emitting) return;

            Vector3 position = transform.position;
            Vector3 travel = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
            previousPosition = position;
            emitIn -= Time.deltaTime;
            if (emitIn > 0f) return;

            emitIn = .11f;
            Vector2 circle = Random.insideUnitCircle * .23f;
            Emit(position + new Vector3(circle.x, .13f, circle.y),
                new Vector3(-circle.y, .26f, circle.x) * 2f, .32f, .11f, WindColor);
            if (travel.sqrMagnitude > .00001f)
            {
                Vector3 back = -travel.normalized;
                Emit(position + Vector3.up * .12f + back * .19f, back * .9f, .36f, .15f, TrailColor);
            }
        }

        void Emit(Vector3 position, Vector3 velocity, float life, float size, Color color)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = life,
                startSize = size,
                startColor = color
            };
            particles.Emit(particle, 1);
        }

        public void End()
        {
            emitting = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Clear()
        {
            emitting = false;
            particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
