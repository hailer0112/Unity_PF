using UnityEngine;

namespace Storybook
{
    internal sealed class BrokenCrownImpact : MonoBehaviour
    {
        const float BreakAt = .085f;
        const float Lifetime = .30f;
        static readonly Color Gold = new(1f, .79f, .36f);
        SpriteRenderer whole, left, right;
        ParticleSystem sparkPrefab;
        float age;

        internal static void Play(MonsterHealth target, Sprite icon, ParticleSystem sparks)
        {
            var camera = Camera.main;
            Quaternion rotation = camera != null ? camera.transform.rotation : Quaternion.Euler(45f, 0f, 0f);
            Vector3 up = rotation * Vector3.up;
            var bar = target.GetComponentInChildren<EnemyHealthBar>();
            Vector3 top = bar != null ? bar.PositionAboveHead(up) : FindTop(target, up);

            var effect = new GameObject("Broken Crown impact").AddComponent<BrokenCrownImpact>();
            effect.transform.SetPositionAndRotation(top + up * .24f, rotation);
            effect.transform.SetParent(target.transform.parent, true);
            effect.sparkPrefab = sparks;

            float scale = .32f / Mathf.Max(icon.bounds.size.x, icon.bounds.size.y);
            effect.whole = effect.MakePiece(icon, scale, "Crown");
            Rect leftRect = icon.rect;
            leftRect.width = Mathf.Floor(leftRect.width * .5f);
            Rect rightRect = icon.rect;
            rightRect.xMin = leftRect.xMax;
            effect.left = effect.MakePiece(Slice(icon, leftRect, 1f), scale, "Left fragment");
            effect.right = effect.MakePiece(Slice(icon, rightRect, 0f), scale, "Right fragment");
            effect.left.enabled = effect.right.enabled = false;
        }

        static Sprite Slice(Sprite icon, Rect rect, float pivotX) =>
            Sprite.Create(icon.texture, rect, new Vector2(pivotX, .5f), icon.pixelsPerUnit, 0, SpriteMeshType.FullRect);

        static Vector3 FindTop(MonsterHealth target, Vector3 up)
        {
            Vector3 origin = target.transform.position;
            float height = .8f;
            foreach (var sprite in target.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!sprite.enabled || sprite.sprite == null) continue;
                Bounds bounds = sprite.bounds;
                Vector3 extents = bounds.extents;
                float extent = Mathf.Abs(up.x) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
                height = Mathf.Max(height, Vector3.Dot(bounds.center - origin, up) + extent);
            }
            return origin + up * height;
        }

        SpriteRenderer MakePiece(Sprite sprite, float scale, string name)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.transform.localScale = Vector3.one * scale;
            renderer.sprite = sprite;
            renderer.color = Gold;
            renderer.sortingOrder = 6;
            return renderer;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (age < BreakAt) return;
            if (whole.enabled)
            {
                whole.enabled = false;
                left.enabled = right.enabled = true;
                if (sparkPrefab != null)
                {
                    var burst = Instantiate(sparkPrefab, transform.position, transform.rotation, transform);
                    var main = burst.main;
                    main.startColor = Gold;
                    burst.Play();
                }
            }

            float progress = (age - BreakAt) / (Lifetime - BreakAt);
            MovePiece(left, -1f, progress);
            MovePiece(right, 1f, progress);
        }

        static void MovePiece(SpriteRenderer piece, float direction, float progress)
        {
            piece.transform.localPosition = new Vector3(direction * (.012f + progress * .14f), .13f * progress - .22f * progress * progress);
            piece.transform.localRotation = Quaternion.Euler(0f, 0f, -direction * (7f + progress * 32f));
            piece.color = new Color(Gold.r, Gold.g, Gold.b, 1f - progress);
        }

        void OnDestroy()
        {
            if (left != null) Destroy(left.sprite);
            if (right != null) Destroy(right.sprite);
        }
    }
}
