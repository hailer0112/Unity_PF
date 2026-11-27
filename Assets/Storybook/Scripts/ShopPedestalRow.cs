using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ShopPedestalRow : MonoBehaviour
    {
        const int SlotCount = 4;
        [SerializeField] ShopPedestal pedestalPrefab;
        [SerializeField] Transform merchant;
        [SerializeField] Vector3 offsetFromMerchant = new(0f, 0f, -1.25f);
        [SerializeField, Min(.01f)] float spacing = 1f;
        [SerializeField] ShopProduct[] products;
        [SerializeField, Min(0f)] float floatAmplitude = .05f, minimumClearance = .03f;
        [SerializeField, Min(.01f)] float floatPeriod = 2f;
        bool initialized;

        internal void Initialize(int floorSeed, int roomId)
        {
            if (initialized || pedestalPrefab == null || merchant == null) return;
            initialized = true;

            var random = new System.Random(unchecked(floorSeed * 397 ^ roomId));
            for (int slot = 0; slot < SlotCount; slot++)
            {
                Vector3 offset = offsetFromMerchant + Vector3.right * ((slot - (SlotCount - 1) * .5f) * spacing);
                var pedestal = Instantiate(pedestalPrefab, merchant.TransformPoint(offset), merchant.rotation, transform);
                pedestal.SetSlot(slot);
                var product = PickProduct(slot, random);
                if (product != null)
                    ShopProductDisplay.Show(pedestal, product, floatAmplitude, floatPeriod, minimumClearance);
            }
        }

        internal ShopProduct PickProduct(int slot, System.Random random)
        {
            if (products == null) return null;
            long total = 0;
            foreach (var product in products)
                if (product != null && product.SlotIndex == slot && product.Icon != null)
                    total += product.Weight;
            if (total <= 0) return null;

            double roll = random.NextDouble() * total;
            foreach (var product in products)
            {
                if (product == null || product.SlotIndex != slot || product.Icon == null) continue;
                roll -= product.Weight;
                if (roll < 0d) return product;
            }
            return null;
        }
    }
}
