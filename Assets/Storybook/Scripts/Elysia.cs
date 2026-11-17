using UnityEngine;

namespace Storybook
{
    public sealed class Elysia : NpcDialogue
    {
        const float BreathPeriod = 2.6f;
        const float BreathLift = .01f;

        [SerializeField] Transform interactionPoint;
        [SerializeField] SpriteRenderer artwork;
        [SerializeField] Sprite front, right, back, left;

        ArchiveStoryEntrance entrance;
        Transform visual;
        Vector3 restPosition;
        float breathTime;

        public string NpcId => "elysia";
        public override string DisplayName => "엘리시아";
        public Transform InteractionPoint => interactionPoint;

        protected override void Awake()
        {
            base.Awake();
            visual = artwork.transform;
            restPosition = visual.localPosition;
            artwork.sprite = front;
            entrance = FindAnyObjectByType<ArchiveStoryEntrance>();
        }

        protected override void Update()
        {
            base.Update();

            breathTime = Mathf.Repeat(breathTime + Time.deltaTime, BreathPeriod);
            float phase = breathTime * (2f * Mathf.PI / BreathPeriod);
            float lift = BreathLift * .5f * (1f - Mathf.Cos(phase));
            visual.localPosition = restPosition + Vector3.up * lift;
        }

        protected override bool CanStart(HongGildong candidate) => entrance == null || !entrance.CanUse(candidate);

        protected override void FacePlayer(Vector3 offset)
        {
            if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z))
                artwork.sprite = offset.x > 0f ? right : left;
            else
                artwork.sprite = offset.z > 0f ? back : front;
        }

        protected override void FaceFront() => artwork.sprite = front;

        protected override void OnDisable()
        {
            base.OnDisable();
            FaceFront();
            if (visual == null) return;

            visual.localPosition = restPosition;
            breathTime = 0f;
        }
    }
}
