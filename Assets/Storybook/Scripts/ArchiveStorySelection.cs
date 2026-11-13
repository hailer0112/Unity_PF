using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Storybook
{
    [Serializable]
    internal sealed class StoryEntry
    {
        public string id;
        public string title;
        public string region;
        [TextArea] public string description;
        public Sprite thumbnail;
        public string sceneName;
        public bool locked;
    }

    internal sealed class ArchiveStorySelection : MonoBehaviour
    {
        static readonly Color Gold = new Color32(205, 166, 96, 255);
        static readonly Color Brass = new Color32(117, 87, 67, 255);
        static readonly Color Ink = new Color32(20, 17, 39, 255);
        static readonly Color Paper = new Color32(248, 231, 198, 255);
        static readonly Color Muted = new Color32(190, 177, 205, 255);
        readonly List<Image> borders = new();
        readonly List<CanvasGroup> reveals = new();

        Font font;
        bool ownsFont;
        Canvas canvas;
        Text heading, region, description, availability, enterLabel;
        Button enter;
        StoryEntry selected;
        Func<StoryEntry, bool> travel;
        Action closed;
        bool departing;

        internal void Show(IReadOnlyList<StoryEntry> stories, Func<StoryEntry, bool> onTravel, Action onClosed)
        {
            travel = onTravel;
            closed = onClosed;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 28);
            ownsFont = font != null;
            if (!ownsFont) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1152, 680);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var input = new GameObject("Story selection input");
                input.AddComponent<EventSystem>();
                input.AddComponent<InputSystemUIInputModule>();
            }

            Image dim = Picture("Dim", transform, new Color(.026f, .019f, .07f, .84f));
            Fill(dim.rectTransform, 0);
            dim.raycastTarget = true;
            RectTransform frame = Center(Picture("Gold frame", transform, Gold).rectTransform, 0, 0, 976, 580);
            Image panel = Picture("Archive panel", frame, Ink);
            Fill(panel.rectTransform, 2);

            Center(Label("동화 선택", panel.transform, 36, Paper).rectTransform, 0, 237, 540, 54);
            var close = MakeButton("닫기", panel.transform, new Color32(51, 36, 65, 255));
            Center(close.GetComponent<RectTransform>(), 409, 238, 98, 39);
            close.onClick.AddListener(Close);
            Center(Picture("Header rule", panel.transform, Brass).rectTransform, 0, 198, 880, 2);

            var scroll = Center(Rect("Book shelf", panel.transform), 0, 63, 880, 236);
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            var viewport = Picture("Shelf viewport", scroll, new Color(0, 0, 0, 0));
            Fill(viewport.rectTransform, 0);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Book cards", viewport.transform);
            content.anchorMin = content.anchorMax = new Vector2(0, .5f);
            content.pivot = new Vector2(0, .5f);
            float span = stories.Count * 216 - 24;
            content.sizeDelta = new Vector2(Mathf.Max(880, span), 220);
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = content;
            scrollRect.vertical = false;
            scrollRect.horizontal = span > 880;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            for (int i = 0; i < stories.Count; i++)
            {
                var entry = stories[i];
                float x = Mathf.Max(0, (880 - span) * .5f) + 96 + 216 * i;
                MakeCard(content, entry, x);
            }

            RectTransform detail = Center(Picture("Selection details", panel.transform, new Color32(31, 23, 52, 255)).rectTransform,
                0, -145, 880, 153);
            Center(Picture("Detail rule", detail, Brass).rectTransform, 0, 76, 880, 2);
            heading = Label("동화를 선택하세요", detail, 24, Paper, TextAnchor.MiddleLeft);
            Center(heading.rectTransform, -140, 45, 540, 34);
            region = Label("", detail, 17, Gold, TextAnchor.MiddleLeft);
            Center(region.rectTransform, -140, 12, 540, 28);
            description = Label("", detail, 17, Muted, TextAnchor.UpperLeft);
            Center(description.rectTransform, -115, -35, 590, 52);
            availability = Label("", detail, 17, Gold);
            Center(availability.rectTransform, 325, 41, 170, 26);
            enter = MakeButton("입장", detail, new Color32(130, 88, 49, 255));
            Center(enter.GetComponent<RectTransform>(), 325, -8, 170, 46);
            enterLabel = enter.GetComponentInChildren<Text>();
            enter.interactable = false;
            enter.onClick.AddListener(Enter);
            StartCoroutine(RevealCards());
        }

        void MakeCard(RectTransform parent, StoryEntry story, float x)
        {
            var border = Picture(story.id, parent, Brass);
            border.rectTransform.anchorMin = border.rectTransform.anchorMax = new Vector2(0, .5f);
            border.rectTransform.anchoredPosition = new Vector2(x, 0);
            border.rectTransform.sizeDelta = new Vector2(192, 212);
            borders.Add(border);
            var group = border.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            reveals.Add(group);

            var book = Picture("Book cover", border.transform,
                story.locked ? new Color32(31, 24, 43, 255) : new Color32(52, 37, 73, 255));
            Fill(book.rectTransform, 3);
            book.raycastTarget = true;
            var button = book.gameObject.AddComponent<Button>();
            button.targetGraphic = book;
            int index = borders.Count - 1;
            button.onClick.AddListener(() => Select(story, index));

            Center(Picture("Spine", book.transform, story.locked ? new Color32(70, 52, 70, 255) : Brass).rectTransform,
                -81, 0, 4, 202);
            var inset = Center(Picture("Cover inlay", book.transform, new Color32(24, 20, 42, 255)).rectTransform,
                5, 31, 150, 115);
            if (story.thumbnail != null)
            {
                var art = Picture("Thumbnail", inset, story.locked ? new Color(.46f, .43f, .52f) : Color.white);
                art.sprite = story.thumbnail;
                art.preserveAspect = true;
                Fill(art.rectTransform, 5);
            }
            else
            {
                var seal = Center(Picture("Book seal", inset, new Color32(90, 65, 91, 255)).rectTransform,
                    0, 0, 33, 33);
                seal.localRotation = Quaternion.Euler(0, 0, 45);
                Center(Picture("Seal inset", seal, Ink).rectTransform, 0, 0, 22, 22);
            }

            Center(Label(story.title, book.transform, 21, story.locked ? Muted : Paper).rectTransform,
                5, -45, 165, 34);
            Center(Label(story.locked ? "잠김" : story.region, book.transform, 15, story.locked ? Muted : Gold).rectTransform,
                5, -76, 160, 25);
        }

        IEnumerator RevealCards()
        {
            const float duration = .19f;
            const float stagger = .09f;
            float end = duration + stagger * (reveals.Count - 1);
            for (float elapsed = 0; elapsed < end; elapsed += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < reveals.Count; i++)
                {
                    float t = Mathf.Clamp01((elapsed - i * stagger) / duration);
                    reveals[i].alpha = t;
                    reveals[i].transform.localScale = Vector3.one * Mathf.Lerp(.95f, 1f, t);
                    reveals[i].blocksRaycasts = t >= .8f;
                }
                yield return null;
            }
            foreach (var card in reveals)
            {
                card.alpha = 1;
                card.transform.localScale = Vector3.one;
                card.blocksRaycasts = true;
            }
        }

        void Select(StoryEntry story, int index)
        {
            selected = story;
            for (int i = 0; i < borders.Count; i++) borders[i].color = i == index ? Gold : Brass;
            heading.text = story.title;
            region.text = story.region;
            description.text = story.description;
            bool canEnter = CanEnter(story);
            availability.text = story.locked ? "잠김" : canEnter ? "입장 가능" : "입장 불가";
            enter.interactable = canEnter;
        }

        static bool CanEnter(StoryEntry story) =>
            story != null && !story.locked && !string.IsNullOrWhiteSpace(story.sceneName)
            && Application.CanStreamedLevelBeLoaded(story.sceneName);

        void Enter()
        {
            if (departing || !CanEnter(selected)) return;
            departing = travel(selected);
            if (departing)
            {
                canvas.enabled = false;
                GetComponent<GraphicRaycaster>().enabled = false;
            }
            else availability.text = "입장 불가";
        }

        void Update()
        {
            if (departing)
            {
                if (StorybookTransition.IsBusy) return;
                departing = false;
                canvas.enabled = true;
                GetComponent<GraphicRaycaster>().enabled = true;
                availability.text = "입장 불가";
            }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }

        void Close()
        {
            if (departing) return;
            gameObject.SetActive(false);
            closed?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (ownsFont && font != null) Destroy(font);
        }

        Text Label(string value, Transform parent, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var text = Rect("Label", parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.text = value;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        Button MakeButton(string title, Transform parent, Color color)
        {
            var image = Picture(title, parent, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Fill(Label(title, image.transform, 19, Paper).rectTransform, 2);
            return button;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return (RectTransform)obj.transform;
        }

        static Image Picture(string name, Transform parent, Color color)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform Center(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        static void Fill(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * padding;
            rect.offsetMax = Vector2.one * -padding;
        }
    }
}
