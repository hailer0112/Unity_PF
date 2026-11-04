using UnityEngine;

namespace FairyTaleRecovery
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private float gravity = -20f;
        private CharacterController controller;
        private PlayerInputReader input;
        private float verticalSpeed;
        public Vector3 MoveDirection { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputReader>();
        }

        private void Update()
        {
            Vector2 axes = Vector2.ClampMagnitude(input.MoveInput, 1f);
            MoveDirection = new Vector3(axes.x, 0f, axes.y);
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            // A single owner of Move allows a later roll to replace horizontal velocity.
            Vector3 velocity = MoveDirection * moveSpeed + Vector3.up * verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        private void OnDisable() { verticalSpeed = 0f; MoveDirection = Vector3.zero; }
        private void OnValidate() { moveSpeed = Mathf.Max(0f, moveSpeed); gravity = Mathf.Min(0f, gravity); }
    }
}
