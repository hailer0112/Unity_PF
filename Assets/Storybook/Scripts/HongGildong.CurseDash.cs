using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        const float DashBodyQueryPadding = .12f;
        const float DashLandingClearance = .01f;
        const float DashLandingStep = .04f;
        const float DashLandingSearchRadius = .9f;
        const int DashLandingDirections = 16;

        readonly Collider[] dashBodies = new Collider[24];
        readonly HashSet<Collider> ignoredDashBodies = new();
        bool curseDashActive;
        Vector3? lastClearDashPosition;
        PalaceDungeonRoom curseDashRoom;

        void GetDashCapsule(out Vector3 bottom, out Vector3 top, out float radius)
        {
            Vector3 scale = transform.lossyScale;
            radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float axis = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * .5f - radius);
            Vector3 center = transform.TransformPoint(controller.center);
            Vector3 end = transform.up * axis;
            bottom = center - end;
            top = center + end;
        }

        void BeginCurseDash()
        {
            if (!TryGetComponent<PlayerHealth>(out var health) ||
                health.Relics.Find<CursedFootstepsRelic>() == null || !TryGetComponent(out cursedFootsteps)) return;

            curseDashActive = true;
            cursedFootsteps.BeginDash();
            curseDashRoom = cursedFootsteps.DashRoom;
            if (!OverlapsDashBody(Vector3.zero)) lastClearDashPosition = transform.position;
        }

        void IgnoreDashBodies(float step)
        {
            GetDashCapsule(out var bottom, out var top, out float radius);
            // Controller sweeps can make contact before a query capsule touches the smaller
            // query shape, so select body pairs slightly ahead of the movement step.
            int count = DashOverlaps(bottom, top, radius + step + DashBodyQueryPadding, out var hits);
            for (int i = 0; i < count; i++)
            {
                var body = hits[i];
                if (!IsDashBody(body) || ignoredDashBodies.Contains(body) ||
                    Physics.GetIgnoreCollision(controller, body)) continue;
                Physics.IgnoreCollision(controller, body, true);
                ignoredDashBodies.Add(body);
            }
        }

        static bool IsDashBody(Collider collider)
        {
            // The regular enemies move with their root controller. The Dragon King's root box
            // is his body, separate from the child hurtboxes, dais, and room barriers.
            if (!collider.TryGetComponent<MonsterHealth>(out var monster) || monster.IsDead) return false;
            return collider is CharacterController && monster.TryGetComponent<MonsterCombatAI>(out _) ||
                collider is BoxCollider && monster.TryGetComponent<DragonKingPhase>(out _);
        }

        int DashOverlaps(Vector3 bottom, Vector3 top, float radius, out Collider[] hits)
        {
            hits = dashBodies;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            if (count < hits.Length) return count;
            hits = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            return hits.Length;
        }

        bool OverlapsDashBody(Vector3 offset)
        {
            GetDashCapsule(out var bottom, out var top, out float radius);
            int count = DashOverlaps(bottom + offset, top + offset, radius + DashBodyQueryPadding, out var hits);
            Vector3 center = (bottom + top) * .5f + offset;
            for (int i = 0; i < count; i++)
            {
                Collider body = hits[i];
                if (!IsDashBody(body)) continue;

                Vector3 closest;
                float clearance = radius + DashLandingClearance;
                if (body is CharacterController character)
                {
                    Vector3 scale = character.transform.lossyScale;
                    clearance += character.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    closest = character.transform.TransformPoint(character.center);
                }
                else
                {
                    Bounds bounds = body.bounds;
                    closest = body.ClosestPoint(new Vector3(center.x, bounds.center.y, center.z));
                }
                float x = center.x - closest.x;
                float z = center.z - closest.z;
                if (x * x + z * z < clearance * clearance) return true;
            }
            return false;
        }

        void EndCurseDash(bool separate)
        {
            if (!curseDashActive) return;
            try
            {
                if (separate && controller.enabled && OverlapsDashBody(Vector3.zero))
                    SeparateDashLanding();
            }
            finally
            {
                foreach (var body in ignoredDashBodies)
                    if (body != null && controller != null && Physics.GetIgnoreCollision(controller, body))
                        Physics.IgnoreCollision(controller, body, false);
                ignoredDashBodies.Clear();
                System.Array.Clear(dashBodies, 0, dashBodies.Length);
                curseDashActive = false;
                curseDashRoom = null;
                lastClearDashPosition = null;
                cursedFootsteps.FinishDash();
            }
        }

        internal void CancelCurseDashForRoomChange()
        {
            if (curseDashActive)
            {
                dashTimeLeft = 0f;
                EndCurseDash(true);
            }
            cursedFootsteps?.Clear();
        }

        void SeparateDashLanding()
        {
            Vector3 start = transform.position;
            Vector3 fallback = lastClearDashPosition.HasValue
                ? Vector3.ProjectOnPlane(lastClearDashPosition.Value - start, Vector3.up)
                : Vector3.zero;
            float limit = DashLandingSearchRadius;
            if (CanLandDash(start, fallback)) limit = Mathf.Min(limit, fallback.magnitude);

            // Search close to the desired end first. The last clear point is a fallback if
            // walls or other actors surround the desired end.
            for (float distance = DashLandingStep; distance < limit; distance += DashLandingStep)
            for (int side = 0; side < DashLandingDirections; side++)
            {
                var direction = Quaternion.AngleAxis(side * (360f / DashLandingDirections), Vector3.up) * dashDirection;
                Vector3 offset = direction * distance;
                if (!CanLandDash(start, offset)) continue;
                controller.Move(offset);
                if (!OverlapsDashBody(Vector3.zero)) return;
                MoveToLastClearDashPosition();
                return;
            }
            MoveToLastClearDashPosition();
        }

        void MoveToLastClearDashPosition()
        {
            if (!lastClearDashPosition.HasValue) return;
            Vector3 start = transform.position;
            Vector3 offset = Vector3.ProjectOnPlane(lastClearDashPosition.Value - start, Vector3.up);
            if (CanLandDash(start, offset)) controller.Move(offset);
        }

        bool CanLandDash(Vector3 start, Vector3 offset)
        {
            float distance = offset.magnitude;
            if (distance < .001f) return false;
            Vector3 position = start + offset;
            if ((InsideClearing(position) - position).sqrMagnitude > .000001f ||
                curseDashRoom != null && curseDashRoom.InCombat && !curseDashRoom.Contains(position) ||
                OverlapsDashBody(offset)) return false;

            GetDashCapsule(out var bottom, out var top, out float radius);
            // Use the capsule's inner skin so a resting contact with the floor or a wall
            // still permits an escape along that surface.
            radius = Mathf.Max(.01f, radius - controller.skinWidth);
            Vector3 direction = offset / distance;
            var hits = dashBodyHits;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, direction, hits, distance, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                hits = Physics.CapsuleCastAll(bottom, top, radius, direction, distance, ~0,
                    QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider != controller && !ignoredDashBodies.Contains(hit.collider) &&
                    !Physics.GetIgnoreCollision(controller, hit.collider) &&
                    Vector3.Dot(direction, hit.normal) < -.001f)
                    return false;
            }
            return true;
        }
    }
}
