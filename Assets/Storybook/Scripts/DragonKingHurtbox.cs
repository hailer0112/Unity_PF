using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DragonKingHurtbox : MonoBehaviour
    {
        enum Region { Torso, LeftArm, RightArm, LowerTorso }
        [SerializeField] Region region;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var selected = UnityEditor.Selection.activeTransform;
            var owner = GetComponentInParent<MonsterHealth>();
            if (selected == null || owner == null ||
                (selected != owner.transform && !selected.IsChildOf(owner.transform))) return;

            var box = GetComponent<BoxCollider>();
            Gizmos.color = region switch
            {
                Region.LeftArm => new Color(.28f, .67f, 1f),
                Region.RightArm => new Color(.95f, .37f, .52f),
                Region.LowerTorso => new Color(.26f, 1f, .55f),
                _ => new Color(1f, .74f, .24f)
            };
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = previous;
        }
#endif
    }
}
