#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Storybook
{
    [DefaultExecutionOrder(-500)]
    internal sealed class PalaceDeveloperMenu : MonoBehaviour
    {
        static readonly Color Ink = new(.91f, .95f, .98f);
        static readonly Color Accent = new(.46f, .84f, .95f);
        static readonly Color Muted = new(.62f, .72f, .79f);
        static PalaceDeveloperMenu openMenu;
        static int closedFrame = -1;
        internal static bool BlocksGameplay => openMenu != null || closedFrame == Time.frameCount;

        readonly List<Button> summonButtons = new();
        PalaceDungeon dungeon;
        HongGildong player;
        PlayerHealth health;
        GameObject menu, eventSystem;
        Text readout, status, invulnerabilityLabel;
        Font font;
        float previousTimeScale;
        bool toggleHeld;

        internal void Initialize(PalaceDungeon owner, HongGildong controls, PlayerHealth playerHealth)
        {
            dungeon = owner;
            player = controls;
            health = playerHealth;
        }

        void Update()
        {
            bool held = Keyboard.current?.f1Key.isPressed == true;
            bool toggle = held && !toggleHeld;
            toggleHeld = held;
            if (!toggle) return;
            if (openMenu == this) Close();
            else if (dungeon?.Current != null && player != null && player.isActiveAndEnabled &&
                health != null && !health.IsDead && !StorybookTransition.IsBusy)
                Open();
        }

        void Open()
        {
            if (menu == null) Build();
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            openMenu = this;
            menu.SetActive(true);
            if (EventSystem.current == null)
            {
                if (eventSystem == null)
                {
                    eventSystem = new GameObject("Developer UI input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    eventSystem.transform.SetParent(transform, false);
                }
                eventSystem.SetActive(true);
            }
            status.text = "";
            Refresh();
        }

        void Close()
        {
            if (openMenu != this) return;
            openMenu = null;
            closedFrame = Time.frameCount;
            Time.timeScale = previousTimeScale;
            menu.SetActive(false);
            if (eventSystem != null) eventSystem.SetActive(false);
        }

        void OnDisable() => Close();

        void OnDestroy()
        {
            if (font != null && font.name != "LegacyRuntime") Destroy(font);
        }

        void Refresh()
        {
            readout.text = $"구역 {dungeon.Floor}    HP {health.CurrentHP:0.#}/{health.MaxHP:0.#}    " +
                $"MP {player.CurrentMP:0.#}/{player.MaxMP:0.#}    이야기 파편 {health.StoryFragments:N0}";
            invulnerabilityLabel.text = health.TestInvulnerable ? "무적 모드 끄기" : "무적 모드 켜기";
            foreach (Button button in summonButtons) button.interactable = dungeon.CanSpawnTestMonster;
        }

        void Run(Action action, string success)
        {
            try
            {
                action();
                status.text = success;
                Refresh();
            }
            catch (Exception error) { ReportError(error); }
        }

        void Travel(Func<bool> travel)
        {
            try
            {
                if (!travel()) throw new InvalidOperationException("The player cannot start a dungeon transition.");
                Close();
            }
            catch (Exception error) { ReportError(error); }
        }

        void Summon(TestMonsterKind kind)
        {
            try
            {
                status.text = dungeon.SpawnTestMonster(kind) ? "소환 완료 · F1로 돌아가 전투를 시작하세요." : "같은 몬스터가 이미 살아 있습니다.";
            }
            catch (Exception error) { ReportError(error); }
        }

        void ReportError(Exception error)
        {
            status.text = "테스트 실행 실패 · Console에서 원인을 확인하세요.";
            Debug.LogException(error, this);
        }

        void Build()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 18);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            menu = new GameObject("Palace developer test menu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            menu.SetActive(false);
            menu.transform.SetParent(transform, false);
            var canvas = menu.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = menu.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(920, 540);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var shade = Rectangle(menu.transform, "Dimmer", 0, 0, 0, 0);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            Paint(shade, new Color(.015f, .025f, .045f, .76f));
            var panel = Rectangle(shade, "Developer panel", 0, 0, 760, 482);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            Paint(panel, new Color(.075f, .105f, .15f));
            Paint(Rectangle(panel, "Top accent", 0, 0, 760, 3), Accent);

            Label(panel, "개발자 테스트", 23, 15, 350, 34, 25, Ink);
            AddButton(panel, "닫기  F1", 630, 16, 106, Close);
            readout = Label(panel, "", 24, 60, 700, 28, 16, Ink);

            Label(panel, "방 이동", 24, 96, 340, 23, 17, Accent);
            AddButton(panel, "일반 전투방", 24, 124, 162, () => Travel(() => dungeon.TestRoom(PalaceRoomType.Combat)));
            AddButton(panel, "회복방", 194, 124, 162, () => Travel(() => dungeon.TestRoom(PalaceRoomType.Recovery)));
            AddButton(panel, "용왕 보스방", 24, 163, 162, () => Travel(() => dungeon.TestRoom(PalaceRoomType.Boss)));
            AddButton(panel, "상인방 · 미구현", 194, 163, 162);
            AddButton(panel, "보물방 · 미구현", 24, 202, 162);
            AddButton(panel, "시련방 · 미구현", 194, 202, 162);

            Label(panel, "구역 생성", 24, 255, 340, 23, 17, Accent);
            for (int floor = 1; floor <= 4; floor++)
            {
                int target = floor;
                AddButton(panel, floor == 4 ? "용왕 보스 구역" : $"일반 {floor}구역",
                    24 + ((floor - 1) % 2) * 170, 284 + ((floor - 1) / 2) * 39, 162,
                    () => Travel(() => dungeon.TestFloor(target)));
            }
            Label(panel, "구역을 생성하면 기존 방과 웨이브가 초기화됩니다.", 24, 369, 347, 25, 13, Muted);

            Label(panel, "플레이어", 394, 96, 340, 23, 17, Accent);
            AddButton(panel, "HP 전량 회복", 394, 124, 162, () => Run(() => health.TryHeal(health.MaxHP), "HP를 최대치로 채웠습니다."));
            AddButton(panel, "MP 전량 회복", 564, 124, 162, () => Run(player.RefillMana, "MP를 최대치로 채웠습니다."));
            AddButton(panel, "HP를 1로 설정", 394, 163, 162, () => Run(health.SetOneHPForTesting, "HP를 1로 설정했습니다."));
            invulnerabilityLabel = AddButton(panel, "무적 모드", 564, 163, 162,
                () => Run(() => health.TestInvulnerable = !health.TestInvulnerable, "무적 모드 설정을 변경했습니다."))
                .GetComponentInChildren<Text>();
            AddButton(panel, "이야기 파편 +100", 394, 202, 162, () => Run(() => health.AwardStoryFragments(100, 100), "이야기 파편 100을 지급했습니다."));
            AddButton(panel, "이야기 파편 +500", 564, 202, 162, () => Run(() => health.AwardStoryFragments(500, 500), "이야기 파편 500을 지급했습니다."));
            AddButton(panel, "이야기 파편 초기화", 394, 241, 162, () => Run(health.ClearFragmentsForTesting, "이야기 파편을 0으로 초기화했습니다."));
            AddButton(panel, "쿨타임 초기화", 564, 241, 162, () => Run(player.ResetSkillCooldownsForTesting, "스킬 및 대시 쿨타임을 초기화했습니다."));

            Label(panel, "몬스터 소환", 394, 294, 340, 23, 17, Accent);
            AddSummon(panel, "게 병사", 394, 322, TestMonsterKind.Crab);
            AddSummon(panel, "물고기 궁수", 564, 322, TestMonsterKind.Archer);
            AddSummon(panel, "거북 방패병", 394, 361, TestMonsterKind.Turtle);
            AddSummon(panel, "복어 자폭병", 564, 361, TestMonsterKind.Pufferfish);
            Label(panel, "일반 시작방·전투방에서 한 마리씩 소환합니다.", 394, 403, 341, 22, 13, Muted);

            status = Label(panel, "", 24, 438, 704, 28, 15, Accent);
        }

        void AddSummon(Transform panel, string title, float x, float y, TestMonsterKind kind) =>
            summonButtons.Add(AddButton(panel, title, x, y, 162, () => Summon(kind)));

        Button AddButton(Transform parent, string caption, float x, float y, float width, Action action = null)
        {
            var rect = Rectangle(parent, caption, x, y, width, 32);
            var image = Paint(rect, Color.white);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = new ColorBlock
            {
                normalColor = new Color(.16f, .23f, .31f),
                highlightedColor = new Color(.22f, .37f, .47f),
                pressedColor = new Color(.10f, .19f, .27f),
                selectedColor = new Color(.21f, .32f, .42f),
                disabledColor = new Color(.11f, .15f, .20f),
                colorMultiplier = 1f,
                fadeDuration = .08f
            };
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.interactable = action != null;
            if (action != null) button.onClick.AddListener(() => action());
            var label = Label(rect, caption, 0, 0, width, 32, 15, action == null ? Muted : Ink);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        Text Label(Transform parent, string caption, float x, float y, float width, float height, int size, Color color)
        {
            var rect = Rectangle(parent, "Label", x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.raycastTarget = false;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = caption;
            return text;
        }

        static Image Paint(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static RectTransform Rectangle(Transform parent, string title, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(title, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
#endif
