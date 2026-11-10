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

        [SerializeField] PlayerHealth player;
        [SerializeField] BoxCollider arenaFloor, dais;
        [SerializeField] Transform shoulder, grip, tip;
        [SerializeField] SpriteRenderer weaponArt;

        [Header("Trident Slam")]
        [SerializeField, Min(.01f)] float radius = 2.5f;
        [SerializeField, Min(.01f)] float windupTime = 1.2f;
        [SerializeField, Min(0f)] float damage = 25f;
        [SerializeField, Min(.01f)] float recoveryTime = 1f;
        [SerializeField, Min(0f)] float repeatDelay = 1.25f;
        [SerializeField] bool repeatForTesting = true;

        [Header("Reach")]
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
        Pose windupPose, animatedPose;
        Quaternion impactShoulder, impactGrip;
        Vector3 center;
        float lockedRadius;
        double startedAt, readyAt;
        bool hasAttacked;

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
            effects = new GameObject("Dragon King Trident Slam Effects");
            SceneManager.MoveGameObjectToScene(effects, gameObject.scene);
            warningOutline = CreateRing("Warning outline", 7, .045f);
            shock = CreateRing("Impact shockwave", 8, .12f);
            warning = effects.AddComponent<ChargedAttackWarning>();
            warning.BindOutline(warningOutline);
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
            if (!CanFight())
            {
                if (stage != Stage.Idle) Cancel();
                return;
            }

            double now = Time.timeAsDouble;
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
            if (!arenaFloor.Raycast(ray, out RaycastHit hit, 2f) || !TryResolveStrike(hit.point)) return;

            windupPose = animatedPose;
            center = hit.point;
            lockedRadius = radius;
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
            float progress = Mathf.Clamp01((float)(now - startedAt) / Mathf.Max(.01f, windupTime));
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

            warning.SetProgress(progress);
            if (progress < 1f) return;

            stage = Stage.Recovery;
            startedAt = now;
            hasAttacked = true;
            warning.Hide();
            SetRing(shock, lockedRadius * .16f, RingHeight + .005f, new Color(1f, .68f, .41f, .95f));

            Vector3 delta = player.transform.position - center;
            if (delta.x * delta.x + delta.z * delta.z <= lockedRadius * lockedRadius)
                player.TryTakeDamage(damage);
        }

        void TickRecovery(double now)
        {
            float elapsed = (float)(now - startedAt);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, recoveryTime));
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

            if (t < 1f) return;
            stage = Stage.Idle;
            shock.enabled = false;
            readyAt = now + repeatDelay;
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

        void SetRing(LineRenderer line, float extent, float height, Color color)
        {
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = 2f * Mathf.PI * i / ring.Length;
                ring[i] = center + new Vector3(Mathf.Cos(angle) * extent, height, Mathf.Sin(angle) * extent);
            }

            line.SetPositions(ring);
            line.startColor = line.endColor = color;
            line.enabled = true;
        }

        void Cancel()
        {
            if (stage != Stage.Idle)
                Apply(animatedPose, animatedPose.shoulderRotation, animatedPose.gripRotation);
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

        void OnDestroy()
        {
            if (effects != null) Destroy(effects);
        }
    }
}
