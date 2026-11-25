#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    internal sealed partial class PalaceDeveloperMenu
    {
        readonly List<RelicSummonRow> relicRows = new();
        RectTransform mainPanel, relicPanel, relicContent;
        ScrollRect relicScroll;
        Text relicCount, relicStatus;

        void OpenRelicSummon()
        {
            if (relicPanel == null) BuildRelicSummon();
            mainPanel.gameObject.SetActive(false);
            relicPanel.gameObject.SetActive(true);
            PopulateRelicRows();
        }

        void CloseRelicSummon()
        {
            if (relicPanel == null || !relicPanel.gameObject.activeSelf) return;
            relicPanel.gameObject.SetActive(false);
            mainPanel.gameObject.SetActive(true);
            Refresh();
        }

        void PopulateRelicRows()
        {
            foreach (var row in relicRows)
            {
                row.gameObject.SetActive(false);
                Destroy(row.gameObject);
            }
            relicRows.Clear();

            var prefab = dungeon.RelicSummonRowPrefab;
            var catalog = dungeon.TestRelicCatalog.ToArray();
            relicCount.text = $"등록된 유물 {catalog.Length}종";
            relicStatus.text = prefab == null ? "유물 소환 목록 프리팹을 확인하세요." :
                catalog.Length == 0 ? "등록된 유물이 없습니다." : string.Empty;
            float height = 0f;
            if (prefab != null)
            {
                foreach (var relic in catalog)
                {
                    var row = Instantiate(prefab, relicContent, false);
                    var rect = (RectTransform)row.transform;
                    rect.anchoredPosition = new Vector2(0f, -height);
                    row.Bind(relic, font, ButtonColors,
                        () => RelicTest(() => dungeon.TestRelic(relic.Id, true)),
                        () => RelicTest(() => dungeon.TestRelic(relic.Id, false)));
                    relicRows.Add(row);
                    height += rect.rect.height + 8f;
                }
            }
            relicContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height - 8f));
            RefreshRelicRows();
            Canvas.ForceUpdateCanvases();
            relicScroll.StopMovement();
            relicScroll.verticalNormalizedPosition = 1f;
        }

        void RefreshRelicRows()
        {
            foreach (var row in relicRows)
                row.Refresh(health.Relics.Contains(row.Relic.Id), dungeon.RelicAcquireBlockReason(row.Relic));
        }

        void RelicTest(Func<string> test)
        {
            try
            {
                relicStatus.text = test();
                RefreshRelicRows();
            }
            catch (Exception error)
            {
                relicStatus.text = "테스트 실행 실패 · Console에서 원인을 확인하세요.";
                Debug.LogException(error, this);
            }
        }

        void BuildRelicSummon()
        {
            relicPanel = Rectangle(mainPanel.parent, "Relic summon panel", 0, 0, 760, 683);
            relicPanel.anchorMin = relicPanel.anchorMax = relicPanel.pivot = new Vector2(.5f, .5f);
            relicPanel.anchoredPosition = Vector2.zero;
            Paint(relicPanel, new Color(.075f, .105f, .15f));
            Paint(Rectangle(relicPanel, "Top accent", 0, 0, 760, 3), Accent);
            Label(relicPanel, "유물 소환", 24, 15, 400, 34, 25, Ink);
            relicCount = Label(relicPanel, "", 24, 54, 690, 26, 16, Accent);
            AddButton(relicPanel, "닫기", 630, 16, 106, CloseRelicSummon);

            var scrollArea = Rectangle(relicPanel, "Relic list", 24, 89, 712, 428);
            var viewport = Rectangle(scrollArea, "Viewport", 0, 0, 694, 428);
            Paint(viewport, new Color(.045f, .068f, .103f));
            viewport.gameObject.AddComponent<RectMask2D>();
            relicContent = Rectangle(viewport, "Relic rows", 0, 0, 694, 0);
            var track = Rectangle(scrollArea, "Scrollbar", 701, 0, 11, 428);
            Paint(track, new Color(.045f, .068f, .103f));
            var handle = Rectangle(track, "Handle", 0, 0, 0, 0);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.one;
            var handleImage = Paint(handle, new Color(.37f, .52f, .63f));
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            relicScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
            relicScroll.viewport = viewport;
            relicScroll.content = relicContent;
            relicScroll.horizontal = false;
            relicScroll.vertical = true;
            relicScroll.movementType = ScrollRect.MovementType.Clamped;
            relicScroll.scrollSensitivity = 40f;
            relicScroll.verticalScrollbar = scrollbar;
            relicScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            AddButton(relicPanel, "무작위 유물 지급", 24, 528, 162, () =>
                RelicTest(() => dungeon.TestChestReward(PalaceChestReward.Relic)));
            AddButton(relicPanel, "보유 유물 확인", 194, 528, 162, () => RelicTest(() =>
                $"보유 유물 {health.Relics.Owned.Count}/{FairyTaleRelicInventory.Capacity} · " +
                (health.Relics.Owned.Count == 0 ? "없음" : string.Join(", ", health.Relics.Owned.Select(relic => relic.DisplayName)))));
            AddButton(relicPanel, "유물 한도 검증", 394, 528, 162, () => RelicTest(dungeon.TestRelicLimit));
            AddButton(relicPanel, "테스트 유물 초기화", 564, 528, 162, () => RelicTest(() =>
            {
                health.Relics.ClearForTesting();
                return "보유 유물과 효과를 초기화했습니다.";
            }));
            AddButton(relicPanel, "공격력 보정 확인", 24, 568, 162, () => RelicTest(() =>
                $"유물 공격력 +{player.RelicAttackBonus:P0} · 공격력 {player.ApplyAttackPower(player.BaseAttackPower):0.##} " +
                $"(+{player.ApplyAttackPower(player.BaseAttackPower) - player.BaseAttackPower:0.##})"));
            AddButton(relicPanel, "중독 강제 적용", 194, 568, 162, () => RelicTest(dungeon.TestBloodTemptationPoison));
            relicStatus = Label(relicPanel, "", 24, 608, 712, 58, 15, Accent);
        }
    }
}
#endif
