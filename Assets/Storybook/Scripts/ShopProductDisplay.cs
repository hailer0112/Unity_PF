using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DefaultExecutionOrder(40)]
    public sealed class ShopProductDisplay : MonoBehaviour
    {
        SpriteRenderer pedestalArtwork;
        SortingGroup sortingGroup;
        Vector3 basePosition;
        float amplitude, period;

        public ShopProduct Product { get; private set; }

        internal static void Show(ShopPedestal pedestal, ShopProduct product, float amplitude, float period, float clearance)
        {
            var artwork = pedestal.GetComponentInChildren<SpriteRenderer>();
            if (artwork == null || pedestal.ProductAnchor == null) return;

            var group = pedestal.GetComponent<SortingGroup>();
            if (group == null) group = pedestal.gameObject.AddComponent<SortingGroup>();
            group.sortingLayerID = artwork.sortingLayerID;
            group.sortingOrder = artwork.sortingOrder;

            var root = new GameObject(product.DisplayName);
            root.transform.SetParent(pedestal.ProductAnchor, false);
            root.transform.rotation = artwork.transform.rotation;
            var sprite = root.AddComponent<SpriteRenderer>();
            sprite.sprite = product.Icon;
            sprite.sortingLayerID = artwork.sortingLayerID;
            // The pedestal's own order ranges from -1 to 3 inside the shared ground-depth group.
            sprite.sortingOrder = 4;
            root.transform.localScale = Vector3.one * (product.DisplayHeight / product.Icon.bounds.size.y);

            var display = root.AddComponent<ShopProductDisplay>();
            display.Product = product;
            display.pedestalArtwork = artwork;
            display.sortingGroup = group;
            display.amplitude = Mathf.Max(0f, amplitude);
            display.period = Mathf.Max(.01f, period);
            display.basePosition = pedestal.ProductAnchor.InverseTransformVector(
                Vector3.up * (clearance + display.amplitude) + root.transform.up * (product.DisplayHeight * .5f));
            display.LateUpdate();
        }

        void LateUpdate()
        {
            // Keep the group at the pedestal's ground point while the sprite bobs above it.
            sortingGroup.sortingOrder = pedestalArtwork.sortingOrder;
            float bob = amplitude * Mathf.Sin(Time.time * (2f * Mathf.PI / period));
            transform.localPosition = basePosition + transform.parent.InverseTransformVector(Vector3.up * bob);
        }
    }
}
