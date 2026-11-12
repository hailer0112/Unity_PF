using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class Elysia : MonoBehaviour
    {
        const float BreathPeriod = 2.6f;
        const float BreathLift = .01f;

        [SerializeField] Transform interactionPoint;
        [SerializeField] Transform indicatorAnchor;
        [SerializeField] SpriteRenderer artwork;
        [SerializeField] Sprite front, right, back, left;
        [SerializeField, Min(.1f)] float interactionRange = 1.35f;

        HongGildong player;
        Transform visual;
        Vector3 restPosition;
        float breathTime;

        public string NpcId => "elysia";
        public string DisplayName => "엘리시아";
        public Transform InteractionPoint => interactionPoint;
        public Transform IndicatorAnchor => indicatorAnchor;
        public bool IsInteracting { get; private set; }

        void Awake()
        {
            visual = artwork.transform;
            restPosition = visual.localPosition;
            artwork.sprite = front;
            player = FindFirstObjectByType<HongGildong>();
        }

        void Update()
        {
            UpdateInteraction();

            breathTime = Mathf.Repeat(breathTime + Time.deltaTime, BreathPeriod);
            float phase = breathTime * (2f * Mathf.PI / BreathPeriod);
            float lift = BreathLift * .5f * (1f - Mathf.Cos(phase));
            visual.localPosition = restPosition + Vector3.up * lift;
        }

        void UpdateInteraction()
        {
            var keyboard = Keyboard.current;
            bool pressedInteract = keyboard != null && keyboard.fKey.wasPressedThisFrame;

            if (IsInteracting)
            {
                if (pressedInteract || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                    !PlayerInRange(out _))
                    EndInteraction();
                return;
            }

            if (pressedInteract && PlayerInRange(out Vector3 offset))
            {
                artwork.sprite = DirectionTo(offset);
                IsInteracting = true;
            }
        }

        bool PlayerInRange(out Vector3 offset)
        {
            offset = default;
            if (player == null || !player.isActiveAndEnabled) return false;

            offset = transform.InverseTransformDirection(player.transform.position - transform.position);
            offset.y = 0f;
            return offset.sqrMagnitude <= interactionRange * interactionRange;
        }

        Sprite DirectionTo(Vector3 offset)
        {
            if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z))
                return offset.x > 0f ? right : left;

            return offset.z > 0f ? back : front;
        }

        public void EndInteraction()
        {
            IsInteracting = false;
            artwork.sprite = front;
        }

        void OnDisable()
        {
            EndInteraction();
            if (visual == null) return;

            visual.localPosition = restPosition;
            breathTime = 0f;
        }
    }
}

