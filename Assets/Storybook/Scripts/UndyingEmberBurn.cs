using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    internal sealed class UndyingEmberBurn : MonoBehaviour
    {
        const double TimeTolerance = .00001d;

        MonsterHealth target;
        PlayerHealth owner;
        UndyingEmberRelic relic;
        UndyingEmberVisual visual;
        double nextTickAt, expiresAt;

        internal static void Apply(PlayerHealth owner, MonsterHealth target, UndyingEmberRelic relic)
        {
            if (!target.CanTakeDamage) return;
            if (!target.TryGetComponent(out UndyingEmberBurn burn))
                burn = target.gameObject.AddComponent<UndyingEmberBurn>();

            double now = Time.timeAsDouble;
            if (burn.owner != null && now > burn.expiresAt) burn.Advance(now);
            if (!target.CanTakeDamage) return;

            if (burn.owner == null)
            {
                burn.target = target;
                burn.owner = owner;
                burn.relic = relic;
                burn.nextTickAt = now + relic.TickInterval;
                target.Died += burn.Stop;
                burn.enabled = true;
                if (burn.visual != null) burn.visual.Remove();
                burn.visual = UndyingEmberVisual.Create(target, relic.EmberParticles);
            }

            burn.expiresAt = now + relic.Duration;
        }

        void Update()
        {
            if (owner == null || owner.IsDead || !owner.Relics.Contains(UndyingEmberRelic.RelicId))
            {
                Stop();
                return;
            }
            if (Time.timeScale > 0f) Advance(Time.timeAsDouble);
        }

        void Advance(double now)
        {
            while (nextTickAt <= expiresAt + TimeTolerance && System.Math.Min(nextTickAt, expiresAt) <= now)
            {
                if (!target.CanTakeDamage) { Stop(); return; }
                nextTickAt += relic.TickInterval;
                Tick();
                if (owner == null) return;
            }

            if (now >= expiresAt)
            {
                // Let a wave from the final tick fade, while immediately ending the burn.
                if (visual != null) visual.Finish();
                Stop();
            }
        }

        void Tick()
        {
            var source = target;
            var scene = source.gameObject.scene;
            Vector3 center = source.transform.position;
            float radius = relic.SplashRadius;
            float splash = relic.SplashDamage;
            target.ReceiveDamage(relic.TargetDamage);

            // A lethal tick can still apply its splash. Death cancels every subsequent tick.
            if (visual != null && !target.IsDead && radius > 0f)
                visual.Pulse(center, radius);

            if (radius <= 0f || splash <= 0f) return;
            float radiusSquared = radius * radius;
            foreach (var other in FindObjectsByType<MonsterHealth>())
            {
                if (other == null || other == source || !other.CanTakeDamage || other.gameObject.scene != scene)
                    continue;
                Vector3 offset = other.transform.position - center;
                if (offset.x * offset.x + offset.z * offset.z <= radiusSquared)
                    other.ReceiveDamage(splash);
            }
        }

        void Stop() => enabled = false;

        void OnDisable()
        {
            if (target != null) target.Died -= Stop;
            owner = null;
            if (visual != null && !visual.IsFinishing)
            {
                visual.Remove();
                visual = null;
            }
        }
    }
}
