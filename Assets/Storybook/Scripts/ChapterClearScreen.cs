using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ChapterClearScreen : MonoBehaviour
    {
        [SerializeField] Button returnButton;
        [SerializeField] Text title, description, buttonLabel, status;
        [SerializeField, Tooltip("Name or build path of the future library scene.")]
        string libraryScene;

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

        void Update()
        {
            if (shown && !loading && Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                Close();
        }

        void RequestReturn()
        {
            if (loading) return;
            if (!string.IsNullOrWhiteSpace(libraryScene) && Application.CanStreamedLevelBeLoaded(libraryScene))
            {
                loading = true;
                returnButton.interactable = false;
                SceneManager.LoadSceneAsync(libraryScene);
                return;
            }

            if (status.gameObject.activeSelf)
            {
                Close();
                return;
            }
            status.text = "테스트 · 서고 씬 연결 대기";
            buttonLabel.text = "닫기";
            status.gameObject.SetActive(true);
        }

        void Close()
        {
            shown = false;
            gameObject.SetActive(false);
            closed?.Invoke();
            closed = null;
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (returnButton != null) returnButton.onClick.RemoveListener(RequestReturn);
            closed?.Invoke();
            if (font != null) Destroy(font);
        }
    }
}
