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
        [SerializeField] Sprite[] sheathedRun = new Sprite[32];
        [SerializeField] Sprite[] sheathedRunFrontOverride = new Sprite[4];
        [SerializeField] Sprite[] sheathedRunBackOverride = new Sprite[4];
        [SerializeField] Sprite[] sheathedRunLeftOverride = new Sprite[4];
        [SerializeField] Sprite[] sheathedRunRightOverride = new Sprite[4];
        [SerializeField, Min(1f)] float sheathedRunFramesPerSecond = 8f;
        float sheathedRunTime;
        [SerializeField] Sprite[] swingFront, swingBack;
        [SerializeField] Sprite[] swingRight = new Sprite[4];
        [SerializeField] Sprite[] swingLeft = new Sprite[4];
        [SerializeField] Sprite[] swingFrontBasic = new Sprite[4];
        [SerializeField] Sprite[] swingBackBasic = new Sprite[4];
        [SerializeField] Sprite[] drawnIdle = new Sprite[4];
        [SerializeField] Sprite[] drawnRun = new Sprite[16];
        const float DrawnRunFramesPerSecond = 8f;
        float drawnRunTime;
        [SerializeField] Sprite[] walkFront, walkBack;
        [SerializeField] Sprite[] runFront, runBack;
        [SerializeField] LineRenderer bladeTrail;
        const int WhiteSweepSegments = 24;
        readonly Vector3[] whiteSweepVertices = new Vector3[(WhiteSweepSegments + 1) * 3];
        readonly Color[] whiteSweepColors = new Color[(WhiteSweepSegments + 1) * 3];
        Mesh whiteSweepMesh;
        MeshRenderer whiteSweepRenderer;
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
        [SerializeField] Sprite[] dashFrames = new Sprite[20];
        [Header("Q - Azure Slash")]
        [SerializeField, Min(.01f)] float azureSlashRange = 6f;
        [SerializeField, Min(.01f)] float azureSlashSpeed = 12f;
        [SerializeField, Min(.01f)] float azureSlashWidth = 1.5f;
        [SerializeField, Min(0f)] float azureSlashDamageMultiplier = 2f;
        [SerializeField, Min(0f)] float azureSlashCooldown = 5f;
        [SerializeField, Min(0f)] float azureSlashCastTime = .3f;
        [SerializeField] Sprite[] azureSlashFrames = new Sprite[16];
        [SerializeField] Sprite azureSlashProjectileRight, azureSlashProjectileLeft;
        [Header("Move Marker")]
        [SerializeField, Min(.01f)] float markerRadius = .22f;
        [SerializeField, Min(.001f)] float markerWidth = .035f;
        [SerializeField, Min(.001f)] float markerHeight = .025f;
        [SerializeField] Color markerColor = new Color(.25f, 1f, .65f, .9f);
        [SerializeField] Material markerMaterial;

        const float Windup = .12f, ContactEnd = .29f, RecoveryEnd = .42f;
        const float KnockbackSpeed = 8f;
        const double CombatStanceDuration = 20d, SheatheDuration = .18d;

        enum WeaponStance { Sheathed, Drawn, Sheathing }
        WeaponStance weaponStance;
        double weaponStanceChangeAt;
        bool basicSwingDrawsWeapon;

        public bool IsWeaponDrawn => weaponStance != WeaponStance.Sheathed;
        public bool IsSheathingWeapon => weaponStance == WeaponStance.Sheathing;
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
        float SweepSign => attackLeft == attackBack ? -1f : 1f;
        public bool Attacking => attackTime < RecoveryEnd;
        public bool IsCastingAzureSlash { get; private set; }
        public bool HasMoveDestination => destination.HasValue;
        internal Rect MovementBounds => movementBounds;
        Rect? dungeonMovementBounds;

        internal void PlaceInDungeon(Vector3 position, Rect bounds)
        {
            dungeonMovementBounds = bounds;
            PrepareForAction();
            controller.enabled = false;
            transform.position = position + Vector3.up * .025f;
            controller.enabled = true;
        }
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
            InitializeMana();
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
            bladeTrail.enabled = false;
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
            if (whiteSweepRenderer != null) whiteSweepRenderer.enabled = false;
        }
        void OnDestroy()
        {
            move.Dispose(); point.Dispose(); strike.Dispose(); dash.Dispose(); azureSlash.Dispose();
            cloneSlashInput.Dispose();
            windstepInput.Dispose();
            swordTyphoonInput.Dispose();
            if (moveMarker != null) Destroy(moveMarker.gameObject);
            if (whiteSweepMesh != null) Destroy(whiteSweepMesh);
            if (whiteSweepRenderer != null) Destroy(whiteSweepRenderer.gameObject);
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            UpdateWeaponStance();
            RegenerateMana();
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
                float remaining = dashTimeLeft;
                MoveDash();
                body.sprite = DashPose(remaining);
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
            body.flipX = Attacking ? attackLeft : left;
            body.sprite = Attacking ? SwingPose() : TravelPose();
            body.transform.localPosition = Vector3.up * artworkGroundOffset;
        }

        void LateUpdate()
        {
            Sprite[] directionalSwing = DirectionalBasicSwingFrames();
            bool showWhiteSweep = attackTime >= Windup && attackTime < ContactEnd &&
                !IsDashing && !IsCastingAzureSlash && !IsCastingCloneSlash && !IsCastingSwordTyphoon &&
                body.enabled && directionalSwing != null && body.sprite == directionalSwing[2];
            if (!showWhiteSweep)
            {
                if (whiteSweepRenderer != null) whiteSweepRenderer.enabled = false;
                return;
            }

            if (whiteSweepMesh == null) CreateWhiteSweep();

            float head = SweepAngle(attackTime);
            float tail = Mathf.Max(-halfArc, head - 84f);
            float progress = Mathf.InverseLerp(Windup, ContactEnd, attackTime);
            float opacity = (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, progress))) *
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .12f, progress));
            Vector3 height = Vector3.up * .40f;
            for (int i = 0; i <= WhiteSweepSegments; i++)
            {
                float t = i / (float)WhiteSweepSegments;
                float taper = Mathf.Sin(t * Mathf.PI);
                Vector3 radial = Quaternion.AngleAxis(Mathf.Lerp(tail, head, t) * SweepSign, Vector3.up) *
                    slashDirection;
                int index = i * 3;
                float width = reach * (.04f + .25f * taper);
                whiteSweepVertices[index] = transform.InverseTransformVector(height + radial * (reach - width));
                whiteSweepVertices[index + 1] = transform.InverseTransformVector(height + radial * (reach - width * .25f));
                whiteSweepVertices[index + 2] = transform.InverseTransformVector(height + radial * reach);
                whiteSweepColors[index] = new Color(.74f, .84f, 1f, 0f);
                whiteSweepColors[index + 1] = new Color(1f, 1f, 1f, opacity * taper * .92f);
                whiteSweepColors[index + 2] = new Color(.85f, .92f, 1f, opacity * taper * .62f);
            }
            whiteSweepMesh.vertices = whiteSweepVertices;
            whiteSweepMesh.colors = whiteSweepColors;
            whiteSweepMesh.RecalculateBounds();
            whiteSweepRenderer.sortingLayerID = body.sortingLayerID;
            // Keep the sweep behind the sprite, including the hat in rear attacks.
            whiteSweepRenderer.sortingOrder = body.sortingOrder - 1;
            whiteSweepRenderer.enabled = true;
        }

        void CreateWhiteSweep()
        {
            var effect = new GameObject("White basic attack sweep", typeof(MeshFilter), typeof(MeshRenderer));
            effect.transform.SetParent(transform, false);
            whiteSweepMesh = new Mesh { name = "White basic attack sweep" };
            whiteSweepMesh.MarkDynamic();
            whiteSweepMesh.vertices = whiteSweepVertices;
            var triangles = new int[WhiteSweepSegments * 12];
            for (int i = 0; i < WhiteSweepSegments * 2; i++)
            {
                int inner = i / 2 * 3 + i % 2;
                int offset = i * 6;
                triangles[offset] = inner;
                triangles[offset + 1] = inner + 1;
                triangles[offset + 2] = inner + 3;
                triangles[offset + 3] = inner + 1;
                triangles[offset + 4] = inner + 4;
                triangles[offset + 5] = inner + 3;
            }
            whiteSweepMesh.triangles = triangles;
            effect.GetComponent<MeshFilter>().sharedMesh = whiteSweepMesh;
            whiteSweepRenderer = effect.GetComponent<MeshRenderer>();
            whiteSweepRenderer.sharedMaterial = bladeTrail.sharedMaterial;
            whiteSweepRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            whiteSweepRenderer.receiveShadows = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void ResetSkillCooldownsForTesting()
        {
            azureSlashReadyAt = cloneSlashReadyAt = windstepReadyAt = swordTyphoonReadyAt = dashReadyAt = 0d;
        }

        internal void MoveToTestRoom(Vector3 position) => PlaceInDungeon(position, dungeonMovementBounds ?? movementBounds);
#endif

        Vector3 InsideClearing(Vector3 p)
        {
            Rect bounds = dungeonMovementBounds ?? movementBounds;
            return new Vector3(Mathf.Clamp(p.x, bounds.xMin, bounds.xMax), p.y,
                Mathf.Clamp(p.z, bounds.yMin, bounds.yMax));
        }

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

        internal Vector3 FacingDirection => facing;

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

        Sprite DashPose(float remaining)
        {
            if (dashFrames.Length == 20)
            {
                int direction = Mathf.Abs(dashDirection.x) > Mathf.Abs(dashDirection.z)
                    ? (dashDirection.x > 0f ? 0 : 1) : (dashDirection.z > 0f ? 3 : 2);
                int frame = Mathf.Clamp((int)((1f - remaining / dashDuration) * 5f), 0, 4);
                Sprite pose = dashFrames[direction * 5 + frame];
                if (pose != null)
                {
                    body.flipX = false;
                    return pose;
                }
            }
            body.flipX = left;
            return back ? dashBack : dashFront;
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

            if (!TrySpendSkillMana(HudSkill.AzureSlash, azureSlashManaCost)) return false;
            EnterCombatStance();

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
            bool horizontal = Mathf.Abs(azureSlashDirection.x) > Mathf.Abs(azureSlashDirection.z);
            bool aimLeft = horizontal && azureSlashDirection.x < 0f;
            int direction = horizontal ? (aimLeft ? 1 : 0) : (azureSlashDirection.z > 0f ? 3 : 2);
            float progress = azureSlashCastTime > 0f ? elapsed / azureSlashCastTime : 1f;
            Sprite pose = azureSlashFrames.Length == 16
                ? azureSlashFrames[direction * 4 + Mathf.Clamp((int)(progress * 4), 0, 3)]
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

            if (elapsed < azureSlashCastTime) return;

            IsCastingAzureSlash = false;
            AzureSlashProjectile.Launch(transform, azureSlashDirection, azureSlashRange, azureSlashSpeed,
                azureSlashWidth, attackDamage * azureSlashDamageMultiplier, targetMask,
                aimLeft ? azureSlashProjectileLeft : azureSlashProjectileRight, aimLeft);
        }

        void BeginSwing(Vector2 cursor)
        {
            if (!TryGroundPoint(cursor, out Vector3 aim)) return;
            basicSwingDrawsWeapon = EnterCombatStance();
            Vector3 offset = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
            PrepareForAction();
            slashDirection = offset.sqrMagnitude > .0025f ? offset.normalized : facing;
            attackBack = Mathf.Abs(slashDirection.z) > .08f ? slashDirection.z > 0f : back;
            attackLeft = Mathf.Abs(slashDirection.x) > .08f ? slashDirection.x < 0f : left;
            hitThisSwing.Clear();
            restoredManaThisSwing = false;
            attackTime = 0f;
            gaitPhase = 0f;
        }

        Sprite[] DirectionalBasicSwingFrames()
        {
            Sprite[] frames;
            if (Mathf.Abs(slashDirection.x) > Mathf.Abs(slashDirection.z))
                frames = slashDirection.x > 0f ? swingRight : swingLeft;
            else
                frames = attackBack ? swingBackBasic : swingFrontBasic;
            return frames != null && frames.Length == 4 && System.Array.TrueForAll(frames, frame => frame != null) ? frames : null;
        }

        Sprite SwingPose()
        {
            Sprite[] directionalSwing = DirectionalBasicSwingFrames();
            Sprite[] frames = directionalSwing ?? (attackBack ? swingBack : swingFront);
            if (directionalSwing != null) body.flipX = false;
            if (attackTime < Windup * .62f)
                return !basicSwingDrawsWeapon && directionalSwing != null ? frames[3] : frames[0];
            float strikePoseStart = directionalSwing != null ? Windup : Mathf.Lerp(Windup, ContactEnd, .30f);
            if (attackTime < strikePoseStart)
                return !basicSwingDrawsWeapon && directionalSwing != null ? frames[3] : frames[1];
            if (attackTime < ContactEnd) return frames[2];
            return frames[3];
        }

        bool EnterCombatStance()
        {
            bool drawsWeapon = !IsWeaponDrawn;
            weaponStance = WeaponStance.Drawn;
            weaponStanceChangeAt = Time.timeAsDouble + CombatStanceDuration;
            return drawsWeapon;
        }

        void UpdateWeaponStance()
        {
            double now = Time.timeAsDouble;
            if (!IsWeaponDrawn || now < weaponStanceChangeAt || IsDashing || Attacking ||
                IsCastingAzureSlash || IsCastingCloneSlash || IsWindstepActive || IsCastingSwordTyphoon ||
                swordTyphoons.Exists(typhoon => typhoon != null && typhoon.isActiveAndEnabled))
                return;

            if (IsSheathingWeapon)
            {
                weaponStance = WeaponStance.Sheathed;
                return;
            }

            weaponStance = WeaponStance.Sheathing;
            weaponStanceChangeAt = now + SheatheDuration;
        }

        Sprite DrawnIdlePose()
        {
            int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.z)
                ? (facing.x > 0f ? 3 : 2) : (facing.z > 0f ? 1 : 0);
            if (drawnIdle.Length == 4 && drawnIdle[direction] != null)
                return drawnIdle[direction];

            Sprite[] frames = direction switch
            {
                0 => swingFrontBasic,
                1 => swingBackBasic,
                2 => swingLeft,
                _ => swingRight
            };
            return frames != null && frames.Length == 4 ? frames[3] : null;
        }

        Sprite TravelPose()
        {
            Vector3 velocity = Vector3.ProjectOnPlane(controller.velocity, Vector3.up);
            if (velocity.sqrMagnitude < .01f)
            {
                gaitPhase = 0f;
                sheathedRunTime = 0f;
                drawnRunTime = 0f;
                Sprite armedIdle = IsWeaponDrawn ? DrawnIdlePose() : null;
                if (armedIdle != null)
                {
                    body.flipX = false;
                    return armedIdle;
                }
                return back ? idleBack : idleFront;
            }
            bool windstep = IsWindstepActive;
            bool hasWindstepSheet = windstep && windstepFrames.Length == 20 &&
                System.Array.TrueForAll(windstepFrames, frame => frame != null);
            if (hasWindstepSheet)
            {
                Sprite windstepPose = WindstepMotionPose(velocity);
                if (windstepPose != null)
                {
                    body.flipX = false;
                    return windstepPose;
                }
            }
            if (!windstep && IsWeaponDrawn && drawnRun.Length == 16)
            {
                int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.z)
                    ? (facing.x > 0f ? 0 : 1) : (facing.z > 0f ? 3 : 2);
                int frame = (int)(drawnRunTime * DrawnRunFramesPerSecond);
                Sprite pose = drawnRun[direction * 4 + frame];
                if (pose != null)
                {
                    drawnRunTime = Mathf.Repeat(drawnRunTime + Time.deltaTime, 4f / DrawnRunFramesPerSecond);
                    body.flipX = false;
                    return pose;
                }
            }
            drawnRunTime = 0f;
            if ((!windstep || hasWindstepSheet) && sheathedRun.Length == 32 &&
                System.Array.TrueForAll(sheathedRun, frame => frame != null))
            {
                int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.z)
                    ? (facing.x > 0f ? 3 : 2) : (facing.z > 0f ? 1 : 0);
                Sprite[] directionOverride = direction switch
                {
                    0 => sheathedRunFrontOverride,
                    1 => sheathedRunBackOverride,
                    2 => sheathedRunLeftOverride,
                    3 => sheathedRunRightOverride,
                    _ => null
                };
                bool useOverride = directionOverride != null && directionOverride.Length == 4 &&
                    System.Array.TrueForAll(directionOverride, frame => frame != null);
                int frameCount = useOverride ? directionOverride.Length : 8;
                sheathedRunTime = Mathf.Repeat(sheathedRunTime + Time.deltaTime,
                    frameCount / sheathedRunFramesPerSecond);
                int frameIndex = Mathf.Min(frameCount - 1, Mathf.FloorToInt(
                    sheathedRunTime * sheathedRunFramesPerSecond));
                body.flipX = false;
                return useOverride ? directionOverride[frameIndex]
                    : sheathedRun[direction * 8 + frameIndex];
            }
            Sprite[] frames = back ? walkBack : walkFront;
            float cycleDistance = WalkCycleDistance;
            if (windstep && !hasWindstepSheet)
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
                if (offset.sqrMagnitude > reach * reach || angle < start || angle > end || !hitThisSwing.Add(target))
                    continue;
                if (DealsBasicDamage(target, attackDamage, transform.position)) RestoreManaOnBasicHit();
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

    }
}
