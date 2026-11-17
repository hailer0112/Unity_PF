using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Storybook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterHealth))]
    public sealed class DragonKingTridentSlam : MonoBehaviour
    {
        enum Stage { Idle, Windup, Recovery }
        enum Pattern { Trident, Eruption, TidalWave }

        [SerializeField] PlayerHealth player;
        [SerializeField] BoxCollider arenaFloor, dais;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal BoxCollider ArenaFloorForTesting => arenaFloor;
#endif
        [SerializeField] Transform shoulder, grip, tip;
        [SerializeField] SpriteRenderer weaponArt;

        [Header("Encounter")]
        [SerializeField, Min(0f)] float combatStartDelay = 2f;
        [SerializeField, Min(.01f)] float phaseTwoWindupMultiplier = .8f;

        [Header("Trident Slam")]
        [SerializeField, Min(.01f)] float radius = 2.5f;
        [SerializeField, Min(.01f)] float windupTime = 1.2f;
        [SerializeField, Min(0f)] float damage = 25f;
        [SerializeField, Min(.01f)] float recoveryTime = 1f;
        [SerializeField, Min(0f)] float repeatDelay = 1.25f;
        [SerializeField] bool repeatForTesting = true;

        [Header("Corruption Eruption")]
        [SerializeField, Min(.01f)] float eruptionRadius = 1.8f;
        [SerializeField, Min(.01f)] float eruptionWindupTime = 1.4f;
        [SerializeField, Min(0f)] float eruptionDamage = 20f;
        [SerializeField, Min(.01f)] float eruptionRecoveryTime = .8f;
        [SerializeField, Min(.01f)] float eruptionRange = 9f;
        [SerializeField, Range(-90f, 0f)] float freeArmLift = -58f;
        [SerializeField] Vector2 palmEffectOffset = new Vector2(-.9f, -.85f);

        [Header("Tidal Wave")]
        [SerializeField, Min(.01f)] float waveWindupTime = 1.5f;
        [SerializeField, Range(.05f, 1f)] float waveWidthRatio = .60f;
        [SerializeField, Min(.1f)] float waveSpeed = 8f;
        [SerializeField, Min(0f)] float waveDamage = 20f;
        [SerializeField, Min(0f)] float waveKnockback = 2f;
        [SerializeField, Min(.01f)] float waveRecoveryTime = 1f;

        [Header("Reach")]
        [SerializeField, Range(0f, 90f)] float frontHalfAngle = 60f;
        [SerializeField, Min(0f)] float frontClearance = .65f;
        [SerializeField] Vector2 shoulderArc = new Vector2(-55f, 24f);
        [SerializeField, Range(.1f, .95f)] float maxShaftDepth = .74f;

        const int RingSegments = 64;
        const float RingHeight = .032f, ShockDuration = .30f;
        const float StrikeStart = .76f;
        static readonly Quaternion StrikeLean = Quaternion.AngleAxis(24f, Vector3.right);
        static readonly Color WarningRed = new Color(1f, .19f, .24f, .95f);
        readonly Vector3[] ring = new Vector3[RingSegments];

        MonsterHealth health;
        DragonKingPhase phase;
        Transform upperBody;
        ChargedAttackWarning warning;
        LineRenderer warningOutline, shock;
        GameObject effects;
        Stage stage;
        Pattern pattern;
        Transform freeShoulder, palm;
        DragonKingEruptionVisual eruption;
        DragonKingTidalWaveVisual tidalWave;
        HongGildong playerMovement;
        DragonKingWaveLane waveLane;
        Vector3 lastWavePlayerPosition;
        float waveFront;
        bool preferWave, waveActive, waveTouchedPlayer;
        Quaternion impactFreeArm;
        Pose windupPose, impactPose, animatedPose;
        Pose defeatStart, bowedPose;
        Transform defeatHead;
        Quaternion headStart, headBowed;
        Quaternion impactShoulder, impactGrip;
        Vector3 center;
        float lockedRadius, currentWindupDuration;
        double startedAt, readyAt;
        bool hasAttacked, arenaActive;

        struct Pose
        {
            public Vector3 bodyPosition;
            public Quaternion bodyRotation, shoulderRotation, gripRotation, freeArmRotation;
        }

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            phase = GetComponent<DragonKingPhase>();
            if (player == null || arenaFloor == null || dais == null || shoulder == null || grip == null || tip == null || weaponArt == null)
            {
                Debug.LogError("Trident Slam needs its player, floor and rig references.", this);
                enabled = false;
                return;
            }

            upperBody = shoulder.parent;
            playerMovement = player.GetComponent<HongGildong>();
            effects = new GameObject("Dragon King Attack Effects");
            SceneManager.MoveGameObjectToScene(effects, gameObject.scene);
            warningOutline = CreateRing("Warning outline", 7, .045f);
            shock = CreateRing("Impact shockwave", 8, .12f);
            warning = effects.AddComponent<ChargedAttackWarning>();
            warning.BindOutline(warningOutline);

            // The rig names the arms by their on-screen side.
            freeShoulder = upperBody.Find("Left Shoulder");
            palm = freeShoulder != null ? freeShoulder.Find("Palm Socket") : null;
            var prefab = Resources.Load<DragonKingEruptionVisual>("DragonKingEruption");
            if (palm != null && prefab != null)
                eruption = Instantiate(prefab, effects.transform);
            else
                Debug.LogError("Corruption Eruption needs a Palm Socket and its effect prefab.", this);

            var wavePrefab = Resources.Load<DragonKingTidalWaveVisual>("DragonKingTidalWave");
            if (wavePrefab != null) tidalWave = Instantiate(wavePrefab, effects.transform);
            else Debug.LogError("Tidal Wave needs its effect prefab.", this);
            animatedPose = ReadPose();
        }

        void OnEnable()
        {
            if (warning == null) return;
            health.Died += OnBossDied;
            player.Died += Cancel;
        }

        void LateUpdate()
        {
            animatedPose = ReadPose();
            double now = Time.timeAsDouble;
            if (!CanFight())
            {
                if (arenaActive) Cancel();
                phase?.HoldAttacks(now, true);
                return;
            }

            if (!arenaActive)
            {
                arenaActive = true;
                readyAt = now + combatStartDelay;
            }

            if (phase != null && phase.HoldAttacks(now, stage == Stage.Idle)) return;

            switch (stage)
            {
                case Stage.Idle:
                    if (now >= readyAt && (repeatForTesting || !hasAttacked))
                        TryBegin(now);
                    break;
                case Stage.Windup:
                    TickWindup(now);
                    break;
                case Stage.Recovery:
                    TickRecovery(now);
                    break;
            }
        }

        bool CanFight()
        {
            if (health.IsDead || player == null || player.IsDead || arenaFloor == null) return false;
            Bounds bounds = arenaFloor.bounds;
            Vector3 p = player.transform.position;
            return p.x >= bounds.min.x && p.x <= bounds.max.x && p.z >= bounds.min.z && p.z <= bounds.max.z;
        }

        void TryBegin(double now)
        {
            Vector3 p = player.transform.position;
            Bounds bounds = arenaFloor.bounds;
            var ray = new Ray(new Vector3(p.x, bounds.max.y + 1f, p.z), Vector3.down);
            if (!arenaFloor.Raycast(ray, out RaycastHit hit, 2f)) return;

            // The existing trident rig faces local -Z.
            Vector3 front = Vector3.ProjectOnPlane(-transform.forward, Vector3.up);
            Vector3 offset = Vector3.ProjectOnPlane(hit.point - transform.position, Vector3.up);
            bool inFront = Vector3.Angle(front, offset) <= frontHalfAngle;
            bool canStrike = inFront && TryResolveStrike(hit.point);
            if (inFront && (preferWave || !canStrike) && tidalWave != null &&
                DragonKingWaveLane.TryBuild(arenaFloor, playerMovement.MovementBounds, hit.point, waveWidthRatio, out var lane)
                && lane.Contains(hit.point))
            {
                waveLane = lane;
                BeginWindup(Pattern.TidalWave, now);
                SetWaveWarning();
                return;
            }
            Pattern attack;
            if (canStrike)
            {
                attack = Pattern.Trident;
                lockedRadius = radius;
            }
            else
            {
                if (eruption == null || offset.sqrMagnitude > eruptionRange * eruptionRange) return;
                attack = Pattern.Eruption;
                lockedRadius = eruptionRadius;
                impactFreeArm = animatedPose.freeArmRotation * Quaternion.AngleAxis(freeArmLift + 12f, Vector3.forward);
                eruption.BeginCharge(PalmEffectPosition);
            }

            center = hit.point;
            BeginWindup(attack, now);
            SetRing(warningOutline, lockedRadius, RingHeight, WarningRed);
            warning.BeginCircle(center + Vector3.up * RingHeight, lockedRadius);
        }

        bool TryResolveStrike(Vector3 target)
        {
            if (transform.InverseTransformPoint(target).z * transform.lossyScale.z > -frontClearance) return false;

            // The wrist travels on a circle about the shoulder. Intersect that circle with
            // the sphere centered at the target whose radius is the actual grip-to-tip length.
            Vector3 shoulderToTarget = upperBody.InverseTransformPoint(target) - shoulder.localPosition;
            Vector3 offset = Quaternion.Inverse(StrikeLean) * shoulderToTarget;
            Vector3 wrist = grip.localPosition;
            float length = tip.localPosition.magnitude;
            float depth = offset.z - wrist.z;
            if (Mathf.Abs(depth) > length * maxShaftDepth) return false;

            float a = offset.x * wrist.x + offset.y * wrist.y;
            float b = offset.y * wrist.x - offset.x * wrist.y;
            float magnitude = Mathf.Sqrt(a * a + b * b);
            float projection = (offset.sqrMagnitude + wrist.sqrMagnitude - length * length) * .5f - offset.z * wrist.z;
            if (magnitude < .0001f || Mathf.Abs(projection) > magnitude) return false;

            float bearing = Mathf.Atan2(b, a);
            float sweep = Mathf.Acos(Mathf.Clamp(projection / magnitude, -1f, 1f));
            float first = Mathf.DeltaAngle(0f, (bearing - sweep) * Mathf.Rad2Deg);
            float second = Mathf.DeltaAngle(0f, (bearing + sweep) * Mathf.Rad2Deg);
            if (Mathf.Abs(first + 28f) > Mathf.Abs(second + 28f)) (first, second) = (second, first);
            return TryStrikeAngle(first, shoulderToTarget, target) || TryStrikeAngle(second, shoulderToTarget, target);
        }

        bool TryStrikeAngle(float angle, Vector3 shoulderToTarget, Vector3 target)
        {
            if (angle < shoulderArc.x || angle > shoulderArc.y) return false;

            Quaternion arm = StrikeLean * Quaternion.AngleAxis(angle, Vector3.forward);
            Vector3 hand = upperBody.TransformPoint(shoulder.localPosition + arm * grip.localPosition);
            Vector3 toHand = hand - target;
            Bounds pedestal = dais.bounds;
            // The render mesh has a lip above the movement collider.
            pedestal.Encapsulate(pedestal.max + Vector3.up * .1f);
            if (pedestal.IntersectRay(new Ray(target, toHand.normalized), out float distance) &&
                distance < toHand.magnitude) return false;

            impactShoulder = arm;
            Quaternion inverse = Quaternion.Inverse(arm);
            Vector3 shaft = (inverse * shoulderToTarget - grip.localPosition).normalized;
            Vector3 face = Vector3.ProjectOnPlane(inverse * Vector3.forward, shaft);
            impactGrip = Quaternion.LookRotation(face, shaft) *
                         Quaternion.FromToRotation(tip.localPosition, Vector3.up);
            return true;
        }

        void BeginWindup(Pattern attack, double now)
        {
            pattern = attack;
            windupPose = animatedPose;
            float duration = attack switch
            {
                Pattern.Trident => windupTime,
                Pattern.TidalWave => waveWindupTime,
                _ => eruptionWindupTime
            };
            float multiplier = phase != null && phase.IsSecondPhase ? phaseTwoWindupMultiplier : 1f;
            currentWindupDuration = Mathf.Max(.01f, duration * multiplier);
            startedAt = now;
            stage = Stage.Windup;
        }

        void TickWindup(double now)
        {
            float progress = Mathf.Clamp01((float)(now - startedAt) / currentWindupDuration);
            if (pattern == Pattern.Trident) PoseTridentWindup(progress);
            else if (pattern == Pattern.TidalWave) PoseWaveWindup(progress);
            else PoseEruptionWindup(progress);

            warning.SetProgress(progress);
            if (progress >= 1f) ResolveImpact(now);
        }

        Vector3 PalmEffectPosition => palm.TransformPoint(palmEffectOffset) - transform.forward * .12f;

        void PoseEruptionWindup(float progress)
        {
            Pose raised = windupPose;
            raised.bodyPosition.y += .11f;
            raised.bodyRotation *= Quaternion.Euler(-5f, 3f, -3f);
            raised.freeArmRotation *= Quaternion.AngleAxis(freeArmLift, Vector3.forward);

            Pose cast = windupPose;
            cast.bodyPosition.y -= .055f;
            cast.bodyRotation *= Quaternion.Euler(4f, -2f, 2f);
            cast.freeArmRotation = impactFreeArm;

            Pose gather = Blend(windupPose, raised, Ease(.06f, .67f, progress));
            Apply(Blend(gather, cast, Ease(.79f, 1f, progress)));
            eruption.UpdateCharge(PalmEffectPosition, progress);
        }

        void PoseTridentWindup(float progress)
        {
            Pose brace = windupPose;
            brace.bodyPosition.y -= .075f;
            brace.bodyRotation *= Quaternion.Euler(4f, 0f, -2f);

            Pose raised = windupPose;
            raised.bodyPosition.y += .22f;
            raised.bodyRotation *= Quaternion.Euler(-7f, -3f, 4f);
            raised.shoulderRotation *= Quaternion.AngleAxis(23f, Vector3.forward);
            raised.gripRotation *= Quaternion.AngleAxis(-13f, Vector3.forward);
            raised.freeArmRotation *= Quaternion.AngleAxis(-6f, Vector3.forward);

            Pose loaded = raised;
            loaded.bodyPosition.y += .035f;
            loaded.shoulderRotation *= Quaternion.AngleAxis(3f, Vector3.forward);

            Pose contact = windupPose;
            contact.shoulderRotation = impactShoulder;
            contact.gripRotation = impactGrip;

            Pose pose = Blend(windupPose, brace, Ease(0f, .13f, progress));
            pose = Blend(pose, raised, Ease(.11f, .60f, progress));
            pose = Blend(pose, loaded, Ease(.60f, StrikeStart, progress));
            float drop = Mathf.InverseLerp(StrikeStart, 1f, progress);
            Apply(Blend(pose, contact, drop * drop));
        }

        void PoseWaveWindup(float progress)
        {
            float draw = Ease(.05f, .65f, progress);
            float sweep = Ease(.71f, 1f, progress);
            Pose pose = windupPose;
            pose.bodyPosition.y += .15f * draw - .20f * sweep;
            pose.bodyRotation *= Quaternion.Euler(-5f * draw + 9f * sweep,
                10f * draw - 20f * sweep, -5f * draw + 9f * sweep);
            pose.freeArmRotation *= Quaternion.AngleAxis(-9f * draw + 13f * sweep, Vector3.forward);
            Apply(pose);

            Quaternion raised = windupPose.shoulderRotation * Quaternion.AngleAxis(35f, Vector3.forward);
            Quaternion yaw = Quaternion.AngleAxis(Mathf.Lerp(28f, -33f, sweep) * draw, Vector3.up);
            shoulder.rotation = yaw * upperBody.rotation * Quaternion.Slerp(windupPose.shoulderRotation, raised, draw);

            Vector3 backSwing = (Quaternion.AngleAxis(-82f, Vector3.up) * waveLane.Forward + Vector3.up * .36f).normalized;
            Vector3 swingEnd = transform.position + waveLane.Forward * .2f;
            Vector3 followThrough = Vector3.ProjectOnPlane(swingEnd - grip.position, Vector3.up).normalized;
            Vector3 shaft = Vector3.Slerp(backSwing, followThrough, sweep);
            // Keep the sprite facing the same side of the rig as it turns through the swing.
            Vector3 bladeNormal = Vector3.ProjectOnPlane(upperBody.parent.forward, shaft);
            Quaternion swingGrip = Quaternion.LookRotation(bladeNormal, shaft) *
                Quaternion.FromToRotation(tip.localPosition, Vector3.up);
            grip.rotation = Quaternion.Slerp(shoulder.rotation * windupPose.gripRotation, swingGrip, draw);
        }


        void ResolveImpact(double now)
        {
            stage = Stage.Recovery;
            startedAt = now;
            hasAttacked = true;
            warning.Hide();
            impactPose = ReadPose();
            if (pattern == Pattern.TidalWave)
            {
                preferWave = false;
                waveFront = 0f;
                waveTouchedPlayer = false;
                waveActive = true;
                lastWavePlayerPosition = player.transform.position;
                tidalWave.Launch(waveLane, tip.position);
                return;
            }
            if (pattern == Pattern.Trident)
            {
                preferWave = true;
                SetRing(shock, lockedRadius * .16f, RingHeight + .005f, new Color(1f, .68f, .41f, .95f));
            }
            else
                eruption.PlayImpact(center, lockedRadius);

            Vector3 delta = player.transform.position - center;
            if (delta.x * delta.x + delta.z * delta.z <= lockedRadius * lockedRadius)
                player.TryTakeDamage(pattern == Pattern.Trident ? damage : eruptionDamage);
        }

        void TickRecovery(double now)
        {
            float elapsed = (float)(now - startedAt);
            float duration = pattern switch
            {
                Pattern.Trident => recoveryTime,
                Pattern.TidalWave => waveRecoveryTime,
                _ => eruptionRecoveryTime
            };
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration));
            PoseRecovery(progress);
            if (pattern == Pattern.Trident) UpdateShock(elapsed);
            if (pattern == Pattern.TidalWave && waveActive) TickWave(elapsed);

            if (!arenaActive || progress < 1f || waveActive) return;
            stage = Stage.Idle;
            shock.enabled = false;
            readyAt = now + repeatDelay;
        }

        void TickWave(float elapsed)
        {
            float previous = waveFront;
            waveFront = Mathf.Min(waveLane.Length, elapsed * waveSpeed);
            Vector3 position = player.transform.position;
            if (!waveTouchedPlayer && waveLane.CrossesCrest(lastWavePlayerPosition, position, previous, waveFront))
            {
                waveTouchedPlayer = true;
                if (player.TryTakeDamage(waveDamage) && !player.IsDead)
                    playerMovement.ApplyKnockback(waveLane.Forward, waveKnockback);
                if (!waveActive) return;
            }

            lastWavePlayerPosition = position;
            if (waveFront >= waveLane.Length)
            {
                tidalWave.Clear();
                waveActive = false;
            }
            else tidalWave.Move(waveLane, waveFront, waveFront - previous, elapsed);
        }

        void PoseRecovery(float progress)
        {
            Pose pose = impactPose;
            float followThrough = Ease(0f, .16f, progress) * (1f - Ease(.16f, .40f, progress));
            float releaseStart;
            switch (pattern)
            {
                case Pattern.Trident:
                    pose.bodyPosition.y += .10f * followThrough;
                    pose.shoulderRotation *= Quaternion.Euler(-4f * followThrough, 0f, 3f * followThrough);
                    releaseStart = .13f;
                    break;
                case Pattern.Eruption:
                    pose.freeArmRotation *= Quaternion.AngleAxis(5f * followThrough, Vector3.forward);
                    releaseStart = .10f;
                    break;
                default:
                    pose.bodyRotation *= Quaternion.Euler(0f, -4f * followThrough, 2f * followThrough);
                    pose.shoulderRotation *= Quaternion.AngleAxis(-4f * followThrough, Vector3.up);
                    releaseStart = .20f;
                    break;
            }

            Apply(Blend(pose, animatedPose, Ease(releaseStart, 1f, progress)));
        }

        void UpdateShock(float elapsed)
        {
            float wave = elapsed / ShockDuration;
            if (wave < 1f)
                SetRing(shock, lockedRadius * Mathf.Lerp(.16f, 1f, wave), RingHeight + .005f,
                    new Color(1f, .49f, .29f, .95f * (1f - wave)));
            else shock.enabled = false;
        }

        static float Ease(float start, float end, float progress) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, end, progress));

        static Pose Blend(Pose from, Pose to, float progress) => new Pose
        {
            bodyPosition = Vector3.Lerp(from.bodyPosition, to.bodyPosition, progress),
            bodyRotation = Quaternion.Slerp(from.bodyRotation, to.bodyRotation, progress),
            shoulderRotation = Quaternion.Slerp(from.shoulderRotation, to.shoulderRotation, progress),
            gripRotation = Quaternion.Slerp(from.gripRotation, to.gripRotation, progress),
            freeArmRotation = Quaternion.Slerp(from.freeArmRotation, to.freeArmRotation, progress)
        };

        Pose ReadPose() => new Pose
        {
            bodyPosition = upperBody.localPosition,
            bodyRotation = upperBody.localRotation,
            shoulderRotation = shoulder.localRotation,
            gripRotation = grip.localRotation,
            freeArmRotation = freeShoulder != null ? freeShoulder.localRotation : Quaternion.identity
        };

        void Apply(Pose pose)
        {
            upperBody.SetLocalPositionAndRotation(pose.bodyPosition, pose.bodyRotation);
            shoulder.localRotation = pose.shoulderRotation;
            grip.localRotation = pose.gripRotation;
            if (freeShoulder != null) freeShoulder.localRotation = pose.freeArmRotation;
        }


        LineRenderer CreateRing(string objectName, int order, float lineWidth)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(effects.transform, false);
            child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.alignment = LineAlignment.TransformZ;
            line.positionCount = RingSegments;
            line.startWidth = line.endWidth = lineWidth;
            line.sharedMaterial = weaponArt.sharedMaterial;
            line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        void SetWaveWarning()
        {
            Vector3 origin = waveLane.Origin + Vector3.up * RingHeight;
            Vector3 edge = waveLane.Right * (waveLane.Width * .5f);
            Vector3 length = waveLane.Forward * waveLane.Length;
            warningOutline.positionCount = 4;
            warningOutline.SetPosition(0, origin - edge);
            warningOutline.SetPosition(1, origin + edge);
            warningOutline.SetPosition(2, origin + edge + length);
            warningOutline.SetPosition(3, origin - edge + length);
            warningOutline.startColor = warningOutline.endColor = WarningRed;
            warningOutline.enabled = true;
            warning.Begin(origin, waveLane.Forward, waveLane.Width, waveLane.Length);
        }

        void SetRing(LineRenderer line, float extent, float height, Color color)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = 2f * Mathf.PI * i / ring.Length;
                ring[i] = center + new Vector3(Mathf.Cos(angle) * extent, height, Mathf.Sin(angle) * extent);
            }

            line.positionCount = RingSegments;
            line.SetPositions(ring);
            line.startColor = line.endColor = color;
            line.enabled = true;
        }

        void OnBossDied() => StopAttacks(false);

        void Cancel() => StopAttacks(true);

        void StopAttacks(bool restorePose)
        {
            arenaActive = false;
            if (restorePose && stage != Stage.Idle) Apply(animatedPose);
            if (eruption != null) eruption.Clear();
            if (tidalWave != null) tidalWave.Clear();
            waveActive = false;
            stage = Stage.Idle;
            if (warning != null) warning.Hide();
            if (shock != null) shock.enabled = false;
        }

        internal void BeginDefeat()
        {
            StopAttacks(false);
            defeatStart = ReadPose();
            bowedPose = animatedPose;
            bowedPose.bodyPosition.y -= .33f;
            bowedPose.bodyRotation *= Quaternion.Euler(17f, 0f, -5f);
            bowedPose.freeArmRotation *= Quaternion.AngleAxis(14f, Vector3.forward);
            bowedPose.shoulderRotation *= Quaternion.AngleAxis(19f, Vector3.forward);

            defeatHead = upperBody.Find("Head and Beard");
            headStart = defeatHead.localRotation;
            headBowed = headStart * Quaternion.Euler(19f, 0f, -5f);
            Apply(bowedPose);

            // Solve the planted tip on the arena floor with the original grip-to-tip length.
            float length = Vector3.Distance(tip.position, grip.position);
            float drop = Mathf.Clamp01((grip.position.y - arenaFloor.bounds.max.y) / length);
            Vector3 ahead = Vector3.ProjectOnPlane(-transform.forward, Vector3.up).normalized;
            Vector3 shaft = (ahead + transform.right * .25f).normalized * Mathf.Sqrt(1f - drop * drop) - Vector3.up * drop;
            Vector3 normal = Vector3.ProjectOnPlane(upperBody.parent.forward, shaft);
            grip.rotation = Quaternion.LookRotation(normal, shaft) * Quaternion.FromToRotation(tip.localPosition, Vector3.up);
            bowedPose.gripRotation = grip.localRotation;
            Apply(defeatStart);
        }

        internal void PoseDefeat(float progress)
        {
            Apply(Blend(defeatStart, bowedPose, Ease(0f, 1f, progress)));
            defeatHead.localRotation = Quaternion.Slerp(headStart, headBowed, Ease(.25f, 1f, progress));
        }


        void OnDisable()
        {
            if (health != null) health.Died -= OnBossDied;
            if (player != null) player.Died -= Cancel;
            Cancel();
        }

        void OnDrawGizmosSelected()
        {
            if (arenaFloor == null || player == null || !player.TryGetComponent(out HongGildong movement)) return;
            DragonKingWaveLane lane = waveLane;
            if (!Application.isPlaying || pattern != Pattern.TidalWave || stage == Stage.Idle)
            {
                if (!DragonKingWaveLane.TryBuild(arenaFloor, movement.MovementBounds, player.transform.position,
                        waveWidthRatio, out lane)) return;
            }

            Vector3 start = lane.Origin + Vector3.up * .06f;
            Gizmos.color = new Color(.24f, .90f, 1f);
            Gizmos.DrawSphere(start, .12f);
            Gizmos.DrawLine(start, start + lane.Forward * lane.Length);
            Gizmos.DrawWireSphere(start + lane.Forward * lane.Length, .12f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(start, lane.Rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.forward * (lane.Length * .5f), new Vector3(lane.Width, .02f, lane.Length));
            if (waveActive)
            {
                float depth = Mathf.Min(DragonKingWaveLane.CrestDepth, waveFront);
                Gizmos.color = new Color(1f, .76f, .18f);
                Gizmos.DrawWireCube(Vector3.forward * (waveFront - depth * .5f), new Vector3(lane.Width, .18f, depth));
            }
            Gizmos.matrix = previous;
        }

        void OnDestroy()
        {
            if (effects != null) Destroy(effects);
        }
    }
}
