using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class HongGildong : MonoBehaviour
    {
        [SerializeField] Camera fieldCamera;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite idleFront, idleBack;
        [SerializeField] Sprite[] swingFront, swingBack;
        [SerializeField] Sprite[] walkFront, walkBack;
        [SerializeField] LineRenderer bladeTrail;
        [SerializeField, Min(.1f)] float walkSpeed = 1.8f;
        [SerializeField] float reach = 1.7f;
        [SerializeField, Min(0)] float attackDamage = 25f;
        [SerializeField] float halfArc = 68f;
        [SerializeField] LayerMask targetMask;
        [Header("Dash")]
        [SerializeField, Min(.01f)] float dashDistance = 2f;
        [SerializeField, Min(.01f)] float dashDuration = .25f;
        [SerializeField, Min(0f)] float invulnerabilityDuration = .2f;
        [SerializeField, Min(0f)] float dashCooldown = 2f;
        [SerializeField] Sprite dashFront, dashBack;
        [Header("Move Marker")]
        [SerializeField, Min(.01f)] float markerRadius = .22f;
        [SerializeField, Min(.001f)] float markerWidth = .035f;
        [SerializeField, Min(.001f)] float markerHeight = .025f;
        [SerializeField] Color markerColor = new Color(.25f, 1f, .65f, .9f);
        [SerializeField] Material markerMaterial;

        const float Windup = .12f, ContactEnd = .29f, RecoveryEnd = .42f;
        CharacterController controller;
        InputAction move, point, strike, dash;
        Vector3 facing = new Vector3(.8f, 0f, -.6f), slashDirection;
        const float WalkCycleDistance = 1.3f, ArrivalTolerance = .015f;
        Vector3? destination;
        LineRenderer moveMarker;
        Vector3 dashDirection;
        float dashTimeLeft;
        double invulnerableUntil, dashReadyAt;
        float attackTime = RecoveryEnd, gaitPhase;
        bool back, left, attackBack, attackLeft;
        readonly HashSet<AttackTarget> hitThisSwing = new HashSet<AttackTarget>();
        float SweepSign => attackLeft == attackBack ? -1f : 1f;
        public bool Attacking => attackTime < RecoveryEnd;
        public bool HasMoveDestination => destination.HasValue;
        public bool IsDashing => dashTimeLeft > 0f;
        public bool IsInvulnerable => isActiveAndEnabled && Time.timeAsDouble < invulnerableUntil;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            move = new InputAction("Move To", InputActionType.Button, "<Mouse>/rightButton");
            point = new InputAction("Aim", InputActionType.PassThrough, "<Mouse>/position");
            strike = new InputAction("Slash", InputActionType.Button, "<Mouse>/leftButton");
            dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/space");
            CreateMoveMarker();
        }

        void OnEnable() { move.Enable(); point.Enable(); strike.Enable(); dash.Enable(); }
        void OnDisable()
        {
            move.Disable(); point.Disable(); strike.Disable(); dash.Disable();
            PrepareForAction();
            dashTimeLeft = gaitPhase = 0f;
            invulnerableUntil = 0;
            attackTime = RecoveryEnd;
            bladeTrail.enabled = false;
        }
        void OnDestroy()
        {
            move.Dispose(); point.Dispose(); strike.Dispose(); dash.Dispose();
            if (moveMarker != null) Destroy(moveMarker.gameObject);
        }

        void Update()
        {
            Vector2 cursor = point.ReadValue<Vector2>();
            // Actions own movement until they finish; clicks during them are not queued.
            if (!IsDashing && !Attacking && move.WasPressedThisFrame() && TryGroundPoint(cursor, out Vector3 goal))
                SetDestination(goal);
            if (!IsDashing && !Attacking && dash.WasPressedThisFrame() && Time.timeAsDouble >= dashReadyAt)
                BeginDash(cursor);
            if (IsDashing)
            {
                MoveDash();
                body.flipX = left;
                body.sprite = back ? dashBack : dashFront;
                return;
            }
            if (!Attacking && strike.WasPressedThisFrame()) BeginSwing(cursor);
            Vector3 offset = Attacking ? Vector3.zero : RemainingWalk();
            if (offset.sqrMagnitude > 0f) Face(offset.normalized);
            float step = walkSpeed * Time.deltaTime;
            MoveOnGround(Vector3.ClampMagnitude(offset, step));
            ClearReachedDestination();
            if (Attacking)
            {
                float previousTime = attackTime;
                attackTime += Time.deltaTime;
                if (attackTime >= Windup && previousTime < ContactEnd)
                    StrikeTargets(SweepAngle(previousTime), SweepAngle(attackTime));
            }
            DrawTrail();
            body.flipX = Attacking ? attackLeft : left;
            body.sprite = Attacking ? SwingPose() : TravelPose();
            body.transform.localPosition = Vector3.up * .02f;
        }

        static Vector3 InsideClearing(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, -5.3f, 5.3f), p.y, Mathf.Clamp(p.z, -5.3f, 3.65f));

        bool TryGroundPoint(Vector2 cursor, out Vector3 ground)
        {
            ground = default;
            if (!fieldCamera.pixelRect.Contains(cursor)) return false;
            Ray ray = fieldCamera.ScreenPointToRay(cursor);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            ground = ray.GetPoint(distance);
            return true;
        }

        Vector3 RemainingWalk() => destination.HasValue
            ? Vector3.ProjectOnPlane(destination.Value - transform.position, Vector3.up) : Vector3.zero;

        // Future skills call this on successful activation. A travelling skill can
        // explicitly preserve its destination and control its own action movement.
        public void PrepareForAction(bool preserveDestination = false)
        {
            if (preserveDestination) return;
            destination = null;
            gaitPhase = 0f;
            if (moveMarker != null) moveMarker.enabled = false;
        }

        void SetDestination(Vector3 goal)
        {
            destination = InsideClearing(goal);
            moveMarker.transform.position = destination.Value + Vector3.up * markerHeight;
            moveMarker.enabled = true;
            ClearReachedDestination();
        }

        void CreateMoveMarker()
        {
            // Independent of the player transform so it stays on the ground.
            GameObject marker = new GameObject("Move Destination Marker");
            marker.layer = LayerMask.NameToLayer("Ignore Raycast");
            moveMarker = marker.AddComponent<LineRenderer>();
            moveMarker.useWorldSpace = false;
            moveMarker.loop = true;
            moveMarker.positionCount = 48;
            moveMarker.alignment = LineAlignment.TransformZ;
            marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            moveMarker.sharedMaterial = markerMaterial != null ? markerMaterial : bladeTrail.sharedMaterial;
            moveMarker.startColor = moveMarker.endColor = markerColor;
            moveMarker.startWidth = moveMarker.endWidth = markerWidth;
            moveMarker.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moveMarker.receiveShadows = false;
            for (int i = 0; i < moveMarker.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / moveMarker.positionCount;
                moveMarker.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * markerRadius);
            }
            moveMarker.enabled = false;
        }

        void ClearReachedDestination()
        {
            if (RemainingWalk().sqrMagnitude <= ArrivalTolerance * ArrivalTolerance) PrepareForAction();
        }

        void Face(Vector3 direction)
        {
            facing = direction;
            if (Mathf.Abs(direction.x) > .01f) left = direction.x < 0f;
            if (Mathf.Abs(direction.z) > .01f) back = direction.z > 0f;
        }

        CollisionFlags MoveOnGround(Vector3 delta)
        {
            Vector3 next = InsideClearing(transform.position + delta);
            return controller.Move(next - transform.position + Vector3.down * (2f * Time.deltaTime));
        }

        void BeginDash(Vector2 cursor)
        {
            if (!TryGroundPoint(cursor, out Vector3 ground)) return;
            Vector3 direction = Vector3.ProjectOnPlane(ground - transform.position, Vector3.up);
            if (direction.sqrMagnitude < .0025f)
                direction = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (direction.sqrMagnitude < .0025f) direction = Vector3.forward;
            dashDirection = direction.normalized;
            Face(dashDirection);
            gaitPhase = 0f;
            dashTimeLeft = dashDuration;
            invulnerableUntil = Time.timeAsDouble + invulnerabilityDuration;
            dashReadyAt = Time.timeAsDouble + dashCooldown;
            PrepareForAction();
        }

        void MoveDash()
        {
            float dt = Mathf.Min(Time.deltaTime, dashTimeLeft);
            float step = dashDistance * (dt / dashDuration);
            Vector3 before = transform.position;
            CollisionFlags flags = MoveOnGround(dashDirection * step);
            float travelled = Vector3.Dot(transform.position - before, dashDirection);
            bool blocked = (flags & CollisionFlags.Sides) != 0 || travelled + .001f < step;
            dashTimeLeft = blocked ? 0f : Mathf.Max(0f, dashTimeLeft - dt);
        }

        void BeginSwing(Vector2 cursor)
        {
            if (!fieldCamera.pixelRect.Contains(cursor)) return;
            Ray ray = fieldCamera.ScreenPointToRay(cursor);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            Vector3 aim = Physics.Raycast(ray, out RaycastHit hit, 100f, targetMask)
                ? hit.collider.transform.position : ray.GetPoint(distance);
            Vector3 offset = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
            PrepareForAction();
            slashDirection = offset.sqrMagnitude > .0025f ? offset.normalized : facing;
            attackBack = Mathf.Abs(slashDirection.z) > .08f ? slashDirection.z > 0f : back;
            attackLeft = Mathf.Abs(slashDirection.x) > .08f ? slashDirection.x < 0f : left;
            hitThisSwing.Clear();
            attackTime = 0f;
            gaitPhase = 0f;
        }

        Sprite SwingPose()
        {
            Sprite[] frames = attackBack ? swingBack : swingFront;
            if (attackTime < Windup) return frames[0];
            if (attackTime < (Windup + ContactEnd) * .5f) return frames[1];
            if (attackTime < ContactEnd) return frames[2];
            return frames[3];
        }

        Sprite TravelPose()
        {
            Vector3 velocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
            if (velocity.sqrMagnitude < .01f)
            {
                gaitPhase = 0f;
                return back ? idleBack : idleFront;
            }
            Sprite[] frames = back ? walkBack : walkFront;
            float distance = velocity.magnitude * Time.deltaTime;
            gaitPhase = Mathf.Repeat(gaitPhase + distance / WalkCycleDistance, 1f);
            return frames[Mathf.Min((int)(gaitPhase * frames.Length), frames.Length - 1)];
        }

        float SweepAngle(float time) => Mathf.Lerp(-halfArc, halfArc, Mathf.InverseLerp(Windup, ContactEnd, time));

        void StrikeTargets(float start, float end)
        {
            foreach (Collider other in Physics.OverlapSphere(transform.position + Vector3.up * .45f, reach + .45f, targetMask))
            {
                AttackTarget target = other.GetComponentInParent<AttackTarget>(); if (target == null || !target.CanTakeDamage) continue;
                Vector3 offset = Vector3.ProjectOnPlane(other.transform.position - transform.position, Vector3.up);
                float angle = SweepSign * Vector3.SignedAngle(slashDirection, offset, Vector3.up);
                if (offset.sqrMagnitude <= reach * reach && angle >= start && angle <= end && hitThisSwing.Add(target))
                    target.ReceiveDamage(attackDamage);
            }
        }

        void DrawTrail()
        {
            bladeTrail.enabled = attackTime >= Windup && attackTime < ContactEnd;
            if (!bladeTrail.enabled) return;
            float head = SweepAngle(attackTime);
            for (int i = 0; i < bladeTrail.positionCount; i++)
            {
                float angle = Mathf.Lerp(Mathf.Max(-halfArc, head - 62f), head, i / (bladeTrail.positionCount - 1f));
                Vector3 radial = Quaternion.AngleAxis(angle * SweepSign, Vector3.up) * slashDirection;
                bladeTrail.SetPosition(i, transform.position + Vector3.up * .30f + radial * reach);
            }
        }
    }
}
