using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    internal sealed class WishEyeVisual : MonoBehaviour
    {
        const float Entrance = .10f;
        const float Exit = .15f;
        // The neutral Hong Gildong sprites share the same padding above the hat and below the feet.
        const float IdleTopPadding = .084f;
        const float IdleVisibleHeight = .77f;
        SpriteRenderer icon, body;
        Camera view;
        float startedAt, lifetime, headHeight, headGap, height, scale;

        internal static WishEyeVisual Show(Transform caster, SpriteRenderer body, Sprite idle, Camera view,
            WishDevouringEyeRelic relic, WishKind wish, bool recovered)
        {
            var eye = relic.GetEye(wish);
            if (eye == null || body == null || (idle == null && body.sprite == null)) return null;

            var visual = new GameObject("Wish eye").AddComponent<WishEyeVisual>();
            visual.transform.SetParent(caster, false);
            visual.icon = visual.GetComponent<SpriteRenderer>();
            visual.icon.sprite = eye;
            visual.body = body;
            visual.view = view;
            visual.startedAt = Time.time;
            visual.lifetime = relic.VisualDuration;
            visual.headGap = relic.HeadGap;

            var bounds = (idle != null ? idle : body.sprite).bounds;
            float bodyScale = Mathf.Abs(body.transform.lossyScale.y);
            float projectedHeight = view == null ? 1f :
                Mathf.Abs(Vector3.Dot(body.transform.up, view.transform.up));
            visual.headHeight = (bounds.max.y - bounds.size.y * IdleTopPadding) * bodyScale;
            visual.height = bounds.size.y * IdleVisibleHeight * bodyScale * projectedHeight * relic.VisualHeightRatio;
            visual.scale = visual.height / eye.bounds.size.y;
            visual.LateUpdate();

            if (recovered && relic.RecoverySparks != null)
            {
                var sparks = Instantiate(relic.RecoverySparks, body.bounds.center, Quaternion.identity, visual.transform);
                var main = sparks.main;
                main.scalingMode = ParticleSystemScalingMode.Local;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startColor = wish == WishKind.Life ? new Color(1f, .50f, .52f) : new Color(.46f, .76f, 1f);
                sparks.Play();
            }

            return visual;
        }

        void LateUpdate()
        {
            float age = Time.time - startedAt;
            if (age >= lifetime || body == null)
            {
                Remove();
                return;
            }

            float entrance = Mathf.Clamp01(age / Mathf.Min(Entrance, lifetime * .4f));
            float exitDuration = Mathf.Min(Exit, lifetime * .4f);
            float exit = Mathf.Clamp01((age - (lifetime - exitDuration)) / exitDuration);
            float hover = Mathf.Sin(age * 16f) * .025f + .16f * exit;
            Vector3 screenUp = view != null ? view.transform.up : body.transform.up;
            Vector3 anchor = body.transform.position + body.transform.up * headHeight;
            transform.SetPositionAndRotation(anchor + screenUp * (headGap + height * .5f + hover),
                view != null ? view.transform.rotation : body.transform.rotation);
            // Override any inherited scaling so the ratio uses the player's world-space height.
            Vector3 parentScale = transform.parent.lossyScale;
            float size = scale * Mathf.Lerp(.72f, 1f, entrance);
            transform.localScale = new Vector3(size / Mathf.Abs(parentScale.x), size / Mathf.Abs(parentScale.y), size);
            icon.sortingLayerID = body.sortingLayerID;
            icon.sortingOrder = body.sortingOrder + 2;
            icon.color = new Color(1f, 1f, 1f, entrance * (1f - exit));
        }

        internal void Remove()
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
