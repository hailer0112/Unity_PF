using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        [Header("R - Sword Typhoon")]
        [SerializeField, Min(.01f)] float swordTyphoonCastRange = 6f;
        [SerializeField, Min(.01f)] float swordTyphoonRadius = 3f;
        [SerializeField, Min(.01f)] float swordTyphoonDuration = 3f;
        [SerializeField, Min(.01f)] float swordTyphoonTickInterval = .5f;
        [SerializeField, Min(0)] int swordTyphoonTickCount = 6;
        [SerializeField, Min(0f)] float swordTyphoonTickDamageMultiplier = .5f;
        [SerializeField, Min(0f)] float swordTyphoonBurstDamageMultiplier = 2.5f;
        [SerializeField, Min(0f)] float swordTyphoonCooldown = 30f;
        [SerializeField, Min(0f)] float swordTyphoonCastTime = .4f;
        [SerializeField, Min(0f)] float swordTyphoonPullSpeed = 2.6f;

        [SerializeField] Sprite[] swordTyphoonCastFrames = new Sprite[16];
        [SerializeField] Sprite[] swordTyphoonGroundFrames = new Sprite[8];

        InputAction swordTyphoonInput;
        Vector3 swordTyphoonCenter;
        double swordTyphoonStartedAt, swordTyphoonReadyAt;
        readonly List<SwordTyphoon> swordTyphoons = new List<SwordTyphoon>();

        public bool IsCastingSwordTyphoon { get; private set; }
        public float SwordTyphoonCooldownRemaining =>
            (float)System.Math.Max(0d, swordTyphoonReadyAt - Time.timeAsDouble);

        bool BeginSwordTyphoon(Vector2 cursor)
        {
            if (IsCastingSwordTyphoon || IsCastingAzureSlash || IsCastingCloneSlash || IsDashing ||
                Attacking || SwordTyphoonCooldownRemaining > 0f || !TryGroundPoint(cursor, out Vector3 ground))
                return false;

            if (!TrySpendSkillMana(HudSkill.SwordTyphoon, swordTyphoonManaCost)) return false;
            EnterCombatStance();

            Vector3 origin = transform.position;
            origin.y = ground.y;
            Vector3 target = InsideClearing(origin + Vector3.ClampMagnitude(ground - origin, swordTyphoonCastRange));
            swordTyphoonCenter = SwordTyphoon.LimitPlacement(transform, origin, target);
            Vector3 direction = target - origin;
            if (direction.sqrMagnitude > .0025f) Face(direction.normalized);

            PrepareForAction();
            bladeTrail.enabled = false;
            swordTyphoonStartedAt = Time.timeAsDouble;
            swordTyphoonReadyAt = swordTyphoonStartedAt + swordTyphoonCooldown;
            IsCastingSwordTyphoon = true;
            return true;
        }

        void UpdateSwordTyphoon()
        {
            MoveOnGround(Vector3.zero);
            float elapsed = (float)(Time.timeAsDouble - swordTyphoonStartedAt);
            float progress = swordTyphoonCastTime > 0f ? elapsed / swordTyphoonCastTime : 1f;
            int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.z)
                ? (facing.x > 0f ? 0 : 1) : (facing.z > 0f ? 3 : 2);
            Sprite pose = swordTyphoonCastFrames.Length == 16
                ? swordTyphoonCastFrames[direction * 4 + Mathf.Clamp((int)(progress * 4), 0, 3)]
                : null;
            if (pose != null)
            {
                body.flipX = false;
                body.sprite = pose;
            }
            else
            {
                Sprite[] frames = back ? swingBack : swingFront;
                body.flipX = left;
                body.sprite = frames[Mathf.Clamp((int)(progress * frames.Length), 0, frames.Length - 1)];
            }
            body.transform.localPosition = Vector3.up * artworkGroundOffset;
            if (elapsed < swordTyphoonCastTime) return;

            IsCastingSwordTyphoon = false;
            Vector3 origin = transform.position;
            origin.y = swordTyphoonCenter.y;
            // A gate may have closed since the cursor position was locked.
            Vector3 center = SwordTyphoon.LimitPlacement(transform, origin, swordTyphoonCenter);
            swordTyphoons.RemoveAll(typhoon => typhoon == null);
            swordTyphoons.Add(SwordTyphoon.Create(transform, center, swordTyphoonRadius,
                swordTyphoonDuration, swordTyphoonTickInterval, swordTyphoonTickCount,
                attackDamage * swordTyphoonTickDamageMultiplier, attackDamage * swordTyphoonBurstDamageMultiplier,
                swordTyphoonPullSpeed, targetMask, swordTyphoonGroundFrames));
        }

        void CancelSwordTyphoon()
        {
            IsCastingSwordTyphoon = false;
            foreach (SwordTyphoon typhoon in swordTyphoons)
                if (typhoon != null) typhoon.Cancel();
            swordTyphoons.Clear();
        }
    }
}
