using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    public sealed class FishArcherBolt : MonoBehaviour
    {
        // Hong Gildong is on Ignore Raycast, so combat queries include every layer.
        static readonly RaycastHit[] CastHits = new RaycastHit[16];
        static readonly Collider[] OverlapHits = new Collider[8];
        const float TrailAlignmentDistance = .9f;
        float speed, remaining, radius, damage;
        LineRenderer trail;
        Vector3 initialTrailOffset;
        float trailOffsetWeight;

        public static void Launch(Vector3 origin, Vector3 direction, float speed, float range,
            float radius, float damage, Material material, Vector3 visualOffset = default)
        {
            var root = new GameObject("Fish Archer Water Bolt");
            root.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            var bolt = root.AddComponent<FishArcherBolt>();
            bolt.speed = speed;
            bolt.remaining = range;
            bolt.radius = radius;
            bolt.damage = damage;

            var line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 2;
            bolt.trail = line;
            bolt.initialTrailOffset = root.transform.InverseTransformVector(visualOffset);
            bolt.trailOffsetWeight = visualOffset.sqrMagnitude > 0f ? 1f : 0f;
            bolt.UpdateTrail();
            line.startWidth = .025f;
            line.endWidth = .105f;
            line.startColor = new Color(.14f, .55f, .88f, .30f);
            line.endColor = new Color(.62f, .97f, 1f, 1f);
            line.numCapVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 3;
        }

        void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remaining);
            if (step <= 0f) return;

            float impact = 0f;
            if (TouchesObstacle(out PlayerHealth player) ||
                Cast(transform.position, transform.forward, radius, step, out impact, out player))
            {
                transform.position += transform.forward * impact;
                if (player != null) player.TryTakeDamage(damage);
                enabled = false;
                Destroy(gameObject);
                return;
            }

            transform.position += transform.forward * step;
            remaining -= step;
            if (trailOffsetWeight > 0f)
            {
                trailOffsetWeight = Mathf.Max(0f, trailOffsetWeight - step / TrailAlignmentDistance);
                UpdateTrail();
            }
            if (remaining <= .0001f) Destroy(gameObject);
        }

        void UpdateTrail()
        {
            Vector3 offset = initialTrailOffset * trailOffsetWeight;
            trail.SetPosition(0, offset + Vector3.back * .32f);
            trail.SetPosition(1, offset + Vector3.forward * .025f);
        }

        bool TouchesObstacle(out PlayerHealth player)
        {
            Collider[] hits = OverlapHits;
            int count = Physics.OverlapSphereNonAlloc(transform.position, radius, hits, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.OverlapSphere(transform.position, radius, ~0,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            player = null;
            for (int i = 0; i < count; i++)
            {
                Collider collider = hits[i];
                if (collider.GetComponentInParent<AttackTarget>() != null) continue;
                player = collider.GetComponentInParent<PlayerHealth>();
                if (player == null) return true;
            }
            return player != null;
        }

        internal static bool Cast(Vector3 origin, Vector3 direction, float radius, float distance,
            out float impact, out PlayerHealth player, bool ignorePlayer = false)
        {
            RaycastHit[] hits = CastHits;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, distance,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.SphereCastAll(origin, radius, direction, distance,
                    ~0, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }

            impact = distance;
            player = null;
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.distance > impact || hit.collider.GetComponentInParent<AttackTarget>() != null) continue;
                PlayerHealth candidate = hit.collider.GetComponentInParent<PlayerHealth>();
                if (candidate != null && (ignorePlayer || (blocked && hit.distance == impact))) continue;
                impact = hit.distance;
                player = candidate;
                blocked = true;
            }
            return blocked;
        }
    }
}
