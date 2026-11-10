using UnityEngine;

namespace Storybook
{
    public sealed class DragonKingRearVisibility : MonoBehaviour
    {
        [SerializeField] Transform player, boss, visualPlane;
        [SerializeField, Min(0f)] float rearDistance = .9f;
        [SerializeField, Min(0f)] float overlapHalfWidth = 2.45f;
        [SerializeField, Range(0f, 1f)] float rearOpacity = .28f;

        static readonly int Tint = Shader.PropertyToID("_Color");
        SpriteRenderer[] sprites;
        MaterialPropertyBlock properties;
        float opacity = 1f;

        void Awake()
        {
            sprites = visualPlane.GetComponentsInChildren<SpriteRenderer>();
            properties = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            bool behind = player.position.z > boss.position.z + rearDistance;
            bool overlaps = Mathf.Abs(visualPlane.InverseTransformPoint(player.position).x) < overlapHalfWidth;
            float next = Mathf.MoveTowards(opacity, behind && overlaps ? rearOpacity : 1f, 8f * Time.deltaTime);
            if (next != opacity) SetOpacity(next);
        }

        void OnDisable()
        {
            if (opacity != 1f) SetOpacity(1f);
        }

        void SetOpacity(float alpha)
        {
            opacity = alpha;
            foreach (SpriteRenderer sprite in sprites)
            {
                sprite.GetPropertyBlock(properties);
                properties.SetColor(Tint, new Color(1f, 1f, 1f, opacity));
                sprite.SetPropertyBlock(properties);
            }
        }
    }
}
