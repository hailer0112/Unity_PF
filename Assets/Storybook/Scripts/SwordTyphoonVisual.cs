using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    public sealed class SwordTyphoonVisual : MonoBehaviour
    {
        public const float BurstLifetime = .48f;
        const float FullTurn = 2f * Mathf.PI;
        static readonly Color Aqua = new Color(.19f, .91f, .9f, .78f);
        static readonly Color Ice = new Color(.75f, .95f, 1f, .85f);

        readonly LineRenderer[] spirals = new LineRenderer[2], blades = new LineRenderer[3];
        Material lineMaterial, particleMaterial;
        LineRenderer ring;
        ParticleSystem particles;
        float radius;

        public void Configure(float effectRadius)
        {
            radius = effectRadius;
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
            ring = MakeLine("Typhoon Ground Ring", 64, .034f, Aqua, Aqua, true);
            for (int i = 0; i < ring.positionCount; i++)
                ring.SetPosition(i, Polar(FullTurn * i / ring.positionCount, radius, .035f));

            for (int i = 0; i < spirals.Length; i++)
                spirals[i] = MakeLine("Rising Wind", 32, .065f,
                    new Color(.22f, .86f, 1f, .32f), new Color(.45f, .92f, 1f, .02f));
            for (int i = 0; i < blades.Length; i++)
                blades[i] = MakeLine("Orbiting Wind Blade", 12, .095f,
                    new Color(.31f, .85f, 1f, .06f), Ice);

            particles = gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.7f, .4f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            particleMaterial = new Material(Resources.Load<Shader>("WindstepParticle"));
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.3f;
            renderer.velocityScale = .04f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
            Spin(0f);
        }

        LineRenderer MakeLine(string label, int points, float width, Color start, Color end, bool loop = false)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.startWidth = width;
            line.endWidth = loop ? width : width * .25f;
            line.startColor = start;
            line.endColor = end;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void Spin(float elapsed)
        {
            for (int i = 0; i < spirals.Length; i++)
            {
                LineRenderer line = spirals[i];
                for (int j = 0; j < line.positionCount; j++)
                {
                    float t = j / (line.positionCount - 1f);
                    line.SetPosition(j, Polar(elapsed * 3.8f + i * Mathf.PI - t * FullTurn,
                        radius * Mathf.Lerp(.9f, .37f, t), .08f + t * 2.05f));
                }
            }

            for (int i = 0; i < blades.Length; i++)
            {
                LineRenderer line = blades[i];
                for (int j = 0; j < line.positionCount; j++)
                {
                    float t = j / (line.positionCount - 1f);
                    line.SetPosition(j, Polar(elapsed * 5.6f + i * FullTurn / blades.Length + t * .85f,
                        radius * (.68f + .15f * Mathf.Sin(t * Mathf.PI)), .35f + i * .22f + t * .18f));
                }
            }
        }

        public void Pulse(bool burst)
        {
            int count = burst ? 26 : 7;
            float phase = Random.value * FullTurn;
            for (int i = 0; i < count; i++)
            {
                float angle = phase + FullTurn * i / count;
                Vector3 direction = Polar(angle, 1f, 0f);
                Vector3 tangent = Vector3.Cross(Vector3.up, direction);
                var particle = new ParticleSystem.EmitParams
                {
                    position = transform.position + direction * radius * (burst ? .27f : .7f) + Vector3.up * .17f,
                    velocity = burst ? direction * (radius * 2.35f) + Vector3.up * (i % 3 + .7f)
                        : tangent * 3.2f + Vector3.up * 1.1f,
                    startLifetime = burst ? BurstLifetime : .26f,
                    startSize = burst ? .39f : .2f,
                    startColor = i % 3 == 0 ? Ice : Aqua
                };
                particles.Emit(particle, 1);
            }

            if (!burst) return;
            ring.enabled = false;
            foreach (LineRenderer line in spirals) line.enabled = false;
            foreach (LineRenderer line in blades) line.enabled = false;
        }

        static Vector3 Polar(float angle, float distance, float height) =>
            new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);

        void OnDestroy()
        {
            if (lineMaterial != null) Destroy(lineMaterial);
            if (particleMaterial != null) Destroy(particleMaterial);
        }
    }
}
