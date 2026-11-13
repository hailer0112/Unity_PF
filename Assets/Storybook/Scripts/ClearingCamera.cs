using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(Camera))]
    public sealed class ClearingCamera : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField, Min(.5f)] float verticalSize = 2.6f;
        [SerializeField, Min(.5f)] float minimumHalfWidth = 3.9f;
        [SerializeField, Min(0)] float followSmoothTime = .12f;
        [SerializeField] Vector3 offset = new Vector3(0, 10, -8.391f);
        [SerializeField] Vector3 lookAhead = new Vector3(0, 0, .25f);
        [SerializeField] bool limitGroundView;
        [SerializeField] Transform groundViewEntrance;
        [SerializeField] Rect groundViewBounds;
        Camera view;
        Vector3 velocity;
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

        void OnPreCull() => UpdateZoom();

        void UpdateZoom()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Max(verticalSize, minimumHalfWidth / Mathf.Max(.1f, view.aspect));
        }
    }
}
