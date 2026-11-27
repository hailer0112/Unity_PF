using System;
using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class GroundShadow : MonoBehaviour
    {
        [Serializable]
        public struct Style
        {
            [SerializeField] Vector2 size;
            [SerializeField] Vector3 offset;
            [SerializeField, Range(0f, 1f)] float opacity;

            internal Vector2 Size => size;
            internal Vector3 Offset => offset;
            internal float Opacity => opacity;

            internal Style(Vector2 size, Vector3 offset, float opacity)
            {
                this.size = size;
                this.offset = offset;
                this.opacity = opacity;
            }

            internal static Style Character => new(new Vector2(.84f, .62f), new Vector3(0f, .012f, .015f), .46f);
            internal static Style Pickup => new(new Vector2(.43f, .31f), Vector3.up * .018f, .46f);
            internal static Style Pedestal => new(new Vector2(.31f, .20f), Vector3.up * .006f, .44f);
        }

        const string ShadowName = "Contact Shadow";
        static readonly Quaternion FloorRotation = Quaternion.Euler(90f, 0f, 0f);
        static GroundShadow template;

        [SerializeField] SpriteRenderer artwork;
        [SerializeField] Material pedestalMaterial;
        [SerializeField] Material legacyMaterial;

        internal static GroundShadow Attach(Transform anchor, Style style, int sortingOrder = 0,
            int sortingLayer = 0, bool onPedestal = false, bool worldAligned = false)
        {
            Transform existing = anchor.Find(ShadowName) ?? anchor.Find(nameof(GroundShadow));
            if (existing != null && existing.TryGetComponent(out GroundShadow attached))
                return attached;

            if (template == null)
            {
                var prefab = Resources.Load<GameObject>(nameof(GroundShadow));
                if (prefab == null) return null;
                template = prefab.GetComponent<GroundShadow>();
            }

            if (existing != null && existing.TryGetComponent(out MeshRenderer legacy)
                && legacy.sharedMaterial == template.legacyMaterial)
            {
                // Older room assets can still contain the pre-sprite shadow.
                existing.gameObject.SetActive(false);
                existing.name = "Legacy Contact Shadow";
                Destroy(existing.gameObject);
            }

            var shadow = Instantiate(template, anchor, false);
            shadow.name = ShadowName;

            var shadowTransform = shadow.transform;
            if (worldAligned)
                shadowTransform.SetPositionAndRotation(anchor.position + style.Offset, FloorRotation);
            else
                shadowTransform.SetLocalPositionAndRotation(style.Offset, FloorRotation);
            shadowTransform.localScale = new Vector3(style.Size.x, style.Size.y, 1f);

            var renderer = shadow.artwork;
            if (onPedestal) renderer.sharedMaterial = template.pedestalMaterial;
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            // Sprite tint can vary without a separate material or property block.
            renderer.color = new Color(1f, 1f, 1f, style.Opacity);
            return shadow;
        }
    }
}

