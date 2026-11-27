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
        public string DisplayName => displayName;
        public ShopProductKind Kind => kind;
        public ShopProductGrade Grade => grade;
        public Sprite Icon => icon;
        public int Price => price;
        public int Weight => Mathf.Max(0, weight);
        public float RecoveryFraction => recoveryFraction;
        public float DisplayHeight => Mathf.Max(.01f, displayHeight);
        public int SlotIndex => slotIndex;
    }
}
