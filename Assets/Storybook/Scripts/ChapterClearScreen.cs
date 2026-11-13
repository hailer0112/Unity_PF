using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ChapterClearScreen : MonoBehaviour
    {
        [SerializeField] Button returnButton;
        [SerializeField] Text title, description, buttonLabel, status;

        Font font;
        Action closed;
        bool shown, loading;

        internal void Show(Action onClosed)
        {
            closed = onClosed;
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 36);
            if (font != null)
                title.font = description.font = buttonLabel.font = status.font = font;
            if (EventSystem.current == null)
            {
                var input = new GameObject("Chapter Clear UI Input");
                input.transform.SetParent(transform, false);
                input.AddComponent<EventSystem>();
                input.AddComponent<InputSystemUIInputModule>();
            }

            status.gameObject.SetActive(false);
            shown = true;
            returnButton.onClick.AddListener(RequestReturn);
            returnButton.Select();
        }

        void RequestReturn()
        {
            if (!shown || loading) return;
            var player = FindAnyObjectByType<HongGildong>();
            loading = StorybookTransition.TryBegin(StorybookTransition.ArchiveScene,
                StorybookTransition.Reason.Purification, player);
            if (loading)
            {
                returnButton.interactable = false;
                return;
            }

            status.text = "서고 씬을 불러올 수 없습니다";
            status.gameObject.SetActive(true);
        }

        void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(RequestReturn);
            closed?.Invoke();
            if (font != null) Destroy(font);
        }
    }
}
