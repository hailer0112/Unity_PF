using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DisallowMultipleComponent]
    internal sealed class BloodTemptationPoison : MonoBehaviour
    {
        const double Duration = 3d;
        const double Interval = .5d;
        static readonly Color32 Mote = new(178, 110, 237, 171);
        static readonly Color32 TickMote = new(213, 148, 250, 217);

        MonsterHealth target;
        PlayerHealth owner;
        ParticleSystem particles;
        double nextTickAt, expiresAt;

        internal float TickDamage => Mathf.Min(target.MaxHP * .05f, 5f);

        internal static void Apply(PlayerHealth owner, MonsterHealth target, Material particleMaterial)
        {
            if (!target.CanTakeDamage) return;
            if (!target.TryGetComponent(out BloodTemptationPoison poison))
                poison = target.gameObject.AddComponent<BloodTemptationPoison>();

            double now = Time.timeAsDouble;
            if (poison.owner != null && now > poison.expiresAt) poison.Advance(now);
            if (!target.CanTakeDamage) return;

            if (poison.owner == null)
            {
                poison.owner = owner;
                poison.target = target;
                poison.nextTickAt = now + Interval;
                target.Died += poison.Stop;
                poison.enabled = true;
                poison.StartParticles(particleMaterial);
            }

            // Refresh the expiration without rescheduling the next tick.
            poison.expiresAt = now + Duration;
        }

        void Update()
        {
            if (owner == null || owner.IsDead || !owner.Relics.Contains(BloodTemptationRelic.RelicId))
            {
                Stop();
                return;
            }

            if (Time.timeScale > 0f) Advance(Time.timeAsDouble);
        }

        void Advance(double now)
        {
            while (nextTickAt <= now && nextTickAt <= expiresAt)
            {
                nextTickAt += Interval;
                float hp = target.CurrentHP;
                // Directionless damage goes through MonsterHealth without the frontal shield.
                target.ReceiveDamage(TickDamage);
                if (owner == null) return; // Death cleans up synchronously.
                if (target.CurrentHP < hp && particles != null)
                    particles.Emit(new ParticleSystem.EmitParams { startColor = TickMote, applyShapeToPosition = true }, 4);
            }

            if (now >= expiresAt) Stop();
        }

        void StartParticles(Material material)
        {
            if (material == null) return;
            var visual = new GameObject("Blood Temptation poison particles");
            visual.transform.SetParent(transform, false);
            particles = visual.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.36f, .68f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.04f, .16f);
            main.startSize = new ParticleSystem.MinMaxCurve(.065f, .105f);
            main.startColor = (Color)Mote;
            main.maxParticles = 18;

            var emission = particles.emission;
            emission.rateOverTime = 8f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            if (TryGetComponent(out CharacterController body))
            {
                shape.position = body.center;
                shape.radius = body.radius * 1.4f;
                shape.scale = new Vector3(1f, 1.25f, 1f);
            }
            else
            {
                shape.position = Vector3.up * .70f;
                shape.radius = .55f;
            }

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = .35f;
            velocity.space = ParticleSystemSimulationSpace.World;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(.15f, 0f), new GradientAlphaKey(1f, .22f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(fade);

            var renderer = visual.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 3;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            particles.Play();
        }

        void Stop() => enabled = false;

        void OnDisable()
        {
            if (target != null) target.Died -= Stop;
            owner = null;
            if (particles == null) return;
            particles.gameObject.SetActive(false);
            Destroy(particles.gameObject);
            particles = null;
        }
    }
}
