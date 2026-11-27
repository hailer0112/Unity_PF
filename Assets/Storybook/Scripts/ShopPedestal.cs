using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ShopPedestal : MonoBehaviour
    {
        [SerializeField] Transform productAnchor;

        public int SlotIndex { get; private set; } = -1;
        public Transform ProductAnchor => productAnchor;

        internal void SetSlot(int index)
        {
            SlotIndex = index;
            name = $"Shop Pedestal {index}";
        }
    }
}
