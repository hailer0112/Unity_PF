using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PalaceCanopyVisibility : MonoBehaviour
    {
        [SerializeField] float halfWidth;
        [SerializeField] Material[] translucentMaterials;

        Transform player;
        MeshRenderer canopy;
        Material[] solidMaterials;
        bool translucent;

        void Start()
        {
            canopy = GetComponent<MeshRenderer>();
            solidMaterials = canopy.sharedMaterials;
            var hero = FindAnyObjectByType<HongGildong>();
            if (hero != null) player = hero.transform;
        }

        void LateUpdate()
        {
            if (player == null) return;

            // The fixed quarter view projects the raised canopy into the room beyond the gate.
            Vector3 offset = player.position - transform.position;
            bool overlapsTravelLane = Mathf.Abs(offset.x) < halfWidth + .24f && offset.z > -.25f && offset.z < 2.65f;
            if (translucent == overlapsTravelLane) return;

            translucent = overlapsTravelLane;
            canopy.sharedMaterials = translucent ? translucentMaterials : solidMaterials;
            canopy.shadowCastingMode = translucent ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }
}
