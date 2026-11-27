using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ChargedAttackWarning))]
    public sealed class TurtleGuardAI : MonsterCombatAI
    {
        public enum State { Idle, Move, Windup, Thrust, Recovery, Dead }

        [Header("Shield")]
        [SerializeField, Range(0f, 1f)] float frontalReduction = .6f;
        [SerializeField, Range(0f, 90f)] float shieldHalfAngle = 55f;
        [Header("Spear")]
        [SerializeField, Min(.1f)] float attackRange = 1.52f;
        [SerializeField, Min(.1f)] float spearReach = 1.85f;
        [SerializeField, Min(.1f)] float spearWidth = .55f;
        [SerializeField, Min(0f)] float damage = 18f;
        [SerializeField] LineRenderer outline;
        [SerializeField] SpriteRenderer body;
        [Tooltip("Front, back, left, right. Each row has ready, step, windup and thrust frames.")]
        [SerializeField] Sprite[] frames;

        ChargedAttackWarning warning;
        LineRenderer spearFlash;
        Camera view;
        Quaternion restRotation;
        Vector3 origin;
        float gait, hitAt = float.NegativeInfinity, deathAt, visualClock;
        MonsterHealth health;
        public Vector3 Facing { get; private set; } = Vector3.back;
        public State CurrentState { get; private set; }
        bool hitResolved;
        int impactFrame = -1;

        protected override void Awake()
        {
            base.Awake();
            health = GetComponent<MonsterHealth>();
            health.Damaged += ShowHit;
            warning = GetComponent<ChargedAttackWarning>();
            warning.BindOutline(outline);
            spearFlash = new GameObject("Spear thrust flash").AddComponent<LineRenderer>();
            spearFlash.transform.SetParent(transform, false);
            spearFlash.sharedMaterial = outline.sharedMaterial;
            spearFlash.sortingOrder = 3;
            spearFlash.positionCount = 2;
            spearFlash.startWidth = .025f;
            spearFlash.endWidth = .009f;
            spearFlash.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spearFlash.enabled = false;
            restRotation = body.transform.localRotation;
            view = Camera.main;
        }

        void ShowHit() => hitAt = (float)ActionTime;

        internal float GuardDamage(float amount, Vector3 source)
        {
            Vector3 towardSource = PlanarOffset(source);
            if (towardSource.sqrMagnitude < .0001f) return amount;
            return Vector3.Dot(Facing, towardSource.normalized) >= Mathf.Cos(shieldHalfAngle * Mathf.Deg2Rad)
                ? amount * (1f - frontalReduction) : amount;
        }

        protected override void ResetBehavior(bool dead)
        {
            if (dead && CurrentState != State.Dead) deathAt = (float)ActionTime;
            if (!dead && CurrentState == State.Dead) return;
            CurrentState = dead ? State.Dead : State.Idle;
            impactFrame = -1;
            warning.Hide();
            spearFlash.enabled = false;
        }

        protected override void TickBehavior()
        {
            if (CurrentState is State.Windup or State.Thrust or State.Recovery)
            {
                TickThrust();
                return;
            }

            Vector3 offset = PlanarOffset(target.transform.position);
            float distance = offset.magnitude;
            if (!TracksTarget(distance))
            {
                CurrentState = State.Idle;
                return;
            }

            Vector3 direction = distance > .001f ? offset / distance : Facing;
            bool near = distance <= Mathf.Min(spearReach, attackRange) && ClearLane(transform.position, target.transform.position);
            if (near)
            {
                CurrentState = State.Idle;
                // Keep the shield pointed in the last movement or attack direction during cooldown.
                if (AttackReady) BeginThrust(direction);
                return;
            }

            Vector3 before = transform.position;
            Vector3 waypoint = PalaceCover.NextPoint(before, target.transform.position, controller.radius + controller.skinWidth);
            Vector3 step = PlanarOffset(waypoint);
            MoveOnGround(step.normalized, step.magnitude, stayInRoom: true);
            Vector3 moved = PlanarOffset(before);
            CurrentState = moved.sqrMagnitude > .000001f ? State.Move : State.Idle;
            if (CurrentState == State.Move)
            {
                Facing = -moved.normalized;
                gait += moved.magnitude;
            }
        }

        void BeginThrust(Vector3 direction)
        {
            BeginAttackTiming();
            hitResolved = false;
            Facing = direction;
            origin = transform.position;
            origin.y = .032f;
            Vector3 half = Vector3.Cross(Vector3.up, direction) * (spearWidth * .5f);
            Vector3 end = origin + direction * spearReach;
            outline.SetPositions(new[] { origin - half, origin + half, end + half, end - half });
            outline.enabled = true;
            warning.Begin(origin, direction, spearWidth, spearReach);
            CurrentState = State.Windup;
        }

        void TickThrust()
        {
            float elapsed = AttackElapsed;
            AttackPhase phase = AttackPhaseAt(elapsed);
            if (phase == AttackPhase.Windup)
            {
                warning.SetProgress(elapsed / windupTime);
                return;
            }

            if (!hitResolved)
            {
                hitResolved = true;
                impactFrame = Time.frameCount;
                Vector3 offset = target.transform.position - origin;
                float reach = Vector3.Dot(offset, Facing);
                float lateral = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, Facing)));
                if (reach >= 0f && reach <= spearReach && lateral <= spearWidth * .5f &&
                    ClearLane(origin, target.transform.position)) target.TryTakeDamage(damage);
            }
            if (phase == AttackPhase.Active) warning.SetProgress(1f);
            else warning.Hide();
            CurrentState = phase switch
            {
                AttackPhase.Active => State.Thrust,
                AttackPhase.Recovery => State.Recovery,
                _ => State.Idle
            };
        }

        bool ClearLane(Vector3 start, Vector3 end)
        {
            start.y = end.y = transform.position.y + .35f;
            Vector3 offset = end - start;
            if (offset.sqrMagnitude < .0001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(start, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(target.transform))
                    return false;
            return true;
        }

        void LateUpdate()
        {
            if (health.IsTimeStopped) return;
            Vector3 right = view != null ? view.transform.right : Vector3.right;
            Vector3 forward = view != null ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized : Vector3.forward;
            float x = Vector3.Dot(Facing, right), z = Vector3.Dot(Facing, forward);
            int row = Mathf.Abs(x) > Mathf.Abs(z) ? (x < 0f ? 2 : 3) : (z < 0f ? 0 : 1);
            visualClock += Time.deltaTime;
            float death = CurrentState == State.Dead ? Mathf.Clamp01(((float)ActionTime - deathAt) / .55f) : 0f;
            float hit = 1f - Mathf.Clamp01(((float)ActionTime - hitAt) / .16f);
            int frame = impactFrame == Time.frameCount ? 3 : CurrentState switch
            {
                State.Move => Mathf.FloorToInt(gait * 5f) % 2,
                State.Windup or State.Recovery => 2,
                State.Thrust => 3,
                _ => 0
            };
            body.sprite = frames[row * 4 + frame];
            bool thrustVisible = frame == 3 && CurrentState != State.Dead;
            spearFlash.enabled = thrustVisible;
            if (thrustVisible)
            {
                float fade = 1f - Mathf.Clamp01((AttackElapsed - windupTime) / activeTime);
                Vector3 lift = Vector3.up * .29f;
                spearFlash.SetPosition(0, origin + lift + Facing * (spearReach * .36f));
                spearFlash.SetPosition(1, origin + lift + Facing * spearReach);
                spearFlash.startColor = new Color(.91f, .69f, .36f, fade * .65f);
                spearFlash.endColor = new Color(1f, .93f, .70f, fade);
            }

            float idleBob = CurrentState == State.Idle ? Mathf.Round(Mathf.Sin(visualClock * 5f)) * .008f : 0f;
            body.transform.localPosition = Vector3.up * idleBob;
            body.transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, -7f * hit - 30f * death);
            body.color = new Color(1f, 1f - .52f * hit, 1f - .52f * hit, 1f - death);
        }

        void OnDestroy()
        {
            if (health != null) health.Damaged -= ShowHit;
        }
    }
}
