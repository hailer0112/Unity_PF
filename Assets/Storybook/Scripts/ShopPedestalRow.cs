using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ShopPedestalRow : MonoBehaviour
    {
        const int SlotCount = 4;
        const int FirstRelicSlot = 2;
        const int RelicSeedSalt = 0x52E11C;
        [SerializeField] ShopPedestal pedestalPrefab;
        [SerializeField] Transform merchant;
        [SerializeField] Vector3 offsetFromMerchant = new(0f, 0f, -1.25f);
        [SerializeField, Min(.01f)] float spacing = 1f;
        [SerializeField] ShopProduct[] products;
        [SerializeField, Min(0f)] float floatAmplitude = .05f, minimumClearance = .03f;
        [SerializeField, Min(.01f)] float floatPeriod = 2f;
        [SerializeField, Min(0)] int relicPrice = 150;
        [SerializeField, Min(.01f)] float relicDisplayHeight = .32f, relicDisplayMaxWidth = .36f;

        [Header("Product information")]
        [SerializeField, Min(.01f)] float interactionRange = .8f;
        [SerializeField] Vector3 priceOffset = new(0f, 0f, -.15f);
        [SerializeField, Min(220f)] float detailWidth = 350f;
        [SerializeField, Min(.1f)] float purchaseFeedbackDuration = 1.6f;
        [SerializeField] AudioClip purchaseSound;

        readonly ShopPedestal[] pedestals = new ShopPedestal[SlotCount];
        internal IReadOnlyList<ShopPedestal> Pedestals => pedestals;
        internal int RelicPrice => Mathf.Max(0, relicPrice);
        internal float InteractionRange => Mathf.Max(.01f, interactionRange);
        internal Vector3 PriceOffset => priceOffset;
        internal float DetailWidth => Mathf.Max(220f, detailWidth);
        readonly List<ShopProduct> relicProducts = new(SlotCount - FirstRelicSlot);
        internal float PurchaseFeedbackDuration => Mathf.Max(.1f, purchaseFeedbackDuration);
        internal AudioClip PurchaseSound => purchaseSound;
        bool initialized, relicsInitialized;

        internal ShopPedestal FindNearestOffer(Vector3 position)
        {
            ShopPedestal nearest = null;
            float distance = InteractionRange * InteractionRange;
            foreach (var pedestal in pedestals)
            {
                if (pedestal == null || pedestal.IsSold || pedestal.Display == null || !pedestal.Display.isActiveAndEnabled) continue;
                Vector3 offset = position - pedestal.transform.position;
                float candidate = offset.x * offset.x + offset.z * offset.z;
                if (candidate > distance || (nearest != null && candidate >= distance)) continue;
                nearest = pedestal;
                distance = candidate;
            }
            return nearest;
        }

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
                pedestals[slot] = pedestal;
                if (slot >= FirstRelicSlot) continue;
                var product = PickProduct(slot, random);
                if (product != null)
                    ShopProductDisplay.Show(pedestal, product, floatAmplitude, floatPeriod, minimumClearance);
            }
        }

        internal void InitializeRelics(int floorSeed, int roomId, IEnumerable<FairyTaleRelic> catalog,
            FairyTaleRelicInventory inventory)
        {
            if (!initialized || relicsInitialized || inventory == null) return;
            relicsInitialized = true;

            var available = new List<FairyTaleRelic>();
            foreach (var relic in catalog)
                if (relic.Icon != null && !inventory.Contains(relic.Id))
                    available.Add(relic);

            var random = new System.Random(unchecked(floorSeed * 397 ^ roomId ^ RelicSeedSalt));
            for (int slot = FirstRelicSlot; slot < SlotCount && available.Count > 0; slot++)
            {
                int index = random.Next(available.Count);
                var product = ShopProduct.ForRelic(available[index], slot, this, relicDisplayHeight, relicDisplayMaxWidth);
                available.RemoveAt(index);
                relicProducts.Add(product);
                ShopProductDisplay.Show(pedestals[slot], product, floatAmplitude, floatPeriod, minimumClearance);
            }
        }

        void OnDestroy()
        {
            foreach (var product in relicProducts) Destroy(product);
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
