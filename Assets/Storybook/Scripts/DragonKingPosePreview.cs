using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    // Present only in the Dragon King rig preview scene.
    public sealed class DragonKingPosePreview : MonoBehaviour
    {
        [SerializeField] Animator dragon;
        static readonly int PreviewPose = Animator.StringToHash("PreviewPose");

        void Update()
        {
            if (Keyboard.current?.pKey.wasPressedThisFrame == true &&
                dragon.GetCurrentAnimatorStateInfo(0).IsName("Idle"))
                dragon.SetTrigger(PreviewPose);
        }
    }
}
