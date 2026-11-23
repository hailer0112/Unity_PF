using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Storybook
{
    public sealed class CharacterSelectScreen : MonoBehaviour
    {
        public const string SceneName = "CharacterSelectScene";
        public static string SelectedCharacterId { get; private set; }

        [SerializeField] Sprite referenceArtwork;
        [SerializeField] Sprite hongGildongIdle;
        [SerializeField] Font interfaceFont;

        sealed class Character
        {
            internal string Id, Name, Description;
            internal Sprite[] SkillIcons;
            internal bool IsPlayable;
        }

        static readonly string[] IconNames = { "AzureSlash", "CloneSlash", "Windstep", "SwordTyphoon" };
        static readonly string[] SkillNames = { "관통 베기", "분신술", "풍신보", "풍검태풍" };
        static readonly string[] Descriptions =
        {
            "관통하는 검기를 발사하여 적을 공격합니다.",
            "분신을 소환하여 함께 적을 공격합니다.",
            "일정 시간 이동속도를 강화합니다.",
            "일정 범위에 지속 피해를 주는 바람의 소용돌이를 생성합니다."
        };
        static readonly Color Gold = new Color32(247, 193, 110, 255);
        static readonly Color Paper = new Color32(255, 237, 202, 255);
        static readonly Color Muted = new Color32(187, 173, 205, 255);
        const float LayoutScale = 1920f / 1672f;

        Character character;
        Text detail;
        Image[] sparks;
        Button back, confirm;
        Font font;
        Sprite frame, buttonFrame, glow;
        bool leaving;

        void Awake()
        {
            SceneManager.sceneLoaded += FocusFirstButton;
            font = interfaceFont != null ? interfaceFont : Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 32);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var hud = Resources.LoadAll<Sprite>("CombatHud");
            character = new Character
            {
                Id = "hong-gildong",
                Name = "홍길동",
                Description = "검술과 바람의 힘을 활용해 빠르게 전장을 누비는 근접 전투형 캐릭터.",
                SkillIcons = IconNames.Select(name => Array.Find(hud, sprite => sprite.name == name)).ToArray(),
                IsPlayable = true
            };
            frame = MakeFrame(new Color32(30, 23, 48, 255));
            buttonFrame = MakeFrame(new Color32(59, 27, 49, 255));
            glow = MakeGlow();
            Build();
        }

        void FocusFirstButton(Scene scene, LoadSceneMode mode)
        {
            if (scene == gameObject.scene && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(confirm.gameObject);
        }

        void Update()
        {
            float time = Time.unscaledTime;
            for (int i = 0; i < sparks.Length; i++)
                sparks[i].color = new Color(1f, .71f, .33f, .18f + .18f * Mathf.Sin(time * 1.6f + i * 2.1f));
        }

        void Build()
        {
            var canvasRoot = new GameObject("Character Select Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var backdrop = canvasRoot.AddComponent<Image>();
            backdrop.color = new Color32(10, 9, 21, 255);
            backdrop.raycastTarget = false;
            var layout = new GameObject("16:9 composition", typeof(RectTransform), typeof(AspectRatioFitter));
            var fit = layout.GetComponent<AspectRatioFitter>();
            layout.transform.SetParent(canvasRoot.transform, false);
            fit.aspectRatio = 16f / 9f;
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            var root = layout.transform;
            Picture(root, "Archive artwork", 0, 0, 1672, 940.5f, referenceArtwork, Color.white, true);

            Panel(root, "Title plaque", 516, 46, 642, 135, buttonFrame);
            Label(root, "캐릭터 선택", 540, 67, 594, 83, 55, Gold, FontStyle.Bold);
            Panel(root, "Character slots", 101, 188, 174, 576, frame);
            Panel(root, "Selected Hong Gildong", 112, 212, 152, 124, buttonFrame);
            Picture(root, "Hong Gildong slot preview", 134, 218, 108, 112,
                hongGildongIdle, Color.white, true);
            for (int i = 0; i < 3; i++)
            {
                float y = 350 + i * 132;
                Panel(root, "Locked slot " + (i + 1), 112, y, 152, 121, frame);
                Picture(root, "Locked silhouette", 145, y + 7, 86, 105, hongGildongIdle,
                    new Color32(15, 12, 24, 245), true);
                Picture(root, "Lock shackle", 180, y + 49, 16, 15, frame, Gold);
                Picture(root, "Lock body", 176, y + 58, 24, 19, buttonFrame, Gold);
            }

            Panel(root, "Character stage", 474, 167, 679, 457, frame);
            Picture(root, "Stage light", 516, 190, 586, 380, glow, new Color(.29f, .19f, .61f, .67f));
            Picture(root, "Gold stage light", 534, 495, 541, 88, glow, new Color(1f, .57f, .22f, .7f));
            Picture(root, "Hong Gildong Idle", 640, 174, 315, 414,
                hongGildongIdle, Color.white, true);
            sparks = new Image[6];
            Vector2[] stars = {new(614,262),new(674,232),new(958,221),new(992,310),new(618,451),new(968,468)};
            for (int i=0; i<stars.Length; i++)
                sparks[i] = Picture(root, "Stage sparkle", stars[i].x, stars[i].y, 10, 13, glow, Gold);

            Panel(root, "Character information", 1063, 208, 362, 323, frame);
            Panel(root, "Name plaque", 1076, 216, 337, 90, buttonFrame);
            Label(root, character.Name, 1088, 219, 313, 80, 52, Gold, FontStyle.Bold);
            Label(root, character.Description, 1098, 323, 288, 156, 26, Paper);

            Panel(root, "Skill section", 280, 585, 1125, 209, frame);
            Panel(root, "Skill title plaque", 709, 586, 256, 51, buttonFrame);
            Label(root, "스킬 정보", 721, 591, 233, 39, 25, Gold, FontStyle.Bold);
            detail = Label(root, "", 318, 746, 1049, 38, 21, Paper);
            ShowSkill(0);
            for (int i = 0; i < SkillNames.Length; i++)
            {
                int skill = i;
                float x = 306 + i * 268;
                var skillButton = MakeButton(root, SkillNames[i], x, 645, 254, 96, () => ShowSkill(skill));
                Picture(skillButton.transform, IconNames[i], 8, 6, 83, 83, character.SkillIcons[i], Color.white, true);
                Label(skillButton.transform, "QWER"[i].ToString(), 104, 5, 137, 33, 23, Gold, FontStyle.Bold);
                Label(skillButton.transform, SkillNames[i], 98, 36, 148, 50, 24, Paper, FontStyle.Bold);
                var hover = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hover.callback.AddListener(_ => ShowSkill(skill));
                skillButton.gameObject.AddComponent<EventTrigger>().triggers.Add(hover);
            }

            Panel(root, "Navigation plate", 443, 795, 793, 108, frame);
            back = MakeButton(root, "뒤로 가기", 458, 805, 344, 85, () => Navigate("TitleScene", false));
            Label(back.transform, "‹   뒤로 가기", 0, 0, 344, 85, 35, Paper, FontStyle.Bold);
            confirm = MakeButton(root, "선택 완료", 846, 805, 371, 85, () => Navigate(StorybookTransition.ArchiveScene, true));
            Label(confirm.transform, "선택 완료   ›", 0, 0, 371, 85, 35, Gold, FontStyle.Bold);

            if (EventSystem.current == null)
            {
                var events = new GameObject("Character Select Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
                events.GetComponent<EventSystem>().firstSelectedGameObject = confirm.gameObject;
            }
        }

        void ShowSkill(int index) => detail.text = Descriptions[index];

        void Navigate(string scene, bool select)
        {
            if (leaving || (select && !character.IsPlayable)) return;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"Character selection scene is missing from build settings: {scene}", this);
                return;
            }

            leaving = true;
            if (select) SelectedCharacterId = character.Id;
            back.interactable = confirm.interactable = false;
            StartCoroutine(Travel(scene));
        }

        IEnumerator Travel(string scene)
        {
            yield return new WaitForSecondsRealtime(.09f);
            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
        }

        Button MakeButton(Transform parent, string id, float x, float y, float width, float height, Action click)
        {
            var image = Panel(parent, id, x, y, width, height, buttonFrame);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color32(255, 231, 179, 255);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color32(240, 171, 92, 255);
            colors.fadeDuration = .08f;
            button.colors = colors;
            button.onClick.AddListener(() => click());
            return button;
        }

        static RectTransform Area(Transform parent, string id, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(id, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y) * LayoutScale;
            rect.sizeDelta = new Vector2(width, height) * LayoutScale;
            return rect;
        }

        static Image Picture(Transform parent, string id, float x, float y, float width, float height,
            Sprite sprite, Color tint, bool preserveAspect = false)
        {
            var image = Area(parent, id, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        static Image Panel(Transform parent, string id, float x, float y, float width, float height, Sprite sprite)
        {
            var image = Picture(parent, id, x, y, width, height, sprite, Color.white);
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = .6f;
            return image;
        }

        Text Label(Transform parent, string content, float x, float y, float width, float height,
            int size, Color tint, FontStyle style = FontStyle.Normal)
        {
            var label = Area(parent, content, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = Mathf.RoundToInt(size * LayoutScale);
            label.fontStyle = style;
            label.text = content;
            label.color = tint;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            return label;
        }

        static Sprite MakeFrame(Color32 interior)
        {
            const int size = 36;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int a = Mathf.Min(x, size - 1 - x), b = Mathf.Min(y, size - 1 - y);
                int edge = Mathf.Min(a, b, (a + b - 7) / 2);
                if (a + b < 7 || edge == 0) continue;
                pixels[y * size + x] = edge switch
                {
                    1 => new Color32(101, 52, 44, 255),
                    2 => new Color32(223, 150, 78, 255),
                    3 => new Color32(255, 214, 138, 255),
                    4 => new Color32(145, 79, 46, 255),
                    5 => new Color32(79, 48, 56, 255),
                    _ => interior
                };
            }
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100f, 0,
                SpriteMeshType.FullRect, new Vector4(13, 13, 13, 13));
        }

        static Sprite MakeGlow()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = new Vector2(x - 15.5f, y - 15.5f).magnitude / 16f;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - distance), 2)));
            }
            texture.filterMode = FilterMode.Point;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= FocusFirstButton;
            if (interfaceFont == null && font != null && font.name != "LegacyRuntime") Destroy(font);
            foreach (var sprite in new[] { frame, buttonFrame, glow })
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
    }
}
