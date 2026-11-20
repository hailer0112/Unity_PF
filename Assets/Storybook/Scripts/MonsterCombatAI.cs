using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Storybook
{
    internal enum SkillHit { AzureSlash, CloneSlash, SwordTyphoon }

    [RequireComponent(typeof(MonsterHealth), typeof(CharacterController))]
    public abstract class MonsterCombatAI : MonoBehaviour
    {
        static readonly List<MonsterCombatAI> ActiveMonsters = new();
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
        double recoilStartedAt, recoilUntil, nextRecoilAt;
        Vector3 recoilVelocity;
        Vector3 lastDamageDirection;

        (float distance, float seconds, float tilt, float stop, float scale) HitSettings => this switch
        {
            CrabSoldierAI => (.12f, .09f, 6f, .05f, .70f),
            FishArcherAI => (.23f, .11f, 11f, .05f, .62f),
            TurtleGuardAI => (.04f, .08f, 3.5f, .04f, .76f),
            PufferfishBomberAI => (.20f, .10f, 9f, .05f, .58f),
            _ => default
        };

        internal bool ReactToBasicHit(Vector3 attacker, out float stopSeconds, out float effectScale)
        {
            var settings = HitSettings;
            stopSeconds = settings.stop;
            effectScale = settings.scale;
            if (stopSeconds == 0f) return false;
            if (health.IsDead) return true;

            PlayHit(-PlanarOffset(attacker).normalized, settings.distance, settings.seconds, settings.tilt, stopSeconds);
            return true;
        }

        internal void ReactToSkillHit(Vector3 source, SkillHit skill, Vector3? contact)
        {
            var settings = HitSettings;
            if (settings.seconds == 0f) return;

            Vector3 away = -PlanarOffset(source).normalized;
            var reaction = skill switch
            {
                SkillHit.AzureSlash => (.7f, 1.15f, .055f, 1.21f),
                SkillHit.CloneSlash => (.24f, .76f, .035f, .90f),
                _ => (0f, .25f, 0f, .79f)
            };
            var (recoil, tilt, stop, size) = reaction;
            var pose = PlayHit(away, settings.distance * recoil, settings.seconds, settings.tilt * tilt, stop, skill);
            Vector3 point = contact ?? (controller.bounds.center - away * controller.radius * .4f);
            pose.ShowSkillImpact(skill, point, settings.scale * size);
        }

        BasicHitPose PlayHit(Vector3 away, float distance, float seconds, float tilt, float stop, SkillHit? skill = null)
        {
            bool canRecoil = CanRecoil();
            if (!TryGetComponent(out BasicHitPose pose)) pose = gameObject.AddComponent<BasicHitPose>();
            pose.Play(away, seconds, tilt, canRecoil ? stop : 0f, skill);
            if (distance <= 0f || !canRecoil || !controller.enabled) return pose;

            double now = Time.timeAsDouble;
            if (now >= nextRecoilAt)
            {
                recoilStartedAt = now;
                recoilUntil = now + seconds;
                nextRecoilAt = now + .24f;
                recoilVelocity = away * (distance / seconds);
            }
            return pose;
        }

        internal void TrackDamageDirection(Vector3 source)
        {
            Vector3 away = -PlanarOffset(source);
            if (away.sqrMagnitude > .0001f) lastDamageDirection = away.normalized;
        }

        internal float ShowDefeat()
        {
            (float distance, float fallTime, float fadeTime, float tilt, float sink, float height, DefeatBurstStyle burst) settings = this switch
            {
                CrabSoldierAI => (.28f, .28f, .30f, 82f, .12f, .90f, DefeatBurstStyle.Crab),
                FishArcherAI => (.43f, .30f, .28f, 89f, .14f, .88f, DefeatBurstStyle.Fish),
                TurtleGuardAI => (.09f, .30f, .28f, 27f, .18f, .72f, DefeatBurstStyle.Turtle),
                PufferfishBomberAI => (.34f, .50f, .27f, 0f, 0f, 1f, DefeatBurstStyle.Puffer),
                _ => default
            };
            if (settings.fallTime == 0f) return 0f;

            // A tick at the center of a ground effect can have no planar direction.
            Vector3 away = lastDamageDirection != Vector3.zero ? lastDamageDirection : Vector3.back;
            Vector3 offset = DefeatOffset(away, settings.distance);
            if (!TryGetComponent(out BasicHitPose pose)) pose = gameObject.AddComponent<BasicHitPose>();
            pose.PlayDefeat(offset, settings.fallTime, settings.fadeTime,
                away.x < 0f ? settings.tilt : -settings.tilt, settings.sink, settings.height, settings.burst, away);
            return settings.fallTime + settings.fadeTime;
        }

        Vector3 DefeatOffset(Vector3 away, float distance)
        {
            if (!controller.enabled || away == Vector3.zero) return Vector3.zero;

            Vector3 scale = transform.lossyScale;
            float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            Vector3 center = transform.TransformPoint(controller.center);
            float shaft = Mathf.Max(0f, controller.height * Mathf.Abs(scale.y) * .5f - radius);
            // Lift clear of the floor. The body shape is still live during this query.
            Vector3 vertical = Vector3.up * shaft;
            center += Vector3.up * .035f;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(center - vertical, center + vertical,
                         radius, away, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - .02f));
            }
            return KeepInRoom(away * distance);
        }

        bool CanRecoil() => this switch
        {
            CrabSoldierAI crab => crab.CurrentState is CrabSoldierAI.State.Idle or CrabSoldierAI.State.Chase,
            FishArcherAI fish => fish.CurrentState is FishArcherAI.State.Idle or FishArcherAI.State.Approach,
            TurtleGuardAI turtle => turtle.CurrentState is TurtleGuardAI.State.Idle or TurtleGuardAI.State.Move,
            PufferfishBomberAI puffer => puffer.CurrentState is PufferfishBomberAI.State.Idle or PufferfishBomberAI.State.Chase,
            _ => false
        };

        protected bool AttackReady => Time.time >= nextAttack;
        protected float AttackElapsed => Time.time - attackStarted;
        protected virtual bool HasCommittedAction => false;

        protected virtual void Awake()
        {
            controller = GetComponent<CharacterController>();
            health = GetComponent<MonsterHealth>();
        }

        protected void Start()
        {
            if (target == null) target = FindAnyObjectByType<PlayerHealth>();
        }

        protected virtual void OnEnable()
        {
            health.Died += OnDeath;
            ActiveMonsters.Add(this);
        }

        protected virtual void OnDisable()
        {
            recoilUntil = nextRecoilAt = 0d;
            ActiveMonsters.Remove(this);
            health.Died -= OnDeath;
            ResetTrackingAndAction(health.IsDead);
        }

        protected void Update()
        {
            if (health.IsDead) return;
            if (!HasCommittedAction && (target == null || !target.isActiveAndEnabled || target.IsDead))
            {
                ResetTrackingAndAction(false);
                return;
            }

            double now = Time.timeAsDouble;
            double remaining = System.Math.Min(now, recoilUntil) - System.Math.Max(recoilStartedAt, now - Time.deltaTime);
            if (remaining > 0d && CanRecoil() && controller.enabled)
            {
                controller.Move(KeepInRoom(recoilVelocity * (float)remaining));
                return;
            }
            if (now >= roomReadyAt) TickBehavior();
        }

        public void ActivateInRoom(Bounds bounds)
        {
            if (health.IsDead) return;
            recoilUntil = nextRecoilAt = 0d;
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
            Vector3 offset = SteerAroundMonsters(direction) * Mathf.Min(distance, moveSpeed * Time.deltaTime);
            if (stayInRoom) offset = KeepInRoom(offset);
            controller.Move(offset + Vector3.down * (2f * Time.deltaTime));
            return Vector3.Dot(transform.position - before, direction) > .0001f;
        }

        Vector3 KeepInRoom(Vector3 offset)
        {
            if (roomBounds is not Bounds bounds) return offset;
            Vector3 before = transform.position;
            // Keep the capsule inside the encounter footprint, even near open passages.
            float radius = controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float xMargin = Mathf.Min(radius, bounds.extents.x);
            float zMargin = Mathf.Min(radius, bounds.extents.z);
            offset.x = Mathf.Clamp(before.x + offset.x, Mathf.Min(before.x, bounds.min.x + xMargin),
                Mathf.Max(before.x, bounds.max.x - xMargin)) - before.x;
            offset.z = Mathf.Clamp(before.z + offset.z, Mathf.Min(before.z, bounds.min.z + zMargin),
                Mathf.Max(before.z, bounds.max.z - zMargin)) - before.z;
            return offset;
        }

        Vector3 SteerAroundMonsters(Vector3 direction)
        {
            if (direction.sqrMagnitude < .0001f) return direction;
            Vector3 right = Vector3.Cross(Vector3.up, direction);
            float strongest = 0f, avoidance = 0f;
            foreach (var other in ActiveMonsters)
            {
                if (other == this || !other.controller.enabled || other.gameObject.scene != gameObject.scene) continue;
                Vector3 offset = PlanarOffset(other.transform.position);
                float ahead = Vector3.Dot(offset, direction);
                if (ahead <= 0f || Mathf.Abs(other.transform.position.y - transform.position.y) > controller.height) continue;

                float spacing = controller.bounds.extents.x + other.controller.bounds.extents.x + .12f;
                float across = Vector3.Dot(offset, right);
                float weight = (1f - Mathf.Clamp01(ahead / (spacing + .3f))) *
                    (1f - Mathf.Clamp01(Mathf.Abs(across) / spacing));
                if (weight <= strongest) continue;
                strongest = weight;
                // Keep a consistent side when following directly behind another body.
                avoidance = weight * (across < -.02f ? 1f : -1f);
            }
            return (direction + right * (avoidance * .8f)).normalized;
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
