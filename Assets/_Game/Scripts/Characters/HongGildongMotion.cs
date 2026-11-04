using UnityEngine;

namespace FairyTaleRecovery
{
    // Rigid-joint animation: no root motion, input ownership, or combat side effects.
    [DefaultExecutionOrder(100)]
    public sealed class HongGildongMotion : MonoBehaviour
    {
        public Transform hips, chest, head, leftThigh, rightThigh, leftShin, rightShin;
        public Transform leftArm, rightArm, leftForearm, rightForearm, ponytail, leftCoat, rightCoat, scarf;
        [SerializeField, Min(0.1f)] private float strideLength = 1.8f;
        [SerializeField, Range(0f, 50f)] private float legSwing = 32f;
        [SerializeField, Min(0f)] private float blendSpeed = 12f;
        private CharacterController motor;
        private Vector3 hipRest, lastPosition;
        private float phase, weight, clock;
        private bool poseInitialized;
        public float MovementBlend => weight;

        private void OnEnable()
        {
            motor = GetComponentInParent<CharacterController>();
            poseInitialized = hips != null;
            if (poseInitialized) hipRest = hips.localPosition;
            lastPosition = transform.position;
            weight = phase = clock = 0f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || hips == null) return;
            Vector3 velocity = motor != null ? motor.velocity : (transform.position - lastPosition) / dt;
            lastPosition = transform.position;
            Animate(velocity, dt);
        }

        // Also usable by an editor preview; never moves the gameplay root.
        public void Animate(Vector3 velocity, float dt)
        {
            if (hips == null || dt <= 0f) return;
            if (!poseInitialized) { hipRest = hips.localPosition; poseInitialized = true; }
            velocity.y = 0f;
            float speed = velocity.magnitude;
            Vector3 local = transform.InverseTransformDirection(velocity);
            Vector3 direction = speed > 0.05f ? local / speed : Vector3.zero;
            weight = Mathf.MoveTowards(weight, Mathf.Clamp01(speed / 1.5f), dt * blendSpeed);
            phase += speed / strideLength * Mathf.PI * 2f * dt;
            clock += dt;
            float step = Mathf.Sin(phase) * weight;
            float breathe = Mathf.Sin(clock * 2.3f);
            hips.localPosition = hipRest + Vector3.up * (0.008f * breathe + 0.025f * Mathf.Abs(step));
            Pose(hips, direction.z * 3f * weight, 0f, -direction.x * 4f * weight);
            Pose(chest, -3f + breathe * 0.6f, step * 3f, 0f);
            Pose(head, 1f + breathe * 0.5f, -step * 2f, 0f);
            // Local forward/back and strafe components follow the mouse-facing frame.
            Pose(leftThigh, step * legSwing * direction.z, 0f, -step * legSwing * direction.x);
            Pose(rightThigh, -step * legSwing * direction.z, 0f, step * legSwing * direction.x);
            Pose(leftShin, -Mathf.Max(0f, -Mathf.Sin(phase)) * 38f * weight, 0f, 0f);
            Pose(rightShin, -Mathf.Max(0f, Mathf.Sin(phase)) * 38f * weight, 0f, 0f);
            Pose(leftArm, -step * 16f * direction.z, 0f, 10f - step * 6f * direction.x);
            Pose(rightArm, -12f + step * 8f * direction.z, -6f, -9f);
            Pose(leftForearm, -12f - Mathf.Abs(step) * 8f, 0f, 0f);
            Pose(rightForearm, -20f - Mathf.Abs(step) * 4f, 0f, 0f);
            Pose(ponytail, 8f + weight * 12f + Mathf.Sin(clock * 5f) * (2f + weight * 4f), step * 6f, step * 5f);
            Pose(leftCoat, -5f + step * 14f, 0f, 5f + weight * 3f);
            Pose(rightCoat, -5f - step * 14f, 0f, -5f - weight * 3f);
            Pose(scarf, 12f + weight * 15f + Mathf.Sin(clock * 4f) * 5f, step * 7f, 0f);
        }

        private static void Pose(Transform joint, float x, float y, float z)
        {
            if (joint != null) joint.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}
