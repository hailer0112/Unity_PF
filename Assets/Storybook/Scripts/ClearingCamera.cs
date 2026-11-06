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
        Camera view;
        Vector3 velocity;
        void Awake() { view = GetComponent<Camera>(); }
        void Start()
        {
            if (followTarget == null)
            {
                var player = FindFirstObjectByType<HongGildong>();
                if (player != null) followTarget = player.transform;
            }
            if (followTarget != null) transform.position = FocusPoint() + offset;
        }
        Vector3 FocusPoint() => new Vector3(followTarget.position.x, 0, followTarget.position.z) + lookAhead;
        void LateUpdate()
        {
            if (followTarget == null) return;
            transform.position = Vector3.SmoothDamp(transform.position, FocusPoint() + offset, ref velocity, followSmoothTime);
        }
        void OnPreCull()
        {
            if (view == null) view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Max(verticalSize, minimumHalfWidth / Mathf.Max(.1f, view.aspect));
        }
    }
}
