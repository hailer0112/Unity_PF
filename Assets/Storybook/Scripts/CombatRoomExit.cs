using UnityEngine;

namespace Storybook
{
    public sealed class CombatRoomExit : MonoBehaviour
    {
        [SerializeField] Collider blocker;
        [SerializeField] Transform doorVisual;
        [SerializeField, Min(0f)] float liftHeight = 1.6f;
        [SerializeField, Min(.01f)] float liftSpeed = 4f;

        Vector3 openPosition;
        public bool IsOpen { get; private set; }

        void Awake()
        {
            openPosition = doorVisual.localPosition + Vector3.up * liftHeight;
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            blocker.enabled = false;
        }

        void Update()
        {
            if (!IsOpen) return;

            doorVisual.localPosition = Vector3.MoveTowards(
                doorVisual.localPosition, openPosition, liftSpeed * Time.deltaTime);
        }
    }
}
