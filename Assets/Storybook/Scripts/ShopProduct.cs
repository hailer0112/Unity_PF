using UnityEngine;

namespace Storybook
{
    public enum ShopProductKind { HealthOrb, ManaOrb, Relic }
    public enum ShopProductGrade { Low, Medium, High }

    [CreateAssetMenu(menuName = "Storybook/Shop Product")]
    public sealed class ShopProduct : ScriptableObject
    {
        [SerializeField] string id, displayName;
        [SerializeField] ShopProductKind kind;
        [SerializeField] ShopProductGrade grade;
        [SerializeField] Sprite icon;
        [SerializeField, Min(0)] int price, weight;
        [SerializeField, Range(0f, 1f)] float recoveryFraction;
        [SerializeField, Min(.01f)] float displayHeight = .32f;
        [SerializeField, Min(0)] int slotIndex;

        public string Id => id;
        public string DisplayName => Relic != null ? Relic.DisplayName : displayName;
        public FairyTaleRelic Relic { get; private set; }
        public string RelicId => Relic?.Id;
        public string Description => Relic?.Description;
        public ShopProductKind Kind => kind;
        public ShopProductGrade Grade => grade;
        public Sprite Icon => Relic != null ? Relic.Icon : icon;
        ShopPedestalRow priceSource;
        public int Price => priceSource != null ? priceSource.RelicPrice : price;
        public int Weight => Mathf.Max(0, weight);
        public float RecoveryFraction => recoveryFraction;
        public float DisplayHeight => Mathf.Max(.01f, displayHeight);
        public int SlotIndex => slotIndex;
        internal float DisplayMaxWidth { get; private set; } = float.PositiveInfinity;

        internal static ShopProduct ForRelic(FairyTaleRelic relic, int slot, ShopPedestalRow priceSource, float height, float maxWidth)
        {
            var product = CreateInstance<ShopProduct>();
            product.hideFlags = HideFlags.DontSave;
            product.id = $"shop-relic-{relic.Id}";
            product.kind = ShopProductKind.Relic;
            product.Relic = relic;
            product.priceSource = priceSource;
            product.slotIndex = slot;
            product.displayHeight = height;
            product.DisplayMaxWidth = Mathf.Max(.01f, maxWidth);
            return product;
        }
    }
}
