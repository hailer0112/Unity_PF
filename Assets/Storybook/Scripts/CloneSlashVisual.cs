using UnityEngine;

namespace Storybook
{
    public sealed class CloneSlashVisual : MonoBehaviour
    {
        [SerializeField] SpriteRenderer body;
        [SerializeField] LineRenderer sweep;

        Sprite[] frames;
        float opacity, leadTime, animationTime, fadeTime;
        float impactTime;
        bool impacted;
        public Vector3 AttackOrigin { get; private set; }

        public void Initialize(Vector3 origin, Sprite[] frames, bool flip, float opacity, float leadTime,
            float animationTime, float fadeTime, Vector3 direction, float range, float halfArc)
        {
            AttackOrigin = origin;
            this.frames = frames;
            this.opacity = opacity;
            this.leadTime = leadTime;
            this.animationTime = animationTime;
            this.fadeTime = fadeTime;
            body.flipX = flip;
            sweep.enabled = false;
            for (int i = 0; i < sweep.positionCount; i++)
            {
                float angle = Mathf.Lerp(-halfArc, halfArc, i / (sweep.positionCount - 1f));
                Vector3 radial = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                sweep.SetPosition(i, origin + Vector3.up * .3f + radial * range);
            }
            Update();
        }

        public void Impact()
        {
            impacted = true;
            impactTime = Time.time;
            body.sprite = frames[frames.Length - 1];
            SetOpacity(opacity);
            sweep.enabled = true;
        }

        void Update()
        {
            if (frames == null) return;
            if (!impacted)
            {
                body.sprite = frames[FrameAt(animationTime, leadTime, frames.Length)];
                SetOpacity(opacity * Mathf.Clamp01(animationTime / .06f));
                animationTime += Time.deltaTime;
                return;
            }

            float elapsed = Time.time - impactTime;
            float alpha = fadeTime > 0f ? opacity * (1f - Mathf.Clamp01(elapsed / fadeTime)) : 0f;
            SetOpacity(alpha);
            if (elapsed >= fadeTime) Destroy(gameObject);
        }

        void SetOpacity(float alpha)
        {
            body.color = new Color(1f, 1f, 1f, alpha);
            sweep.startColor = sweep.endColor = new Color(.87f, .9f, .92f, alpha);
        }

        public static int FrameAt(float elapsed, float impactAt, int count)
        {
            if (count == 1 || elapsed >= impactAt) return count - 1;
            return Mathf.Min((int)(elapsed / impactAt * count), count - 2);
        }
    }
}
