using UnityEngine;

namespace Storybook
{
    public sealed class FishArcherAI : MonsterCombatAI
    {
        public enum State { Idle, Approach, Aim, Release, Recover, Dead }

        [SerializeField] FishArcherVisual visual;
        [SerializeField] LineRenderer aimGuide;
        [SerializeField, Min(.1f)] float attackRange = 5f;

        [Header("Water Bolt")]
        [SerializeField, Min(.1f)] float boltSpeed = 7f;
        [SerializeField, Min(.1f)] float boltRange = 6f;
        [SerializeField, Min(0f)] float boltDamage = 10f;

        const float MuzzleHeight = .34f;
        const float BoltRadius = .075f;
        const float ApproachTolerance = .12f;
        Vector3 shotDirection = Vector3.forward;
        bool holdingPosition, fired;

        public State CurrentState { get; private set; }
        internal float AimProgress => Mathf.Clamp01(AttackElapsed / Mathf.Max(.01f, windupTime));
        internal float RecoverProgress =>
            Mathf.Clamp01((AttackElapsed - windupTime - activeTime) / Mathf.Max(.01f, recoveryTime));

        void Reset()
        {
            moveSpeed = 1.3f;
            detectionRange = 6f;
            loseTargetRange = 8f;
            windupTime = .8f;
            activeTime = .13f;
            recoveryTime = .58f;
            attackInterval = 2.5f;
        }

        protected override void ResetBehavior(bool dead)
        {
            CurrentState = dead ? State.Dead : State.Idle;
            if (aimGuide != null) aimGuide.enabled = false;
            holdingPosition = false;
        }

        protected override void TickBehavior()
        {
            if ((CurrentState is State.Aim or State.Release or State.Recover) && TickAttack()) return;

            Vector3 toTarget = PlanarOffset(target.transform.position);
            float distance = toTarget.magnitude;
            CurrentState = State.Idle;
            if (!TracksTarget(distance))
            {
                holdingPosition = false;
                return;
            }

            // The planar direction is undefined if Windstep leaves both bodies centered together.
            Vector3 direction = distance > .00001f ? toTarget / distance : shotDirection;
            visual.Face(direction);

            // Once stopped, allow a small drift outside attack range before moving again.
            holdingPosition = distance <= attackRange + (holdingPosition ? ApproachTolerance : 0f);
            if (!holdingPosition)
            {
                CurrentState = State.Approach;
                MoveOnGround(direction, distance - attackRange + .01f, stayInRoom: true);
                return;
            }

            if (!AttackReady || distance > attackRange) return;

            if (distance <= .00001f ||
                !FishArcherBolt.Cast(Muzzle(), direction, BoltRadius, distance, out _, out PlayerHealth struck) ||
                struck == target)
                BeginAim(direction);
        }

        void BeginAim(Vector3 direction)
        {
            BeginAttackTiming();
            CurrentState = State.Aim;
            shotDirection = direction;
            visual.LockAimFacing(direction);
            fired = false;
            UpdateGuide();
        }

        bool TickAttack()
        {
            AttackPhase phase = AttackPhaseAt(AttackElapsed);
            if (phase == AttackPhase.Windup)
            {
                UpdateGuide();
                return true;
            }

            if (!fired)
            {
                fired = true;
                visual.MarkRelease();
                aimGuide.enabled = false;
                FishArcherBolt.Launch(Muzzle(), shotDirection, boltSpeed, boltRange, BoltRadius, boltDamage,
                    aimGuide.sharedMaterial, visual.BoltTrailOffset(shotDirection));
            }

            if (phase == AttackPhase.Finished) return false;
            CurrentState = phase == AttackPhase.Active ? State.Release : State.Recover;
            return true;
        }

        Vector3 Muzzle() => transform.position + Vector3.up * MuzzleHeight;

        void UpdateGuide()
        {
            aimGuide.enabled = true;
            float distance = boltRange;
            if (FishArcherBolt.Cast(Muzzle(), shotDirection, BoltRadius, distance, out float impact, out _, ignorePlayer: true))
                distance = impact;

            Vector3 ground = transform.position + Vector3.up * .03f;
            aimGuide.SetPosition(0, ground + shotDirection * Mathf.Min(.22f, distance));
            aimGuide.SetPosition(1, ground + shotDirection * distance);
        }
    }
}
