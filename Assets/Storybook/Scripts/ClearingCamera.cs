using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(Camera))]
    public sealed class ClearingCamera : MonoBehaviour
    {
        const float BasicHitShakeStrength = .052f;
        const float BasicHitShakeDuration = .085f;

        [SerializeField] Transform followTarget;
        [SerializeField, Min(.5f)] float verticalSize = 2.6f;
        [SerializeField, Min(.5f)] float minimumHalfWidth = 3.9f;
        [SerializeField, Min(0)] float followSmoothTime = .12f;
        [SerializeField] Vector3 offset = new Vector3(0, 10, -8.391f);
        [SerializeField] Vector3 lookAhead = new Vector3(0, 0, .25f);
        [SerializeField] bool limitGroundView;
        [SerializeField] Transform groundViewEntrance;
        [SerializeField] Rect groundViewBounds;
        [Header("Clone Slash Hit Shake")]
        [SerializeField, Range(0f, BasicHitShakeStrength)] float cloneSlashShakeStrength = .04f;
        [SerializeField, Range(0f, BasicHitShakeDuration)] float cloneSlashShakeDuration = .065f;
        Camera view;
        Vector3 velocity;
        double hitShakeStartedAt, hitShakeUntil;
        float hitShakeStrength, hitShakeDuration;
        float pendingShakeStrength, pendingShakeDuration;
        bool renderingHitShake;
        bool? dungeonBossView;
        internal void SetDungeonRoom(bool bossView, bool snap)
        {
            dungeonBossView = bossView;
            if (!snap || followTarget == null) return;
            velocity = Vector3.zero;
            transform.position = FollowPosition();
        }

        void Awake() { view = GetComponent<Camera>(); }
        void Start()
        {
            if (followTarget == null)
            {
                var player = FindFirstObjectByType<HongGildong>();
                if (player != null) followTarget = player.transform;
            }
            if (followTarget != null) transform.position = FollowPosition();
        }
        Vector3 FocusPoint() => new Vector3(followTarget.position.x, 0, followTarget.position.z) + lookAhead;
        void LateUpdate()
        {
            if (followTarget == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, FollowPosition(), ref velocity, followSmoothTime);
        }
        Vector3 FollowPosition()
        {
            Vector3 position = FocusPoint() + offset;
            bool clampView = dungeonBossView ?? (limitGroundView &&
                (groundViewEntrance == null || followTarget.position.z >= groundViewEntrance.position.z));
            if (!clampView)
                return position;

            // Measure the ground footprint at the same zoom used for rendering.
            UpdateZoom();
            var ground = new Plane(Vector3.up, Vector3.zero);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int corner = 0; corner < 4; corner++)
            {
                Ray ray = view.ViewportPointToRay(new Vector3(corner % 2, corner / 2, 0f));
                if (!ground.Raycast(ray, out float distance)) return position;
                Vector3 delta = ray.GetPoint(distance) - transform.position;
                Vector2 footprint = new Vector2(delta.x, delta.z);
                min = Vector2.Min(min, footprint);
                max = Vector2.Max(max, footprint);
            }

            position.x = ClampAxis(position.x, groundViewBounds.xMin - min.x, groundViewBounds.xMax - max.x);
            position.z = ClampAxis(position.z, groundViewBounds.yMin - min.y, groundViewBounds.yMax - max.y);
            return position;
        }

        static float ClampAxis(float position, float min, float max) =>
            min <= max ? Mathf.Clamp(position, min, max) : (min + max) * .5f;

        internal void RequestCombatShake(bool defeated, bool piercing)
        {
            float strength = defeated ? .111f : piercing ? .062f : BasicHitShakeStrength;
            QueueHitShake(strength, defeated ? .14f : BasicHitShakeDuration);
        }

        internal void RequestCloneSlashShake() =>
            QueueHitShake(cloneSlashShakeStrength, cloneSlashShakeDuration);

        void QueueHitShake(float strength, float duration)
        {
            if (strength <= pendingShakeStrength || duration <= 0f) return;
            pendingShakeStrength = strength;
            pendingShakeDuration = duration;
        }

        void OnPreCull()
        {
            RestoreHitShakeView();
            UpdateZoom();
            double now = Time.unscaledTimeAsDouble;
            if (pendingShakeStrength > 0f)
            {
                // Start on the first rendered frame. Keep the strongest impulse without stacking offsets.
                if (now >= hitShakeUntil || pendingShakeStrength > hitShakeStrength ||
                    (pendingShakeStrength == hitShakeStrength && now - hitShakeStartedAt >= .045d))
                {
                    hitShakeStrength = pendingShakeStrength;
                    hitShakeDuration = pendingShakeDuration;
                    hitShakeStartedAt = now;
                    hitShakeUntil = now + hitShakeDuration;
                }
                pendingShakeStrength = 0f;
            }

            float remaining = (float)(hitShakeUntil - now);
            if (remaining <= 0f) return;
            float weight = remaining / hitShakeDuration;
            float angle = (1f - weight) * Mathf.PI * 3f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * .45f, 0f) * (hitShakeStrength * weight);
            view.worldToCameraMatrix = Matrix4x4.Translate(offset) * view.worldToCameraMatrix;
            renderingHitShake = true;
        }

        void OnPostRender() => RestoreHitShakeView();
        void OnDisable()
        {
            pendingShakeStrength = 0f;
            hitShakeUntil = 0d;
            RestoreHitShakeView();
        }

        void RestoreHitShakeView()
        {
            if (!renderingHitShake) return;
            view.ResetWorldToCameraMatrix();
            renderingHitShake = false;
        }

        void UpdateZoom()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Max(verticalSize, minimumHalfWidth / Mathf.Max(.1f, view.aspect));
        }
    }
}
