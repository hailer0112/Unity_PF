using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ArchiveStoryEntrance : MonoBehaviour
    {
        [SerializeField] HongGildong player;
        [SerializeField] GameObject portalEffect;
        [SerializeField, Min(.1f)] float interactionRange = 1.15f;
        [SerializeField] StoryEntry[] stories;

        ArchiveStorySelection selection;
        Font font;
        GUIStyle prompt;
        int closedFrame = -1;

        internal bool CanUse(HongGildong candidate)
        {
            if (!isActiveAndEnabled || candidate == null || candidate != player || !player.isActiveAndEnabled ||
                selection != null || closedFrame == Time.frameCount || StorybookTransition.IsBusy || NpcDialogue.BlocksInteraction) return false;
            Vector3 offset = player.transform.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= interactionRange * interactionRange;
        }

        void Update()
        {
            if (CanUse(player) && Keyboard.current?.fKey.wasPressedThisFrame == true)
                OpenSelection();
        }

        void OpenSelection()
        {
            if (stories == null || stories.Length == 0) return;
            var root = new GameObject("Story selection", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            selection = root.AddComponent<ArchiveStorySelection>();
            player.enabled = false;
            selection.Show(stories, BeginTravel, CloseSelection);
        }

        bool BeginTravel(StoryEntry story) => StorybookTransition.TryBegin(story.sceneName,
            StorybookTransition.Reason.Departure, player, portalEffect);

        void CloseSelection()
        {
            selection = null;
            closedFrame = Time.frameCount;
            if (player != null && !StorybookTransition.IsBusy &&
                player.TryGetComponent(out PlayerHealth health) && !health.IsDead) player.enabled = true;
        }

        void OnGUI()
        {
            if (!CanUse(player)) return;
            if (prompt == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR" }, 19);
                prompt = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 19 };
                if (font != null) prompt.font = font;
                prompt.normal.textColor = new Color(.96f, .9f, .81f);
            }

            const float width = 290f;
            GUI.Box(new Rect((Screen.width - width) * .5f, Screen.height * .67f, width, 44f),
                "F - 동화 선택", prompt);
        }

        void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
