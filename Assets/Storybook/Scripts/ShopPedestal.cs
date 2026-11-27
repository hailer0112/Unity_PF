using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DefaultExecutionOrder(40), DisallowMultipleComponent]
    public sealed class ShopPedestal : MonoBehaviour
    {
        [SerializeField] Transform productAnchor;

        public int SlotIndex { get; private set; } = -1;
        public Transform ProductAnchor => productAnchor;
        internal ShopProductDisplay Display { get; set; }
        public string SoldProductId { get; private set; }
        public bool IsSold => SoldProductId != null;
        internal bool PurchaseInProgress { get; set; }

        SortingGroup sortingGroup;
        SpriteRenderer pedestalArtwork;

        internal void BindSorting(SpriteRenderer artwork, SortingGroup group)
        {
            pedestalArtwork = artwork;
            sortingGroup = group;
        }

        internal void CompleteSale(string productId)
        {
            SoldProductId = productId;
            var sold = Display;
            Display = null;
            sold.gameObject.SetActive(false);
            Destroy(sold.gameObject);
        }

        void LateUpdate()
        {
            if (sortingGroup != null) sortingGroup.sortingOrder = pedestalArtwork.sortingOrder;
        }

        internal void SetSlot(int index)
        {
            SlotIndex = index;
            name = $"Shop Pedestal {index}";
        }
    }
}
