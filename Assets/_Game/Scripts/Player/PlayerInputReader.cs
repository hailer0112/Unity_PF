using UnityEngine;
using UnityEngine.InputSystem;

namespace FairyTaleRecovery
{
    // Input ownership stays here; movement and weapons consume values only.
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputAction move;
        private InputAction point;
        public Vector2 MoveInput => Application.isFocused ? move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 PointerPosition => point.ReadValue<Vector2>();
        public bool CanAim => Application.isFocused && Mouse.current != null;

        private void Awake()
        {
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            point = new InputAction("Point", InputActionType.Value, "<Mouse>/position");
        }

        private void OnEnable() { move.Enable(); point.Enable(); }
        private void OnDisable() { move.Disable(); point.Disable(); }
        private void OnDestroy() { move.Dispose(); point.Dispose(); }
    }
}
