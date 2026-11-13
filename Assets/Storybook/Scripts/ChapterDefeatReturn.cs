using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerHealth), typeof(HongGildong))]
    public sealed class ChapterDefeatReturn : MonoBehaviour
    {
        PlayerHealth health;
        HongGildong player;

        void Awake()
        {
            health = GetComponent<PlayerHealth>();
            player = GetComponent<HongGildong>();
        }

        void OnEnable() => health.Died += ReturnToArchive;
        void OnDisable() => health.Died -= ReturnToArchive;

        void ReturnToArchive()
        {
            if (!StorybookTransition.TryBegin(StorybookTransition.ArchiveScene, StorybookTransition.Reason.Defeat, player))
                Debug.LogError("Cannot return to StorybookArchive after defeat.", this);
        }
    }
}
