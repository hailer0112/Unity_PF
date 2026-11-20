using System;
using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    public sealed class SwordTyphoon : MonoBehaviour
    {
        const float TraceHeight = .35f;
        const float CenterClearance = .35f;
        static readonly RaycastHit[] ObstacleHits = new RaycastHit[16];

        readonly Collider[] overlapHits = new Collider[32];
        readonly HashSet<MonsterHealth> targets = new HashSet<MonsterHealth>();
        Transform owner;
        SwordTyphoonVisual visuals;
        ClearingCamera hitCamera;
        LayerMask targetMask;
        double startedAt;
        float radius, duration, interval, tickDamage, burstDamage, pullSpeed;
        int nextTick = 1, tickCount;

        public static SwordTyphoon Create(Transform owner, Vector3 center, float radius, float duration,
            float interval, int tickCount, float tickDamage, float burstDamage, float pullSpeed, LayerMask targetMask,
            Sprite[] groundFrames = null, ClearingCamera camera = null)
        {
            var root = new GameObject("Sword Typhoon");
            root.transform.position = center;
            var vortex = root.AddComponent<SwordTyphoon>();
            vortex.owner = owner;
            vortex.hitCamera = camera;
            vortex.radius = radius;
            vortex.duration = duration;
            vortex.interval = interval;
            vortex.tickCount = tickCount;
            vortex.tickDamage = tickDamage;
            vortex.burstDamage = burstDamage;
            vortex.pullSpeed = pullSpeed;
            vortex.targetMask = targetMask;
            vortex.startedAt = Time.timeAsDouble;
            vortex.visuals = root.AddComponent<SwordTyphoonVisual>();
            vortex.visuals.Configure(radius, duration, groundFrames);
            return vortex;
        }

        void LateUpdate()
        {
            double elapsed = Time.timeAsDouble - startedAt;
            double activeElapsed = Math.Min(elapsed, duration);
            CollectTargets();
            // Catch up due ticks after a slow frame, including the tick at expiry.
            while (nextTick <= tickCount && nextTick * (double)interval <= activeElapsed + .00001d)
            {
                nextTick++;
                DamageTargets(tickDamage);
                if (!isActiveAndEnabled) return;
                visuals.Pulse(false);
            }

            if (elapsed >= duration)
            {
                DamageTargets(burstDamage);
                if (!isActiveAndEnabled) return;
                visuals.Pulse(true);
                enabled = false;
                Destroy(gameObject, SwordTyphoonVisual.BurstLifetime);
                return;
            }

            PullTargets();
            visuals.Spin((float)elapsed);
        }

        void CollectTargets()
        {
            targets.Clear();
            Vector3 center = transform.position;
            Vector3 scanCenter = center + Vector3.up * TraceHeight;
            float scanRadius = radius + .5f;
            int count = Physics.OverlapSphereNonAlloc(scanCenter, scanRadius, overlapHits, targetMask,
                QueryTriggerInteraction.Collide);
            Collider[] hits = overlapHits;
            if (count == hits.Length)
            {
                hits = Physics.OverlapSphere(scanCenter, scanRadius, targetMask, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }

            for (int i = 0; i < count; i++)
            {
                var enemy = AttackTarget.FromDamageCollider(hits[i], out bool useSurface) as MonsterHealth;
                if (enemy == null || !enemy.CanTakeDamage || targets.Contains(enemy)) continue;
                Vector3 enemyGround = useSurface ? hits[i].ClosestPoint(scanCenter) : enemy.transform.position;
                enemyGround.y = center.y;
                Vector3 offset = enemyGround - center;
                if (offset.sqrMagnitude <= radius * radius && ObstacleDistance(owner, center, enemyGround) >= offset.magnitude)
                    targets.Add(enemy);
            }
        }

        void DamageTargets(float damage)
        {
            foreach (MonsterHealth enemy in targets)
            {
                if (!isActiveAndEnabled) return;
                if (enemy != null && enemy.CanTakeDamage) enemy.ReceiveSkillDamage(damage, transform.position, SkillHit.SwordTyphoon, camera: hitCamera);
            }
        }

        void PullTargets()
        {
            foreach (MonsterHealth enemy in targets)
            {
                if (enemy == null || !enemy.CanTakeDamage ||
                    !enemy.TryGetComponent(out CharacterController body) || !body.enabled) continue;

                // The crab's warning and contact footprint are fixed once its claw attack starts.
                if (enemy.TryGetComponent(out CrabSoldierAI crab) && crab.isActiveAndEnabled &&
                    (crab.CurrentState == CrabSoldierAI.State.Windup || crab.CurrentState == CrabSoldierAI.State.Slam))
                    continue;

                Vector3 offset = Vector3.ProjectOnPlane(transform.position - enemy.transform.position, Vector3.up);
                float step = Mathf.Min(pullSpeed * Time.deltaTime, offset.magnitude - CenterClearance);
                if (step > 0f) body.Move(offset.normalized * step);
            }
        }

        public static Vector3 LimitPlacement(Transform owner, Vector3 origin, Vector3 target)
        {
            Vector3 offset = target - origin;
            float distance = ObstacleDistance(owner, origin, target);
            return distance < offset.magnitude
                ? origin + offset.normalized * Mathf.Max(0f, distance - .15f)
                : target;
        }

        static float ObstacleDistance(Transform owner, Vector3 origin, Vector3 target)
        {
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            if (distance <= .0001f) return distance;

            Vector3 start = origin + Vector3.up * TraceHeight;
            Vector3 direction = offset / distance;
            RaycastHit[] hits = ObstacleHits;
            int count = Physics.RaycastNonAlloc(start, direction, hits, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(start, direction, distance, ~0, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i].collider;
                if (collider.transform.IsChildOf(owner) || collider.GetComponentInParent<AttackTarget>() != null) continue;
                distance = Mathf.Min(distance, hits[i].distance);
            }
            return distance;
        }

        public void Cancel()
        {
            enabled = false;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
