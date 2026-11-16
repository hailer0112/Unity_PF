using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        [Header("E - Windstep")]
        [SerializeField, Min(.01f)] float windstepDuration = 4f;
        [SerializeField, Min(0f)] float windstepSpeedBonus = .4f;
        [SerializeField, Min(0f)] float windstepCooldown = 12f;

        InputAction windstepInput;
        double windstepEndsAt, windstepReadyAt;
        bool windstepVisualActive;
        WindstepVisual windstepVisual;
        readonly List<Collider> windstepBodies = new List<Collider>();
        readonly Collider[] windstepScan = new Collider[32];

        public bool IsWindstepActive => isActiveAndEnabled && Time.timeAsDouble < windstepEndsAt;
        public float WindstepTimeRemaining => IsWindstepActive ? (float)(windstepEndsAt - Time.timeAsDouble) : 0f;
        public float WindstepCooldownRemaining => (float)System.Math.Max(0d, windstepReadyAt - Time.timeAsDouble);
        public float EffectiveWalkSpeed => walkSpeed * (IsWindstepActive ? 1f + windstepSpeedBonus : 1f);

        void InitializeWindstepInput() =>
            windstepInput = new InputAction("Windstep", InputActionType.Button, "<Keyboard>/e");

        void UpdateWindstep(bool acceptInput)
        {
            if (acceptInput && !IsCastingSwordTyphoon && windstepInput.WasPressedThisFrame() && !IsWindstepActive &&
                WindstepCooldownRemaining <= 0f && TrySpendSkillMana(HudSkill.Windstep, windstepManaCost))
            {
                double now = Time.timeAsDouble;
                windstepEndsAt = now + windstepDuration;
                windstepReadyAt = now + windstepCooldown;
                BeginWindstepVisual();
            }

            if (IsWindstepActive)
            {
                IgnoreNearbyMonsterBodies();
                return;
            }

            if (windstepVisualActive)
            {
                windstepVisual.End();
                windstepVisualActive = false;
            }

            RestoreWindstepBodies(false);
        }

        internal double SuspendForFloorTransition()
        {
            double windstepExpiry = windstepEndsAt;
            enabled = false;
            return windstepExpiry;
        }

        internal void ResumeFromFloorTransition(double windstepExpiry)
        {
            enabled = true;
            if (windstepExpiry <= Time.timeAsDouble) return;
            windstepEndsAt = windstepExpiry;
            BeginWindstepVisual();
        }

        void BeginWindstepVisual()
        {
            if (windstepVisual == null) windstepVisual = WindstepVisual.Create(transform);
            windstepVisual.Begin();
            windstepVisualActive = true;
        }

        void IgnoreNearbyMonsterBodies()
        {
            if (!controller.enabled) return;

            // Include the player's possible walk or dash step and nearby body extents.
            float movementSpeed = Mathf.Max(EffectiveWalkSpeed, dashDistance / dashDuration);
            float radius = 2f + movementSpeed * Time.deltaTime;
            Vector3 center = transform.TransformPoint(controller.center);
            int count = Physics.OverlapSphereNonAlloc(center, radius, windstepScan, ~0, QueryTriggerInteraction.Ignore);
            Collider[] nearby = windstepScan;
            if (count == nearby.Length)
            {
                nearby = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
                count = nearby.Length;
            }

            for (int i = 0; i < count; i++)
            {
                Collider candidate = nearby[i];
                if (candidate == controller || Physics.GetIgnoreCollision(controller, candidate)) continue;

                MonsterHealth monster = candidate.GetComponentInParent<MonsterHealth>();
                if (monster == null || monster.IsDead) continue;

                Physics.IgnoreCollision(controller, candidate, true);
                if (!windstepBodies.Contains(candidate)) windstepBodies.Add(candidate);
            }
        }

        void RestoreWindstepBodies(bool force)
        {
            if (windstepBodies.Count == 0) return;

            Collider[] overlaps = null;
            if (!force && controller.enabled)
            {
                Vector3 center = transform.TransformPoint(controller.center);
                Vector3 scale = transform.lossyScale;
                float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float radius = controller.radius * horizontalScale;
                float axis = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * .5f - radius);
                Vector3 end = transform.up * axis;
                // Retain only pairs still touching the controller's padded capsule.
                overlaps = Physics.OverlapCapsule(center - end, center + end,
                    radius + controller.skinWidth * horizontalScale, ~0, QueryTriggerInteraction.Ignore);
            }

            for (int i = windstepBodies.Count - 1; i >= 0; i--)
            {
                Collider other = windstepBodies[i];
                if (other != null && overlaps != null && System.Array.IndexOf(overlaps, other) >= 0) continue;

                if (controller != null && other != null && Physics.GetIgnoreCollision(controller, other))
                    Physics.IgnoreCollision(controller, other, false);
                windstepBodies.RemoveAt(i);
            }
        }

        void ClearWindstep()
        {
            windstepEndsAt = 0d;
            windstepVisualActive = false;
            if (windstepVisual != null) windstepVisual.Clear();
            RestoreWindstepBodies(true);
        }
    }
}
