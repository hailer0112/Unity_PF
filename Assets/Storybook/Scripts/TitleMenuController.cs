using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class TitleMenuController : MonoBehaviour
    {
        [SerializeField] Button startButton;
        [SerializeField] Button quitButton;
        [SerializeField] CanvasGroup menu;
        [SerializeField] CanvasGroup fade;
        [SerializeField] GameObject quitNotice;

        bool leaving;
        Coroutine noticeTimer;

        void Awake()
        {
            startButton.onClick.AddListener(StartGame);
            quitButton.onClick.AddListener(QuitGame);
        }

        void OnDestroy()
        {
            startButton.onClick.RemoveListener(StartGame);
            quitButton.onClick.RemoveListener(QuitGame);
        }

        void StartGame()
        {
            if (leaving) return;
            if (!Application.CanStreamedLevelBeLoaded(StorybookTransition.ArchiveScene))
            {
                Debug.LogError("StorybookArchive must be enabled in the build scene list.", this);
                return;
            }

            leaving = true;
            menu.interactable = false;
            quitNotice.SetActive(false);
            startButton.targetGraphic.CrossFadeColor(startButton.colors.pressedColor, .025f, true, true);
            StartCoroutine(EnterArchive());
        }

        IEnumerator EnterArchive()
        {
            yield return new WaitForSecondsRealtime(.08f);
            const float fadeDuration = .18f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fade.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            yield return SceneManager.LoadSceneAsync(StorybookTransition.ArchiveScene, LoadSceneMode.Single);
        }

        void QuitGame()
        {
            if (leaving) return;
#if UNITY_WEBGL && !UNITY_EDITOR
            ShowQuitNotice();
#else
            leaving = true;
            menu.interactable = false;
            quitButton.targetGraphic.CrossFadeColor(quitButton.colors.pressedColor, .025f, true, true);
            StartCoroutine(ExitAfterFeedback());
#endif
        }

        IEnumerator ExitAfterFeedback()
        {
            yield return new WaitForSecondsRealtime(.08f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif !UNITY_WEBGL
            Application.Quit();
#endif
        }

        void ShowQuitNotice()
        {
            quitNotice.SetActive(true);
            if (noticeTimer != null) StopCoroutine(noticeTimer);
            noticeTimer = StartCoroutine(DismissQuitNotice());
        }

        IEnumerator DismissQuitNotice()
        {
            yield return new WaitForSecondsRealtime(3f);
            quitNotice.SetActive(false);
            noticeTimer = null;
        }
    }
}
