using UnityEngine;

namespace FairyTaleRecovery
{
    [RequireComponent(typeof(PlayerInputReader))]
    public sealed class PlayerAim : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [SerializeField] private float groundHeight = 0f;
        [SerializeField, Min(0.001f)] private float minimumAimDistance = 0.1f;
        private PlayerInputReader input;
        public Vector3 AimDirection => transform.forward;
        public Vector3 AimPoint { get; private set; }

        private void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            if (aimCamera == null) aimCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (aimCamera == null || !input.CanAim) return;
            Vector2 pointer = input.PointerPosition;
            if (!aimCamera.pixelRect.Contains(pointer)) return;
            Ray ray = aimCamera.ScreenPointToRay(pointer);
            Plane ground = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            if (!ground.Raycast(ray, out float distance)) return;
            AimPoint = ray.GetPoint(distance);
            Vector3 direction = AimPoint - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < minimumAimDistance * minimumAimDistance) return;
            Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private void OnValidate()
        {
            turnSpeed = Mathf.Max(0f, turnSpeed);
            minimumAimDistance = Mathf.Max(0.001f, minimumAimDistance);
        }
    }
}
