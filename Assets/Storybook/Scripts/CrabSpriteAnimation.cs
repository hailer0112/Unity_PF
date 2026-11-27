using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrabSpriteAnimation : MonoBehaviour
    {
        enum Facing { Front, Back, Left, Right }
        enum Motion { Idle, Walk, Attack }

        [SerializeField] Sprite[] idleFrames, walkFrames;
        [SerializeField, Min(1)] float idleFPS = 1.5f;
        [SerializeField, Min(.1f)] float walkFramesPerMeter = 2.8f;
        [SerializeField, Tooltip("Preview only: enable in Play mode to inspect walk frames without adding AI.")] bool previewWalk;
        [SerializeField] Animator directionalAnimator;

        static readonly int[,] States =
        {
            { State("Front_Idle"), State("Back_Idle"), State("Left_Idle"), State("Right_Idle") },
            { State("Front_Walk"), State("Back_Walk"), State("Left_Walk"), State("Right_Walk") },
            { State("Front_Attack"), State("Back_Attack"), State("Left_Attack"), State("Right_Attack") }
        };

        SpriteRenderer body;
        MonsterHealth health;
        Transform movementRoot;
        Camera view;
        Vector3 previousPosition;
        float clock;
        bool wasWalking;
        Facing facing;

        public bool ActionLocked { get; set; }
        internal bool HasDirectionalAnimator => directionalAnimator != null && directionalAnimator.runtimeAnimatorController != null;

        static int State(string name) => Animator.StringToHash("Base Layer." + name);

        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            health = GetComponentInParent<MonsterHealth>();
            movementRoot = health != null ? health.transform : transform;
            view = Camera.main;
            // Walking follows ground distance and attacks follow the AI clock.
            if (HasDirectionalAnimator) directionalAnimator.speed = 0f;
        }

        void OnEnable()
        {
            previousPosition = movementRoot.position;
            clock = 0f;
            wasWalking = false;
        }

        void LateUpdate()
        {
            if (health != null && health.IsTimeStopped) return;
            Vector3 position = movementRoot.position;
            Vector3 displacement = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
            float distance = displacement.magnitude;
            float speed = distance / Mathf.Max(Time.deltaTime, .0001f);
            previousPosition = position;
            if ((health != null && health.IsDead) || ActionLocked) return;

            bool walking = previewWalk || speed > .05f;
            if (walking != wasWalking) clock = 0f;
            wasWalking = walking;
            float walkedDistance = previewWalk && speed <= .05f ? 1.6f * Time.deltaTime : distance;
            clock += walking ? walkedDistance * walkFramesPerMeter : Time.deltaTime * idleFPS;

            if (HasDirectionalAnimator)
            {
                if (walking && speed > .05f) Face(displacement, true);
                int frameCount = walking ? 4 : 2;
                clock = Mathf.Repeat(clock, frameCount);
                Sample(walking ? Motion.Walk : Motion.Idle, clock / frameCount);
                return;
            }

            var frames = walking ? walkFrames : idleFrames;
            if (frames != null && frames.Length > 0) body.sprite = frames[(int)clock % frames.Length];
        }

        void Face(Vector3 direction, bool stabilizeDiagonal)
        {
            if (direction.sqrMagnitude < 1e-8f) return;
            Vector3 right = view != null ? view.transform.right : Vector3.right;
            Vector3 forward = view != null
                ? Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized
                : Vector3.forward;
            float x = Vector3.Dot(direction, right);
            float z = Vector3.Dot(direction, forward);
            bool horizontal = Mathf.Abs(x) > Mathf.Abs(z);
            if (stabilizeDiagonal && Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(z)) < direction.magnitude * .12f)
                horizontal = facing == Facing.Left || facing == Facing.Right;
            facing = horizontal ? (x < 0f ? Facing.Left : Facing.Right) : (z < 0f ? Facing.Front : Facing.Back);
        }

        internal void BeginDirectionalAttack(Vector3 direction)
        {
            Face(direction, false);
            Sample(Motion.Attack, 0f);
        }

        internal void SampleDirectionalAttack(float progress) => Sample(Motion.Attack, progress);

        void Sample(Motion motion, float progress)
        {
            directionalAnimator.Play(States[(int)motion, (int)facing], 0, progress);
            directionalAnimator.Update(0f);
        }
    }
}
