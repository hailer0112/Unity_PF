using UnityEngine;
using UnityEngine.Serialization;

namespace Storybook
{
    [RequireComponent(typeof(MonsterHealth), typeof(CharacterController))]
    public abstract class MonsterCombatAI : MonoBehaviour
    {
        protected enum AttackPhase { Windup, Active, Recovery, Finished }

        [SerializeField] protected PlayerHealth target;
        [SerializeField, Min(0f)] protected float moveSpeed = 1.6f;
        [SerializeField, Min(0f)] protected float detectionRange = 4.5f;
        [SerializeField, Min(0f)] protected float loseTargetRange = 6.5f;

        [Header("Attack Timing")]
        [SerializeField, Min(0f)] float combatStartDelay = 1.5f;
        [SerializeField, Min(.01f), FormerlySerializedAs("aimTime")] protected float windupTime = .6f;
        [SerializeField, Min(.01f), FormerlySerializedAs("releaseTime")] protected float activeTime = .15f;
        [SerializeField, Min(.01f)] protected float recoveryTime = .7f;
        [SerializeField, Min(0f), Tooltip("Minimum time between attack starts, including windup and recovery.")]
        protected float attackInterval = 2f;

        protected CharacterController controller;
        protected float attackStarted, nextAttack;
        MonsterHealth health;
        bool tracking;
        Bounds? roomBounds;
        double roomReadyAt;

        protected bool AttackReady => Time.time >= nextAttack;
        protected float AttackElapsed => Time.time - attackStarted;

        protected virtual void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<MonsterHealth>();
        }

        protected void Start()
        {
            if (target == null) target = FindAnyObjectByType<PlayerHealth>();
        }

        protected void OnEnable() => health.Died += OnDeath;

        protected void OnDisable()
        {
            health.Died -= OnDeath;
            ResetTrackingAndAction(health.IsDead);
        }

        protected void Update()
        {
            if (health.IsDead) return;
            if (target == null || !target.isActiveAndEnabled || target.IsDead)
            {
                ResetTrackingAndAction(false);
                return;
            }

            if (Time.timeAsDouble >= roomReadyAt) TickBehavior();
        }

        public void ActivateInRoom(Bounds bounds)
        {
            if (health.IsDead) return;
            roomBounds = bounds;
            ResetTrackingAndAction(false);
            roomReadyAt = Time.timeAsDouble + combatStartDelay;
            enabled = true;
        }

        protected abstract void TickBehavior();
        protected abstract void ResetBehavior(bool dead);

        void OnDeath() => ResetTrackingAndAction(true);

        void ResetTrackingAndAction(bool dead)
        {
            tracking = false;
            ResetBehavior(dead);
        }

        protected Vector3 PlanarOffset(Vector3 position) =>
            Vector3.ProjectOnPlane(position - transform.position, Vector3.up);

        protected bool TracksTarget(float distance)
        {
            float range = tracking ? Mathf.Max(detectionRange, loseTargetRange) : detectionRange;
            tracking = distance <= range;
            return tracking;
        }

        protected bool MoveOnGround(Vector3 direction, float distance = float.PositiveInfinity, bool stayInRoom = false)
        {
            Vector3 before = transform.position;
            Vector3 offset = direction * Mathf.Min(distance, moveSpeed * Time.deltaTime);
            if (stayInRoom && roomBounds is Bounds bounds)
            {
                // Keep the capsule inside the encounter footprint, even near open passages.
                float radius = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
                float xMargin = Mathf.Min(radius, bounds.extents.x);
                float zMargin = Mathf.Min(radius, bounds.extents.z);
                offset.x = Mathf.Clamp(before.x + offset.x, Mathf.Min(before.x, bounds.min.x + xMargin),
                    Mathf.Max(before.x, bounds.max.x - xMargin)) - before.x;
                offset.z = Mathf.Clamp(before.z + offset.z, Mathf.Min(before.z, bounds.min.z + zMargin),
                    Mathf.Max(before.z, bounds.max.z - zMargin)) - before.z;
            }

            controller.Move(offset + Vector3.down * (2f * Time.deltaTime));
            return Vector3.Dot(transform.position - before, direction) > .0001f;
        }

        protected void BeginAttackTiming()
        {
            attackStarted = Time.time;
            nextAttack = attackStarted + attackInterval;
        }

        protected AttackPhase AttackPhaseAt(float elapsed)
        {
            if (elapsed < windupTime) return AttackPhase.Windup;
            if (elapsed < windupTime + activeTime) return AttackPhase.Active;
            return elapsed < windupTime + activeTime + recoveryTime ? AttackPhase.Recovery : AttackPhase.Finished;
        }
    }
}
