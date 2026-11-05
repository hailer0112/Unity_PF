using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(Camera))]
    public sealed class ClearingCamera : MonoBehaviour
    {
        // Show the whole playable area even when the Game view or window changes shape.
        void OnPreCull()
        {
            Camera view = GetComponent<Camera>();
            view.orthographicSize = Mathf.Max(4.5f, 6.75f / view.aspect);
        }
    }
}
