using UnityEngine;

namespace Storybook
{
    internal sealed class PalacePickupVisual : MonoBehaviour
    {
        const float DropDuration = .32f;
        const float CollectDuration = .26f;
        static readonly Quaternion FloorRotation = Quaternion.Euler(90f, 0f, 0f);

        Transform icon;
        Sprite glowSprite;
        SpriteRenderer shadow, aura, beam;
        SpriteRenderer[] sparks;
        Color tint;
        float startedAt, collectedAt;
        bool collecting;

        internal static PalacePickupVisual Create(Transform anchor, Transform icon, Texture2D glow, PalaceChestReward kind)
        {
            var visual = new GameObject("Reward effects").AddComponent<PalacePickupVisual>();
            visual.transform.SetParent(anchor, false);
            visual.icon = icon;
            visual.startedAt = Time.time;
            visual.tint = kind switch
            {
                PalaceChestReward.Health => new Color(1f, .28f, .32f),
                PalaceChestReward.Mana => new Color(.27f, .64f, 1f),
                PalaceChestReward.Relic => new Color(1f, .76f, .31f),
                _ => new Color(.77f, .61f, 1f)
            };
            if (glow != null)
            {
                visual.glowSprite = Sprite.Create(glow, new Rect(0, 0, glow.width, glow.height),
                    new Vector2(.5f, .5f), glow.width, 0, SpriteMeshType.FullRect);
                visual.shadow = visual.MakeGlow("Ground shadow", FloorRotation, Vector3.up * .018f,
                    new Vector2(.47f, .38f), 0, new Color(0f, 0f, 0f, .55f));
                visual.aura = visual.MakeGlow("Ground glow", FloorRotation, Vector3.up * .024f,
                    new Vector2(.52f, .46f), 1, visual.tint);
                bool relic = kind == PalaceChestReward.Relic;
                if (relic)
                    visual.beam = visual.MakeGlow("Relic beam", anchor.rotation, Vector3.up * .49f,
                        new Vector2(.17f, 1.25f), 1, Alpha(visual.tint, .26f));
                visual.sparks = new SpriteRenderer[relic ? 6 : 3];
                for (int i = 0; i < visual.sparks.Length; i++)
                    visual.sparks[i] = visual.MakeGlow("Spark", anchor.rotation, Vector3.zero,
                        Vector2.one * .055f, 3, visual.tint);
            }
            visual.Update();
            return visual;
        }

        SpriteRenderer MakeGlow(string name, Quaternion rotation, Vector3 offset, Vector2 size, int order, Color color)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.transform.SetPositionAndRotation(transform.position + offset, rotation);
            renderer.transform.localScale = new Vector3(size.x, size.y / glowSprite.bounds.size.y, 1f);
            renderer.sprite = glowSprite;
            renderer.sortingOrder = order;
            renderer.color = color;
            return renderer;
        }

        void Update()
        {
            float age = Time.time - startedAt;
            if (collecting)
            {
                float progress = (Time.time - collectedAt) / CollectDuration;
                if (progress >= 1f) { Destroy(gameObject); return; }
                for (int i = 0; i < sparks.Length; i++)
                {
                    float angle = i * Mathf.PI * 2f / sparks.Length;
                    Vector3 offset = new(Mathf.Cos(angle) * .29f, .29f + Mathf.Sin(angle) * .17f, Mathf.Sin(angle) * .14f);
                    sparks[i].transform.position = transform.position + Vector3.Lerp(Vector3.up * .17f, offset, progress);
                    sparks[i].color = Alpha(Color.Lerp(tint, Color.white, .5f), 1f - progress);
                }
                return;
            }

            float drop = Mathf.Clamp01(age / DropDuration);
            float hop = .29f * Mathf.Sin(drop * Mathf.PI);
            float bob = .036f * Mathf.Sin((age - DropDuration) * 3.7f) * drop;
            icon.position = transform.position + Vector3.up * (.16f + hop + bob);
            if (aura == null) return;

            aura.color = Alpha(tint, .25f + .08f * Mathf.Sin(age * 3.7f));
            for (int i = 0; i < sparks.Length; i++)
            {
                float phase = Mathf.Repeat(age * .72f + (float)i / sparks.Length, 1f);
                float angle = i * 2.4f + phase;
                Vector3 offset = new(Mathf.Cos(angle) * .22f, .055f + phase * (beam != null ? .55f : .30f),
                    Mathf.Sin(angle) * .15f);
                sparks[i].transform.position = transform.position + offset;
                sparks[i].color = Alpha(tint, .7f * Mathf.Sin(phase * Mathf.PI));
            }
        }

        internal void PlayCollection(ParticleSystem prefab)
        {
            if (prefab != null)
            {
                var burst = Instantiate(prefab, icon.position, Quaternion.identity, transform.parent.parent);
                var main = burst.main;
                main.startColor = Color.Lerp(tint, Color.white, .3f);
                burst.Play();
                return;
            }
            if (glowSprite == null) return;
            transform.SetParent(transform.parent.parent, true);
            collecting = true;
            collectedAt = Time.time;
            shadow.enabled = aura.enabled = false;
            if (beam != null) beam.enabled = false;
            Update();
        }

        static Color Alpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        void OnDestroy()
        {
            if (glowSprite != null) Destroy(glowSprite);
        }
    }
}
