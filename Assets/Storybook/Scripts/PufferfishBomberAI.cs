using UnityEngine;

namespace Storybook
{
    public sealed class PufferfishBomberAI : MonsterCombatAI
    {
        public enum State { Idle, Chase, Inflate, Explode, Killed }

        [SerializeField, Min(.1f)] float triggerRange = 1.2f;
        [SerializeField, Min(.01f)] float inflateSeconds = 1f;
        [SerializeField, Min(.1f)] float blastRadius = 2.25f;
        [SerializeField, Min(0f)] float blastDamage = 20f;
        [SerializeField, Min(.1f)] float poisonRadius = 2.25f;
        [SerializeField, Min(.01f)] float poisonSeconds = 3f;
        [SerializeField, Min(.01f)] float poisonTickInterval = 1f;
        [SerializeField, Min(0f)] float poisonDamage = 5f;

        [Header("Visuals")]
        [SerializeField] Material groundMaterial;
        [SerializeField] ParticleSystem poisonSmoke;
        MonsterHealth health;
        PufferfishHazard warning;
        Vector3 blastCenter;
        double inflateStarted;

        public State CurrentState { get; private set; }
        public int Facing { get; private set; }
        public float InflateProgress { get; private set; }
        protected override bool HasCommittedAction => CurrentState == State.Inflate;

        protected override void Awake()
        {
            base.Awake();
            health = GetComponent<MonsterHealth>();
        }

        protected override void TickBehavior()
        {
            if (CurrentState == State.Inflate)
            {
                InflateProgress = Mathf.Clamp01((float)((Time.timeAsDouble - inflateStarted) / inflateSeconds));
                warning.SetCharge(InflateProgress);
                if (InflateProgress >= 1f) Detonate();
                return;
            }

            Vector3 offset = PlanarOffset(target.transform.position);
            float distance = offset.magnitude;
            if (!TracksTarget(distance))
            {
                CurrentState = State.Idle;
                return;
            }

            if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z)) Facing = offset.x > 0f ? 1 : 3;
            else if (offset.z != 0f) Facing = offset.z > 0f ? 2 : 0;

            if (distance <= triggerRange)
            {
                CurrentState = State.Inflate;
                inflateStarted = Time.timeAsDouble;
                InflateProgress = 0f;
                blastCenter = transform.position;
                warning = PufferfishHazard.Create("Pufferfish blast warning", transform, blastCenter,
                    blastRadius, groundMaterial);
                warning.SetCharge(0f);
                return;
            }

            CurrentState = State.Chase;
            MoveOnGround(offset / distance, distance, stayInRoom: true);
        }

        void Detonate()
        {
            // Resolve the death before any damage, effects or re-entrant callbacks.
            if (!health.TrySelfDestruct()) return;
            PufferfishHazard.Create("Pufferfish explosion", transform.parent, blastCenter, blastRadius, groundMaterial)
                .ShowBurst();
            PufferfishHazard.CreatePoison(transform.parent, blastCenter, poisonRadius, target,
                poisonSeconds, poisonTickInterval, poisonDamage, poisonSmoke);
            PufferfishHazard.DamageInside(target, blastCenter, blastRadius, blastDamage);
        }

        protected override void ResetBehavior(bool dead)
        {
            if (warning != null)
            {
                warning.gameObject.SetActive(false);
                Destroy(warning.gameObject);
                warning = null;
            }
            if (!dead)
            {
                CurrentState = State.Idle;
                InflateProgress = 0f;
            }
            else
            {
                CurrentState = health.DeathCause == MonsterDeathCause.SelfDestruct ? State.Explode : State.Killed;
            }
        }
    }
}
