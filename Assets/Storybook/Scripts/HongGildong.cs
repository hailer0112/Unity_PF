using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class HongGildong : MonoBehaviour
    {
        [SerializeField] Camera fieldCamera;
        [SerializeField] bool allowCombat = true;
        [SerializeField] SpriteRenderer body;
        [SerializeField] float artworkGroundOffset = .02f;
        [SerializeField] Sprite idleFront, idleBack;
        [SerializeField] Sprite[] swingFront, swingBack;
        [SerializeField] Sprite[] walkFront, walkBack;
        [SerializeField] Sprite[] runFront, runBack;
        [SerializeField] LineRenderer bladeTrail;
        [SerializeField, Min(.1f)] float walkSpeed = 1.8f;
        [SerializeField] float reach = 1.36f;
        [SerializeField, Min(0)] float attackDamage = 25f;
        [SerializeField] float halfArc = 68f;
        [SerializeField] LayerMask targetMask;
        [SerializeField] Rect movementBounds = Rect.MinMaxRect(-5.3f, -5.3f, 5.3f, 3.65f);
        [Header("Dash")]
        [SerializeField, Min(.01f)] float dashDistance = 2f;
        [SerializeField, Min(.01f)] float dashDuration = .25f;
        [SerializeField, Min(0f)] float invulnerabilityDuration = .2f;
        [SerializeField, Min(0f)] float dashCooldown = 2f;
        [SerializeField] Sprite dashFront, dashBack;
        [Header("Q - Azure Slash")]
        [SerializeField, Min(.01f)] float azureSlashRange = 6f;
        [SerializeField, Min(.01f)] float azureSlashSpeed = 12f;
        [SerializeField, Min(.01f)] float azureSlashWidth = 1.5f;
        [SerializeField, Min(0f)] float azureSlashDamageMultiplier = 2f;
        [SerializeField, Min(0f)] float azureSlashCooldown = 5f;
        [SerializeField, Min(0f)] float azureSlashCastTime = .3f;
        [Header("Move Marker")]
        [SerializeField, Min(.01f)] float markerRadius = .22f;
        [SerializeField, Min(.001f)] float markerWidth = .035f;
        [SerializeField, Min(.001f)] float markerHeight = .025f;
        [SerializeField] Color markerColor = new Color(.25f, 1f, .65f, .9f);
        [SerializeField] Material markerMaterial;

        const float Windup = .12f, ContactEnd = .29f, RecoveryEnd = .42f;
        const float SlashTrailFade = .075f;
        const float KnockbackSpeed = 8f;
        CharacterController controller;
        Vector3 knockbackDirection;
        float knockbackRemaining;
        InputAction move, point, strike, dash, azureSlash;
        Vector3 facing = new Vector3(.8f, 0f, -.6f), slashDirection;
        const float WalkCycleDistance = 1.3f, RunCycleDistance = 1.7f, ArrivalTolerance = .015f;
        Vector3? destination;
        LineRenderer moveMarker;
        Vector3 dashDirection;
        float dashTimeLeft;
        double invulnerableUntil, dashReadyAt;
        float attackTime = RecoveryEnd, gaitPhase;
        double azureSlashStartedAt, azureSlashReadyAt;
        Vector3 azureSlashDirection;
        bool back, left, attackBack, attackLeft;
        readonly HashSet<AttackTarget> hitThisSwing = new HashSet<AttackTarget>();
        readonly Gradient slashGradient = new Gradient();
        readonly GradientAlphaKey[] slashAlpha = new GradientAlphaKey[4];
        float lastTrailOpacity = -1f;
        float SweepSign => attackLeft == attackBack ? -1f : 1f;
        public bool Attacking => attackTime < RecoveryEnd;
        public bool IsCastingAzureSlash { get; private set; }
        public bool HasMoveDestination => destination.HasValue;
        internal Rect MovementBounds => movementBounds;
        public bool IsDashing => dashTimeLeft > 0f;
        public bool IsInvulnerable => isActiveAndEnabled && Time.timeAsDouble < invulnerableUntil;

        internal (float remaining, float duration) GetHudCooldown(HudSkill skill) => skill switch
        {
            HudSkill.AzureSlash => (CooldownLeft(azureSlashReadyAt), azureSlashCooldown),
            HudSkill.CloneSlash => (CooldownLeft(cloneSlashReadyAt), cloneSlashCooldown),
            HudSkill.Windstep => (WindstepCooldownRemaining, windstepCooldown),
            HudSkill.SwordTyphoon => (SwordTyphoonCooldownRemaining, swordTyphoonCooldown),
            HudSkill.Dash => (CooldownLeft(dashReadyAt), dashCooldown),
            _ => (0f, 0f)
        };

        static float CooldownLeft(double readyAt) => (float)System.Math.Max(0d, readyAt - Time.timeAsDouble);

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            move = new InputAction("Move To", InputActionType.Button, "<Mouse>/rightButton");
            point = new InputAction("Aim", InputActionType.PassThrough, "<Mouse>/position");
            strike = new InputAction("Slash", InputActionType.Button, "<Mouse>/leftButton");
            dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/space");
            azureSlash = new InputAction("Azure Slash", InputActionType.Button, "<Keyboard>/q");
            InitializeCloneSlashInput();
            InitializeWindstepInput();
            swordTyphoonInput = new InputAction("Sword Typhoon", InputActionType.Button, "<Keyboard>/r");
            var bladeVisual = body.gameObject.AddComponent<HongGildongBladeVisual>();
            bladeVisual.Bind(runFront != null && runFront.Length > 1 ? runFront[1] : null,
                runBack != null && runBack.Length > 1 ? runBack[1] : null);
            ConfigureSlashTrail();
            CreateMoveMarker();
            if (allowCombat && TryGetComponent<PlayerHealth>(out _)) gameObject.AddComponent<HongGildongHud>();
        }

        void OnEnable()
        {
            move.Enable();
            point.Enable();
            if (!allowCombat) return;
            strike.Enable();
            dash.Enable();
            azureSlash.Enable();
            cloneSlashInput.Enable();
            windstepInput.Enable();
            swordTyphoonInput.Enable();
        }
        void OnDisable()
        {
            move.Disable(); point.Disable(); strike.Disable(); dash.Disable(); azureSlash.Disable();
            cloneSlashInput.Disable();
            windstepInput.Disable();
            swordTyphoonInput.Disable();
            CancelSwordTyphoon();
            ClearWindstep();
            CancelCloneSlash();
            IsCastingAzureSlash = false;
            PrepareForAction();
            dashTimeLeft = gaitPhase = knockbackRemaining = 0f;
            invulnerableUntil = 0;
            attackTime = RecoveryEnd;
            bladeTrail.enabled = false;
        }
        void OnDestroy()
        {
            move.Dispose(); point.Dispose(); strike.Dispose(); dash.Dispose(); azureSlash.Dispose();
            cloneSlashInput.Dispose();
            windstepInput.Dispose();
            swordTyphoonInput.Dispose();
            if (moveMarker != null) Destroy(moveMarker.gameObject);
        }

        void Update()
        {
            Vector2 cursor = point.ReadValue<Vector2>();
            bool acceptInput = knockbackRemaining <= 0f;
            if (acceptInput && swordTyphoonInput.WasPressedThisFrame()) BeginSwordTyphoon(cursor);
            UpdateWindstep(acceptInput);
            if (IsCastingSwordTyphoon)
            {
                UpdateSwordTyphoon();
                return;
            }
            if (IsCastingCloneSlash)
            {
                UpdateCloneSlash();
                return;
            }

            if (IsCastingAzureSlash)
            {
                UpdateAzureSlash();
                return;
            }

            // Actions own movement until they finish; clicks during them are not queued.
            if (acceptInput && !IsDashing && !Attacking && move.WasPressedThisFrame() && TryGroundPoint(cursor, out Vector3 goal))
                SetDestination(goal);
            if (acceptInput && !IsDashing && !Attacking && dash.WasPressedThisFrame() && Time.timeAsDouble >= dashReadyAt)
                BeginDash(cursor);
            if (IsDashing)
            {
                MoveDash();
                body.flipX = left;
                body.sprite = back ? dashBack : dashFront;
                return;
            }
            if (acceptInput && azureSlash.WasPressedThisFrame() && BeginAzureSlash(cursor))
            {
                UpdateAzureSlash();
                return;
            }

            if (acceptInput && cloneSlashInput.WasPressedThisFrame() && BeginCloneSlash(cursor))
            {
                UpdateCloneSlash();
                return;
            }

            if (acceptInput && !Attacking && strike.WasPressedThisFrame()) BeginSwing(cursor);
            Vector3 offset = Attacking ? Vector3.zero : WalkOffset();
            if (offset.sqrMagnitude > 0f) Face(offset.normalized);
            float step = EffectiveWalkSpeed * Time.deltaTime;
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
            body.transform.localPosition = Vector3.up * artworkGroundOffset;
        }

        Vector3 InsideClearing(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, movementBounds.xMin, movementBounds.xMax), p.y,
                Mathf.Clamp(p.z, movementBounds.yMin, movementBounds.yMax));

        bool TryGroundPoint(Vector2 cursor, out Vector3 ground)
        {
            ground = default;
            if (!fieldCamera.pixelRect.Contains(cursor)) return false;
            Ray ray = fieldCamera.ScreenPointToRay(cursor);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return false;
            ground = ray.GetPoint(distance);
            return true;
        }

        float CoverClearance => (controller.radius + controller.skinWidth) *
            Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);

        Vector3 WalkOffset()
        {
            if (!destination.HasValue) return Vector3.zero;
            Vector3 waypoint = PalaceCover.NextPoint(transform.position, destination.Value, CoverClearance);
            return Vector3.ProjectOnPlane(waypoint - transform.position, Vector3.up);
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
            destination = InsideClearing(PalaceCover.WalkableGoal(transform.position, InsideClearing(goal), CoverClearance));
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

        internal void ApplyKnockback(Vector3 direction, float distance)
        {
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (!isActiveAndEnabled || distance <= 0f || direction.sqrMagnitude < .0001f) return;
            knockbackDirection = direction.normalized;
            knockbackRemaining = distance;
            dashTimeLeft = 0f;
            PrepareForAction();
        }

        CollisionFlags MoveOnGround(Vector3 delta)
        {
            Vector3 before = transform.position;
            float push = Mathf.Min(knockbackRemaining, KnockbackSpeed * Time.deltaTime);
            if (push > 0f) delta = knockbackDirection * push;
            Vector3 next = InsideClearing(before + delta);
            CollisionFlags flags = controller.Move(next - before + Vector3.down * (2f * Time.deltaTime));
            if (push > 0f)
            {
                float travelled = Vector3.Dot(transform.position - before, knockbackDirection);
                knockbackRemaining = (flags & CollisionFlags.Sides) != 0 || travelled + .001f < push
                    ? 0f : Mathf.Max(0f, knockbackRemaining - push);
            }
            return flags;
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

        bool BeginAzureSlash(Vector2 cursor)
        {
            if (IsCastingCloneSlash || IsCastingAzureSlash || IsDashing || Attacking || Time.timeAsDouble < azureSlashReadyAt ||
                !TryGroundPoint(cursor, out Vector3 ground))
                return false;

            Vector3 direction = Vector3.ProjectOnPlane(ground - transform.position, Vector3.up);
            azureSlashDirection = direction.sqrMagnitude > .0025f ? direction.normalized : facing.normalized;
            PrepareForAction();
            Face(azureSlashDirection);
            azureSlashStartedAt = Time.timeAsDouble;
            azureSlashReadyAt = azureSlashStartedAt + azureSlashCooldown;
            IsCastingAzureSlash = true;
            bladeTrail.enabled = false;
            return true;
        }

        void UpdateAzureSlash()
        {
            MoveOnGround(Vector3.zero);
            float elapsed = (float)(Time.timeAsDouble - azureSlashStartedAt);
            Sprite[] frames = back ? swingBack : swingFront;
            float progress = azureSlashCastTime > 0f ? elapsed / azureSlashCastTime : 1f;
            body.flipX = left;
            body.sprite = frames[Mathf.Clamp((int)(progress * frames.Length), 0, frames.Length - 1)];
            body.transform.localPosition = Vector3.up * artworkGroundOffset;

            if (elapsed < azureSlashCastTime) return;

            IsCastingAzureSlash = false;
            AzureSlashProjectile.Launch(transform, azureSlashDirection, azureSlashRange, azureSlashSpeed,
                azureSlashWidth, attackDamage * azureSlashDamageMultiplier, targetMask);
        }

        void BeginSwing(Vector2 cursor)
        {
            if (!TryGroundPoint(cursor, out Vector3 aim)) return;
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
            if (attackTime < Windup * .62f) return frames[0];
            if (attackTime < Mathf.Lerp(Windup, ContactEnd, .30f)) return frames[1];
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
            float cycleDistance = WalkCycleDistance;
            if (IsWindstepActive)
            {
                Sprite[] runFrames = back ? runBack : runFront;
                if (runFrames != null && runFrames.Length > 0)
                {
                    frames = runFrames;
                    cycleDistance = RunCycleDistance;
                }
            }

            float distance = velocity.magnitude * Time.deltaTime;
            gaitPhase = Mathf.Repeat(gaitPhase + distance / cycleDistance, 1f);
            return frames[Mathf.Min((int)(gaitPhase * frames.Length), frames.Length - 1)];
        }

        float SweepAngle(float time) => Mathf.Lerp(-halfArc, halfArc, Mathf.InverseLerp(Windup, ContactEnd, time));

        void StrikeTargets(float start, float end)
        {
            Vector3 center = transform.position + Vector3.up * .45f;
            foreach (Collider other in Physics.OverlapSphere(center, reach + .45f, targetMask, QueryTriggerInteraction.Collide))
            {
                AttackTarget target = AttackTarget.FromDamageCollider(other, out bool useSurface);
                if (target == null || !target.CanTakeDamage) continue;
                Vector3 contact = useSurface ? other.ClosestPoint(center) : other.transform.position;
                Vector3 offset = Vector3.ProjectOnPlane(contact - transform.position, Vector3.up);
                float angle = SweepSign * Vector3.SignedAngle(slashDirection, offset, Vector3.up);
                if (offset.sqrMagnitude <= reach * reach && angle >= start && angle <= end && hitThisSwing.Add(target))
                    target.ReceiveDamage(attackDamage);
            }
        }

        void OnDrawGizmosSelected()
        {
            const int segments = 24;
            Vector3 origin = transform.position + Vector3.up * .035f;
            Vector3 direction = Attacking ? slashDirection : facing;
            Gizmos.color = new Color(.42f, .90f, 1f);
            Vector3 previous = origin;
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfArc, halfArc, i / (float)segments);
                Vector3 next = origin + Quaternion.AngleAxis(angle, Vector3.up) * direction * reach;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
            Gizmos.DrawLine(previous, origin);
        }

        void ConfigureSlashTrail()
        {
            bladeTrail.widthMultiplier = .19f;
            bladeTrail.widthCurve = new AnimationCurve(
                new Keyframe(0f, .01f), new Keyframe(.34f, .62f),
                new Keyframe(.70f, 1f), new Keyframe(1f, .02f));
            slashGradient.colorKeys = new[]
            {
                new GradientColorKey(new Color(.52f, .80f, 1f), 0f),
                new GradientColorKey(new Color(.76f, .92f, 1f), .48f),
                new GradientColorKey(Color.white, .77f)
            };
        }

        void SetSlashTrailOpacity(float opacity)
        {
            if (Mathf.Approximately(lastTrailOpacity, opacity)) return;
            lastTrailOpacity = opacity;
            slashAlpha[0] = new GradientAlphaKey(0f, 0f);
            slashAlpha[1] = new GradientAlphaKey(.55f * opacity, .40f);
            slashAlpha[2] = new GradientAlphaKey(.88f * opacity, .78f);
            slashAlpha[3] = new GradientAlphaKey(0f, 1f);
            slashGradient.alphaKeys = slashAlpha;
            bladeTrail.colorGradient = slashGradient;
        }

        void DrawTrail()
        {
            bladeTrail.enabled = attackTime >= Windup && attackTime < ContactEnd + SlashTrailFade;
            if (!bladeTrail.enabled) return;
            SetSlashTrailOpacity(1f - Mathf.InverseLerp(ContactEnd, ContactEnd + SlashTrailFade, attackTime));
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
