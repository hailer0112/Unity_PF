using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DefaultExecutionOrder(40)]
    public sealed class ShopProductDisplay : MonoBehaviour
    {
        SpriteRenderer artwork;
        Transform visual;
        GameObject highlight;
        Vector3 basePosition;
        float amplitude, period;

        public ShopProduct Product { get; private set; }
        internal Vector3 DetailAnchor => transform.TransformPoint(basePosition)
            + visual.up * localTop + Vector3.up * amplitude;
        float localTop;

        internal static void Show(ShopPedestal pedestal, ShopProduct product, float amplitude, float period, float clearance)
        {
            if (pedestal.IsSold || (pedestal.Display != null && pedestal.Display.gameObject.activeSelf)) return;
            var artwork = pedestal.GetComponentInChildren<SpriteRenderer>();
            if (artwork == null || pedestal.ProductAnchor == null) return;

            var group = pedestal.GetComponent<SortingGroup>();
            if (group == null) group = pedestal.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = artwork.sortingLayerID;
            group.sortingOrder = artwork.sortingOrder;
            pedestal.BindSorting(artwork, group);

            var root = new GameObject(product.DisplayName);
            root.transform.SetParent(pedestal.ProductAnchor, false);
            var visual = new GameObject("Product visual").transform;
            visual.SetParent(root.transform, false);
            visual.rotation = artwork.transform.rotation;
            var sprite = visual.gameObject.AddComponent<SpriteRenderer>();
            sprite.sprite = product.Icon;
            sprite.sortingLayerID = artwork.sortingLayerID;
            // The pedestal's own order ranges from -1 to 3 inside the shared ground-depth group.
            sprite.sortingOrder = 4;
            Bounds bounds = product.Relic != null ? VisibleBounds(product.Icon) : product.Icon.bounds;
            float scale = Mathf.Min(product.DisplayHeight / bounds.size.y, product.DisplayMaxWidth / bounds.size.x);
            visual.localScale = Vector3.one * scale;

            var display = root.AddComponent<ShopProductDisplay>();
            display.Product = product;
            display.artwork = sprite;
            display.visual = visual;
            GroundShadow.Attach(root.transform, GroundShadow.Style.Pedestal,
                sprite.sortingOrder - 1, sprite.sortingLayerID, onPedestal: true);
            display.localTop = bounds.max.y * scale;
            pedestal.Display = display;
            display.amplitude = Mathf.Max(0f, amplitude);
            display.period = Mathf.Max(.01f, period);
            display.basePosition = pedestal.ProductAnchor.InverseTransformVector(
                Vector3.up * (clearance + display.amplitude)
                - visual.up * (bounds.min.y * scale)
                - visual.right * (bounds.center.x * scale));
            display.LateUpdate();
        }

        static Bounds VisibleBounds(Sprite sprite)
        {
            if (sprite.packed || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return sprite.bounds;

            // Sample the shop framing from alpha without changing the shared sprite importer.
            Rect rect = sprite.rect;
            float sampleScale = Mathf.Min(1f, 256f / Mathf.Max(rect.width, rect.height));
            int width = Mathf.CeilToInt(rect.width * sampleScale);
            int height = Mathf.CeilToInt(rect.height * sampleScale);
            var texture = sprite.texture;
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var sample = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                    new Vector2(rect.x / texture.width, rect.y / texture.height));
                RenderTexture.active = target;
                sample.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                var pixels = sample.GetRawTextureData<Color32>();
                Vector2Int min = new(width, height);
                Vector2Int max = new(-1, -1);
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < 16) continue;
                    var pixel = new Vector2Int(x, y);
                    min = Vector2Int.Min(min, pixel);
                    max = Vector2Int.Max(max, pixel);
                }
                if (max.x < 0) return sprite.bounds;

                var sourcePixel = new Vector2(rect.width / width, rect.height / height);
                float ppu = sprite.pixelsPerUnit;
                var lower = (Vector2.Scale(sourcePixel, min) - sprite.pivot) / ppu;
                var upper = (Vector2.Scale(sourcePixel, max + Vector2Int.one) - sprite.pivot) / ppu;
                var bounds = new Bounds();
                bounds.SetMinMax(lower, upper);
                return bounds;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(sample);
            }
        }

        internal void SetHighlighted(bool active, Material material)
        {
            if (highlight == null && active && material != null)
            {
                highlight = new GameObject("Product outline");
                highlight.transform.SetParent(visual, false);
                float offset = .008f / visual.localScale.x;
                AddOutlineSprite(Vector3.left * offset, material);
                AddOutlineSprite(Vector3.right * offset, material);
                AddOutlineSprite(Vector3.up * offset, material);
                AddOutlineSprite(Vector3.down * offset, material);
            }
            if (highlight != null) highlight.SetActive(active);
        }

        void AddOutlineSprite(Vector3 offset, Material material)
        {
            var edge = new GameObject("Outline edge").AddComponent<SpriteRenderer>();
            edge.transform.SetParent(highlight.transform, false);
            edge.transform.localPosition = offset;
            edge.sharedMaterial = material;
            edge.sprite = artwork.sprite;
            edge.color = new Color(.95f, .80f, .47f, .40f);
            edge.sortingLayerID = artwork.sortingLayerID;
            edge.sortingOrder = artwork.sortingOrder - 1;
        }

        void LateUpdate()
        {
            float bob = amplitude * Mathf.Sin(Time.time * (2f * Mathf.PI / period));
            visual.localPosition = basePosition + transform.InverseTransformVector(Vector3.up * bob);
        }
    }
}
