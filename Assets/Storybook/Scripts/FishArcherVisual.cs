using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(FishArcherAI), typeof(MonsterHealth))]
    public sealed class FishArcherVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite[] idleFrames, walkFrames;
        [SerializeField] Sprite aimFrame, releaseFrame, recoverFrame, deathFrame;

        FishArcherAI archer;
        MonsterHealth health;
        Quaternion restRotation;
        Vector3 previousPosition;
        float phase, hitAt = float.NegativeInfinity, diedAt;
        bool walking;

        void Awake()
        {
            archer = GetComponent<FishArcherAI>();
            health = GetComponent<MonsterHealth>();
            restRotation = body.transform.localRotation;
        }

        void OnEnable()
        {
            previousPosition = transform.position;
            health.Damaged += Hit;
            health.Died += Die;
        }

        void OnDisable()
        {
            health.Damaged -= Hit;
            health.Died -= Die;
        }

        void Hit() => hitAt = Time.time;
        void Die() => diedAt = Time.time;

        public void Face(Vector3 direction)
        {
            if (Mathf.Abs(direction.x) > .025f) body.flipX = direction.x < 0f;
        }

        void LateUpdate()
        {
            Vector3 position = transform.position;
            float distance = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up).magnitude;
            previousPosition = position;

            if (health.IsDead)
            {
                float progress = Mathf.Clamp01((Time.time - diedAt) / .5f);
                body.sprite = deathFrame;
                body.transform.localRotation = restRotation * Quaternion.Euler(0, 0, (body.flipX ? 1 : -1) * 19f * progress);
                body.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, progress));
                return;
            }

            float hit = 1f - Mathf.Clamp01((Time.time - hitAt) / .16f);
            body.transform.localRotation = restRotation * Quaternion.Euler(0, 0, (body.flipX ? -1 : 1) * 12f * hit);

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

            bool moving = distance > .055f * Time.deltaTime;
            if (walking != moving) phase = 0f;
            walking = moving;
            phase += walking ? distance * 4f : Time.deltaTime * 1.6f;
            Sprite[] frames = walking ? walkFrames : idleFrames;
            body.sprite = frames[(int)phase % frames.Length];
        }
    }
}
