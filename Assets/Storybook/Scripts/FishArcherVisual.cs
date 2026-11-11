using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(FishArcherAI), typeof(MonsterHealth))]
    public sealed class FishArcherVisual : MonoBehaviour
    {
        enum Facing { Front, Back, Left, Right }
        enum Motion { Idle, Walk, Attack }

        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite[] idleFrames, walkFrames;
        [SerializeField] Sprite aimFrame, releaseFrame, recoverFrame, deathFrame;
        [SerializeField] Animator directionalAnimator;

        static readonly int[,] States =
        {
            { State("Front_Idle"), State("Back_Idle"), State("Left_Idle"), State("Right_Idle") },
            { State("Front_Walk"), State("Back_Walk"), State("Left_Walk"), State("Right_Walk") },
            { State("Front_Attack"), State("Back_Attack"), State("Left_Attack"), State("Right_Attack") }
        };

        FishArcherAI archer;
        MonsterHealth health;
        Camera view;
        Quaternion restRotation;
        Vector3 previousPosition;
        float phase, hitAt = float.NegativeInfinity, diedAt;
        bool walking, releasePending;
        Facing facing;

        bool HasDirectionalAnimator => directionalAnimator != null && directionalAnimator.runtimeAnimatorController != null;
        bool FacesLeft => HasDirectionalAnimator ? facing == Facing.Left : body.flipX;

        static int State(string name) => Animator.StringToHash("Base Layer." + name);

        void Awake()
        {
            archer = GetComponent<FishArcherAI>();
            health = GetComponent<MonsterHealth>();
            restRotation = body.transform.localRotation;
            view = Camera.main;
            // Sample the animator with travelled distance or the existing attack clock.
            if (HasDirectionalAnimator) directionalAnimator.speed = 0f;
        }

        void OnEnable()
        {
            previousPosition = transform.position;
            phase = 0f;
            walking = releasePending = false;
            health.Damaged += Hit;
            health.Died += Die;
        }

        void OnDisable()
        {
            health.Damaged -= Hit;
            health.Died -= Die;
        }

        void Hit() => hitAt = Time.time;

        void Die()
        {
            diedAt = Time.time;
            if (HasDirectionalAnimator) Sample(Motion.Idle, 0f);
        }

        public void Face(Vector3 direction)
        {
            if (!HasDirectionalAnimator && Mathf.Abs(direction.x) > .025f) body.flipX = direction.x < 0f;
        }

        internal void LockAimFacing(Vector3 direction)
        {
            if (HasDirectionalAnimator) SetFacing(direction, false);
        }

        internal void MarkRelease()
        {
            if (HasDirectionalAnimator) releasePending = true;
        }

        internal Vector3 BoltTrailOffset(Vector3 direction)
        {
            if (!HasDirectionalAnimator) return Vector3.zero;

            // Align only the rendered streak with the bow in the quarter-view sprite.
            Vector3 right = view != null ? view.transform.right : Vector3.right;
            Vector3 up = view != null ? view.transform.up * .22f : Vector3.up * .32f;
            return up + right * (Vector3.Dot(direction, right) * .27f);
        }

        void LateUpdate()
        {
            Vector3 position = transform.position;
            Vector3 displacement = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up);
            previousPosition = position;

            if (health.IsDead)
            {
                float progress = Mathf.Clamp01((Time.time - diedAt) / .5f);
                if (!HasDirectionalAnimator) body.sprite = deathFrame;
                body.transform.localRotation = restRotation * Quaternion.Euler(0, 0, (FacesLeft ? 1 : -1) * 19f * progress);
                body.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, progress));
                return;
            }

            float hit = 1f - Mathf.Clamp01((Time.time - hitAt) / .16f);
            body.transform.localRotation = restRotation * Quaternion.Euler(0, 0, (FacesLeft ? -1 : 1) * 12f * hit);

            if (HasDirectionalAnimator)
            {
                if (PlayDirectionalAttack()) return;
            }
            else
            {
                Sprite action = archer.CurrentState switch
                {
                    FishArcherAI.State.Aim => aimFrame,
                    FishArcherAI.State.Release => releaseFrame,
                    FishArcherAI.State.Recover => recoverFrame,
                    _ => null
                };
                if (action != null)
                {
                    body.sprite = action;
                    phase = 0f;
                    return;
                }
            }

            float distance = displacement.magnitude;
            bool moving = distance > .055f * Time.deltaTime;
            if (walking != moving) phase = 0f;
            walking = moving;
            phase += walking ? distance * 4f : Time.deltaTime * 1.6f;

            if (HasDirectionalAnimator)
            {
                if (walking) SetFacing(displacement, true);
                int frameCount = walking ? 4 : 2;
                phase = Mathf.Repeat(phase, frameCount);
                Sample(walking ? Motion.Walk : Motion.Idle, phase / frameCount);
                return;
            }

            Sprite[] frames = walking ? walkFrames : idleFrames;
            body.sprite = frames[(int)phase % frames.Length];
        }

        bool PlayDirectionalAttack()
        {
            // Keep one release frame even if a long frame crosses the active interval.
            float progress = releasePending ? .5f : archer.CurrentState switch
            {
                FishArcherAI.State.Aim => Mathf.Min(.49f, archer.AimProgress * .5f),
                FishArcherAI.State.Release => .5f,
                FishArcherAI.State.Recover => .5f + archer.RecoverProgress * .49f,
                _ => -1f
            };
            if (progress < 0f) return false;
            releasePending = false;
            phase = 0f;
            Sample(Motion.Attack, progress);
            return true;
        }

        void SetFacing(Vector3 direction, bool stabilizeDiagonal)
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

        void Sample(Motion motion, float progress)
        {
            directionalAnimator.Play(States[(int)motion, (int)facing], 0, progress);
            directionalAnimator.Update(0f);
        }
    }
}
