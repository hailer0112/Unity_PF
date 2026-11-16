using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Storybook
{
    internal sealed class StorybookTransition : MonoBehaviour
    {
        internal const string ArchiveScene = "StorybookArchive";
        internal const string PalaceScene = "DragonPalaceChapter1";
        internal enum Reason { Departure, Purification, Defeat }

        static StorybookTransition active;
        CanvasGroup overlay;
        Font font;

        internal static bool IsBusy => active != null;

        internal static bool TryBegin(string destination, Reason reason, HongGildong player, GameObject effect = null)
        {
            if (IsBusy || !Application.CanStreamedLevelBeLoaded(destination)) return false;
            var root = new GameObject("Storybook scene transition");
            active = root.AddComponent<StorybookTransition>();
            DontDestroyOnLoad(root);
            active.StartCoroutine(active.Travel(destination, reason, player, effect));
            return true;
        }

        internal static bool TryBeginFloor(HongGildong player, System.Action emphasizePortal, System.Action changeFloor)
        {
            if (IsBusy || player == null || !player.isActiveAndEnabled) return false;
            var root = new GameObject("Palace floor transition");
            active = root.AddComponent<StorybookTransition>();
            active.StartCoroutine(active.TravelFloor(player, emphasizePortal, changeFloor));
            return true;
        }

        IEnumerator TravelFloor(HongGildong player, System.Action emphasizePortal, System.Action changeFloor)
        {
            var health = player.GetComponent<PlayerHealth>();
            bool restoreProtection = health.ChapterClearProtected;
            double windstepExpiry = player.SuspendForFloorTransition();
            health.ChapterClearProtected = true;
            try
            {
                CreateOverlay(false);
                emphasizePortal();
                yield return new WaitForSecondsRealtime(.18f);
                yield return Fade(1f, .32f);
                changeFloor();
                yield return null;
                yield return Fade(0f, .35f);
            }
            finally
            {
                if (health != null) health.ChapterClearProtected = restoreProtection;
                if (player != null && health != null && !health.IsDead) player.ResumeFromFloorTransition(windstepExpiry);
                Destroy(gameObject);
            }
        }

        IEnumerator Travel(string destination, Reason reason, HongGildong player, GameObject effect)
        {
            var sourceHealth = player != null ? player.GetComponent<PlayerHealth>() : null;
            bool restoreControls = player != null && player.enabled;
            bool restoreProtection = sourceHealth != null && sourceHealth.ChapterClearProtected;
            if (player != null) player.enabled = false;
            if (sourceHealth != null) sourceHealth.ChapterClearProtected = true;
            CreateOverlay(reason == Reason.Defeat);

            if (reason == Reason.Departure)
            {
                if (effect != null && player != null) PlayPortal(effect, player.transform.position);
                yield return new WaitForSecondsRealtime(.26f);
            }
            yield return Fade(1f, reason == Reason.Defeat ? .72f : .32f);

            AsyncOperation load;
            try { load = SceneManager.LoadSceneAsync(destination, LoadSceneMode.Single); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                load = null;
            }

            HongGildong arrived = null;
            PlayerHealth arrivalHealth = null;
            bool arrivalControls = false, arrivalProtection = false;
            if (load != null)
            {
                void OnArrival(Scene scene, LoadSceneMode mode)
                {
                    if (scene.name != destination) return;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        arrived = root.GetComponentInChildren<HongGildong>();
                        if (arrived != null) break;
                    }
                    if (arrived == null) return;
                    if (reason == Reason.Departure && destination == PalaceScene) arrived.RefillMana();
                    arrivalControls = arrived.enabled;
                    arrived.enabled = false;
                    arrivalHealth = arrived.GetComponent<PlayerHealth>();
                    if (arrivalHealth == null) return;
                    arrivalProtection = arrivalHealth.ChapterClearProtected;
                    arrivalHealth.ChapterClearProtected = true;
                }

                SceneManager.sceneLoaded += OnArrival;
                try { yield return load; }
                finally { SceneManager.sceneLoaded -= OnArrival; }
            }
            else
            {
                if (sourceHealth != null) sourceHealth.ChapterClearProtected = restoreProtection;
                arrived = player;
                arrivalControls = restoreControls;
            }

            yield return Fade(0f, .35f);
            if (arrivalHealth != null) arrivalHealth.ChapterClearProtected = arrivalProtection;
            if (arrived != null && (arrivalHealth == null || !arrivalHealth.IsDead))
                arrived.enabled = arrivalControls;
            Destroy(gameObject);
        }

        void PlayPortal(GameObject prefab, Vector3 position)
        {
            try
            {
                var visual = Instantiate(prefab, position + Vector3.up * .06f, Quaternion.identity);
                visual.transform.localScale *= .48f;
                foreach (var renderer in visual.GetComponentsInChildren<ParticleSystemRenderer>())
                {
                    renderer.enabled &= renderer.gameObject.name != "Distortion";
                    foreach (var material in renderer.sharedMaterials)
                        if (material == null || !material.shader.isSupported) renderer.enabled = false;
                }
                foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>())
                {
                    var main = particles.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }
            catch (System.Exception exception) { Debug.LogWarning($"Portal VFX skipped: {exception.Message}", this); }
        }

        IEnumerator Fade(float target, float seconds)
        {
            float start = overlay.alpha;
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                overlay.alpha = Mathf.SmoothStep(start, target, elapsed / seconds);
                yield return null;
            }
            overlay.alpha = target;
        }

        void CreateOverlay(bool defeat)
        {
            var root = new GameObject("Transition overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            overlay = root.GetComponent<CanvasGroup>();
            overlay.alpha = 0f;
            var shade = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(root.transform, false);
            Stretch((RectTransform)shade.transform);
            var image = shade.GetComponent<Image>();
            image.color = new Color(.018f, .025f, .055f);
            image.raycastTarget = false;
            if (!defeat) return;

            var caption = new GameObject("Defeat message", typeof(RectTransform), typeof(Text));
            caption.transform.SetParent(root.transform, false);
            Stretch((RectTransform)caption.transform);
            var text = caption.GetComponent<Text>();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR" }, 28);
            text.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(.96f, .9f, .81f);
            text.text = "쓰러졌습니다. 서고로 귀환합니다.";
            text.raycastTarget = false;
            Destroy(caption, .70f);
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            if (font != null) Destroy(font);
        }
    }
}
