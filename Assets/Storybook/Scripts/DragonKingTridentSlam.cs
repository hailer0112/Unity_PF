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
        [SerializeField] Transform shoulder, grip, tip;
        [SerializeField] SpriteRenderer weaponArt;

        [Header("Encounter")]
        [SerializeField, Min(0f)] float combatStartDelay = 2f;

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
        Quaternion waveReleaseShoulder, waveReleaseGrip;
        float waveFront;
        bool preferWave, waveActive, waveTouchedPlayer;
        Quaternion idleFreeArm, startFreeArm, impactFreeArm;
        Pose windupPose, animatedPose;
        Quaternion impactShoulder, impactGrip;
        Vector3 center;
        float lockedRadius;
        double startedAt, readyAt;
        bool hasAttacked, arenaActive;

        struct Pose
        {
            public Vector3 bodyPosition;
            public Quaternion bodyRotation, shoulderRotation, gripRotation;
        }

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
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
        }

        void OnEnable()
        {
            if (warning == null) return;
            health.Died += Cancel;
            player.Died += Cancel;
        }

        void LateUpdate()
        {
            animatedPose = ReadPose();
            if (freeShoulder != null) idleFreeArm = freeShoulder.localRotation;
            if (!CanFight())
            {
                if (arenaActive) Cancel();
                return;
            }

            double now = Time.timeAsDouble;
            if (!arenaActive)
            {
                arenaActive = true;
                readyAt = now + combatStartDelay;
            }

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
                windupPose = animatedPose;
                pattern = Pattern.TidalWave;
                startedAt = now;
                stage = Stage.Windup;
                SetWaveWarning();
                return;
            }
            if (canStrike)
            {
                pattern = Pattern.Trident;
                lockedRadius = radius;
                windupPose = animatedPose;
            }
            else
            {
                if (eruption == null || offset.sqrMagnitude > eruptionRange * eruptionRange) return;
                pattern = Pattern.Eruption;
                lockedRadius = eruptionRadius;
                startFreeArm = idleFreeArm;
                impactFreeArm = startFreeArm * Quaternion.AngleAxis(freeArmLift + 12f, Vector3.forward);
                eruption.BeginCharge(PalmEffectPosition);
            }

            center = hit.point;
            startedAt = now;
            stage = Stage.Windup;
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

        void TickWindup(double now)
        {
            float duration = pattern switch
            {
                Pattern.Trident => windupTime,
                Pattern.TidalWave => waveWindupTime,
                _ => eruptionWindupTime
            };
            float progress = Mathf.Clamp01((float)(now - startedAt) / Mathf.Max(.01f, duration));
            if (pattern == Pattern.Trident) PoseTridentWindup(progress);
            else if (pattern == Pattern.TidalWave) PoseWaveWindup(progress);
            else PoseEruptionWindup(progress);

            warning.SetProgress(progress);
            if (progress >= 1f) ResolveImpact(now);
        }

        Vector3 PalmEffectPosition => palm.TransformPoint(palmEffectOffset) - transform.forward * .12f;

        void PoseEruptionWindup(float progress)
        {
            Quaternion liftedArm = startFreeArm * Quaternion.AngleAxis(freeArmLift, Vector3.forward);
            float lift = Mathf.SmoothStep(0f, 1f, progress / .8f);
            Quaternion raised = Quaternion.Slerp(startFreeArm, liftedArm, lift);
            freeShoulder.localRotation = Quaternion.Slerp(raised, impactFreeArm,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.88f, 1f, progress)));
            eruption.UpdateCharge(PalmEffectPosition, progress);
        }

        void PoseTridentWindup(float progress)
        {
            Quaternion raisedShoulder = Quaternion.AngleAxis(24f, Vector3.forward);
            Quaternion raisedGrip = Quaternion.AngleAxis(-12f, Vector3.forward);
            if (progress < StrikeStart)
            {
                float lift = Mathf.SmoothStep(0f, 1f, progress / StrikeStart);
                Apply(windupPose, Quaternion.Slerp(windupPose.shoulderRotation, raisedShoulder, lift),
                    Quaternion.Slerp(windupPose.gripRotation, raisedGrip, lift));
            }
            else
            {
                float fall = Mathf.InverseLerp(StrikeStart, 1f, progress);
                fall *= fall;
                Apply(windupPose, Quaternion.Slerp(raisedShoulder, impactShoulder, fall),
                    Quaternion.Slerp(raisedGrip, impactGrip, fall));
            }
        }

        void PoseWaveWindup(float progress)
        {
            float lift = Mathf.SmoothStep(0f, 1f, progress / StrikeStart);
            float sweep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(StrikeStart, 1f, progress));
            Quaternion raised = windupPose.shoulderRotation * Quaternion.AngleAxis(35f, Vector3.forward);
            Quaternion yaw = Quaternion.AngleAxis(Mathf.Lerp(28f, -33f, sweep) * lift, Vector3.up);
            shoulder.rotation = yaw * upperBody.rotation * Quaternion.Slerp(windupPose.shoulderRotation, raised, lift);

            Vector3 backSwing = Quaternion.AngleAxis(82f, Vector3.up) * waveLane.Forward;
            Vector3 swingEnd = transform.position + waveLane.Forward * .2f;
            Vector3 followThrough = Vector3.ProjectOnPlane(swingEnd - grip.position, Vector3.up).normalized;
            Vector3 shaft = Vector3.Slerp(backSwing, followThrough, sweep);
            Quaternion horizontalGrip = Quaternion.LookRotation(Vector3.up, shaft) *
                Quaternion.FromToRotation(tip.localPosition, Vector3.up);
            grip.rotation = Quaternion.Slerp(shoulder.rotation * windupPose.gripRotation, horizontalGrip, lift);
        }

        void ResolveImpact(double now)
        {
            stage = Stage.Recovery;
            startedAt = now;
            hasAttacked = true;
            warning.Hide();
            if (pattern == Pattern.TidalWave)
            {
                preferWave = false;
                waveReleaseShoulder = shoulder.localRotation;
                waveReleaseGrip = grip.localRotation;
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
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, duration));
            if (pattern == Pattern.Trident) PoseTridentRecovery(elapsed, t);
            else if (pattern == Pattern.TidalWave)
            {
                shoulder.localRotation = Quaternion.Slerp(waveReleaseShoulder, animatedPose.shoulderRotation, t);
                grip.localRotation = Quaternion.Slerp(waveReleaseGrip, animatedPose.gripRotation, t);
                if (waveActive) TickWave(elapsed);
            }
            else freeShoulder.localRotation = Quaternion.Slerp(impactFreeArm, idleFreeArm, t);

            if (!arenaActive || t < 1f || waveActive) return;
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

        void PoseTridentRecovery(float elapsed, float t)
        {
            var body = new Pose
            {
                bodyPosition = Vector3.Lerp(windupPose.bodyPosition, animatedPose.bodyPosition, t),
                bodyRotation = Quaternion.Slerp(windupPose.bodyRotation, animatedPose.bodyRotation, t)
            };
            Apply(body, Quaternion.Slerp(impactShoulder, animatedPose.shoulderRotation, t),
                Quaternion.Slerp(impactGrip, animatedPose.gripRotation, t));

            float wave = elapsed / ShockDuration;
            if (wave < 1f)
                SetRing(shock, lockedRadius * Mathf.Lerp(.16f, 1f, wave), RingHeight + .005f,
                    new Color(1f, .49f, .29f, .95f * (1f - wave)));
            else shock.enabled = false;
        }

        Pose ReadPose() => new Pose
        {
            bodyPosition = upperBody.localPosition,
            bodyRotation = upperBody.localRotation,
            shoulderRotation = shoulder.localRotation,
            gripRotation = grip.localRotation
        };

        void Apply(Pose pose, Quaternion arm, Quaternion weapon)
        {
            upperBody.SetLocalPositionAndRotation(pose.bodyPosition, pose.bodyRotation);
            shoulder.localRotation = arm;
            grip.localRotation = weapon;
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

        void Cancel()
        {
            arenaActive = false;
            if (stage != Stage.Idle)
            {
                if (pattern == Pattern.Trident)
                    Apply(animatedPose, animatedPose.shoulderRotation, animatedPose.gripRotation);
                else if (pattern == Pattern.TidalWave)
                {
                    shoulder.localRotation = animatedPose.shoulderRotation;
                    grip.localRotation = animatedPose.gripRotation;
                }
                else freeShoulder.localRotation = idleFreeArm;
            }
            if (eruption != null) eruption.Clear();
            if (tidalWave != null) tidalWave.Clear();
            waveActive = false;
            stage = Stage.Idle;
            if (warning != null) warning.Hide();
            if (shock != null) shock.enabled = false;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= Cancel;
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
