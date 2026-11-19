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

        [SerializeField] Sprite[] windstepFrames = new Sprite[20];
        const float WindstepFrameDuration = .12f;
        float windstepMotionTime;

        InputAction windstepInput;
        double windstepEndsAt, windstepReadyAt;
        bool windstepVisualActive;
        WindstepVisual windstepVisual;

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
                EnterCombatStance();
                double now = Time.timeAsDouble;
                windstepMotionTime = 0f;
                windstepEndsAt = now + windstepDuration;
                windstepReadyAt = now + windstepCooldown;
                BeginWindstepVisual();
            }

            if (IsWindstepActive) return;

            if (windstepVisualActive)
            {
                windstepVisual.End();
                windstepVisualActive = false;
            }

        }

        Sprite WindstepMotionPose(Vector3 velocity)
        {
            int frame = (int)(windstepMotionTime / WindstepFrameDuration);
            if (WindstepTimeRemaining <= WindstepFrameDuration)
                frame = 4;
            else if (frame >= 4)
                return null;

            windstepMotionTime += Time.deltaTime;
            int direction = Mathf.Abs(velocity.x) > Mathf.Abs(velocity.z)
                ? (velocity.x > 0f ? 0 : 1) : (velocity.z > 0f ? 3 : 2);
            return windstepFrames[direction * 5 + frame];
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

        void ClearWindstep()
        {
            windstepEndsAt = 0d;
            windstepVisualActive = false;
            if (windstepVisual != null) windstepVisual.Clear();
        }
    }
}
