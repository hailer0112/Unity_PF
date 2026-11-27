using System;
using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    internal enum HudSkill { AzureSlash, CloneSlash, Windstep, SwordTyphoon, Dash }

    [DisallowMultipleComponent]
    public sealed class HongGildongHud : MonoBehaviour
    {
        static readonly string[] SpriteNames = { "AzureSlash", "CloneSlash", "Windstep", "SwordTyphoon", "Dash" };
        static readonly string[] Keys = { "Q", "W", "E", "R", "SPACE" };
        static readonly Color LabelColor = new Color(.90f, .96f, 1f);
        const float IconSize = 68f;
        const float SlotSpacing = 77f;
        const float BarHeight = 78f;
        const float BarGap = 6f;
        static readonly Color ManaWarningColor = new Color(1f, .62f, .49f);

        const float CooldownFlashDuration = .30f;
        readonly CanvasGroup[] cooldownFlashes = new CanvasGroup[(int)HudSkill.Dash];
        readonly double[] cooldownFlashEnds = new double[(int)HudSkill.Dash];
        readonly Image[] cooldownMasks = new Image[Keys.Length];
        readonly Text[] cooldownLabels = new Text[Keys.Length];
        readonly Text[] keyLabels = new Text[Keys.Length];
        HongGildong player;
        PlayerHealth health;
        Canvas canvas;
        RectTransform healthFill, manaFill, skillRow;
        internal float SkillRowTop => skillRow == null ? 0f :
            RectTransformUtility.WorldToScreenPoint(null, skillRow.TransformPoint(skillRow.rect.max)).y;
        Text healthLabel, manaLabel, windstepActive, deathDeceptionNotice;
        double deathDeceptionNoticeUntil, seedGrowthNoticeUntil;
        Text seedGrowthNotice;
        Font font, noticeFont;

        void Start()
        {
            player = GetComponent<HongGildong>();
            health = GetComponent<PlayerHealth>();
            var sprites = Resources.LoadAll<Sprite>("CombatHud");
            var icons = Array.ConvertAll(SpriteNames, name => Array.Find(sprites, sprite => sprite.name == name));
            var healthFrame = Array.Find(sprites, sprite => sprite.name == "HealthFrame");
            if (healthFrame == null || Array.Exists(icons, icon => icon == null))
            {
                Debug.LogError("CombatHud sprite atlas is missing one or more sprites.", this);
                enabled = false;
                return;
            }

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            noticeFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR" }, 12);
            var root = new GameObject("Hong Gildong HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(null);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = .5f;

            (healthFill, healthLabel) = BuildResourceBar("HP", healthFrame, -18f,
                new Color(.73f, .17f, .24f), new Color(1f, .41f, .44f, .57f));
            (manaFill, manaLabel) = BuildResourceBar("MP", healthFrame, -18f - BarHeight - BarGap,
                new Color(.065f, .37f, .75f), new Color(.30f, .80f, 1f, .68f),
                Resources.Load<Sprite>("ManaEmblem"));
            deathDeceptionNotice = MakeLabel("Death deception notice", canvas.transform, 20, new Color(1f, .53f, .61f));
            if (noticeFont != null) deathDeceptionNotice.font = noticeFont;
            Pin(deathDeceptionNotice.rectTransform, Vector2.up, Vector2.up,
                new Vector2(20f, -18f - (BarHeight + BarGap) * 2f), new Vector2(232f, 32f));
            deathDeceptionNotice.enabled = false;
            seedGrowthNotice = MakeLabel("Seed growth notice", canvas.transform, 18, new Color(.83f, 1f, .48f));
            if (noticeFont != null) seedGrowthNotice.font = noticeFont;
            Pin(seedGrowthNotice.rectTransform, Vector2.up, Vector2.up,
                new Vector2(20f, -50f - (BarHeight + BarGap) * 2f), new Vector2(265f, 34f));
            seedGrowthNotice.enabled = false;
            BuildSkills(icons);
            LateUpdate();
        }

        (RectTransform fill, Text label) BuildResourceBar(string resource, Sprite frame, float y,
            Color fillColor, Color highlightColor, Sprite emblem = null)
        {
            var panel = MakeImage(resource + " frame", canvas.transform, frame, Color.white);
            Pin(panel.rectTransform, Vector2.up, Vector2.up, new Vector2(20f, y), new Vector2(232f, BarHeight));
            if (emblem != null)
                Stretch(MakeImage("Mana emblem", panel.transform, emblem, Color.white).rectTransform);

            var track = MakeRect(resource + " fill track", panel.transform);
            track.anchorMin = new Vector2(.264f, .27f);
            track.anchorMax = new Vector2(.895f, .604f);
            track.offsetMin = track.offsetMax = Vector2.zero;

            var fill = MakeImage(resource + " fill", track, null, fillColor).rectTransform;
            Stretch(fill);
            var gleam = MakeImage(resource + " upper highlight", fill, null, highlightColor);
            gleam.rectTransform.anchorMin = new Vector2(0f, .73f);
            gleam.rectTransform.anchorMax = Vector2.one;
            gleam.rectTransform.offsetMin = gleam.rectTransform.offsetMax = Vector2.zero;
            var label = MakeLabel($"Current {resource} / Max {resource}", track, 13, Color.white);
            Stretch(label.rectTransform);
            return (fill, label);
        }

        void BuildSkills(Sprite[] icons)
        {
            skillRow = MakeRect("Skill row", canvas.transform);
            Pin(skillRow, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 16f),
                new Vector2(SlotSpacing * Keys.Length, 88f));

            for (int i = 0; i < icons.Length; i++)
            {
                var tile = MakeRect(SpriteNames[i] + " slot", skillRow);
                Pin(tile, Vector2.zero, Vector2.zero, new Vector2(SlotSpacing * i + (SlotSpacing - IconSize) * .5f, 0f),
                    new Vector2(IconSize, 88f));

                var art = MakeImage("Pixel art", tile, icons[i], Color.white);
                Pin(art.rectTransform, Vector2.up, Vector2.up, Vector2.zero, new Vector2(IconSize, IconSize));
                art.preserveAspect = true;

                var shade = MakeImage("Cooldown mask", art.transform, icons[i], new Color(.17f, .24f, .35f, .87f));
                Stretch(shade.rectTransform);
                shade.preserveAspect = true;
                shade.type = Image.Type.Filled;
                shade.fillMethod = Image.FillMethod.Vertical;
                shade.fillOrigin = (int)Image.OriginVertical.Top;
                cooldownMasks[i] = shade;

                var remaining = MakeLabel("Cooldown seconds", art.transform, 23, Color.white);
                Stretch(remaining.rectTransform);
                cooldownLabels[i] = remaining;

                var key = MakeLabel("Keyboard shortcut", tile, i == (int)HudSkill.Dash ? 12 : 15, LabelColor);
                Pin(key.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(IconSize, 19f));
                key.text = Keys[i];
                keyLabels[i] = key;

                if (i < cooldownFlashes.Length) cooldownFlashes[i] = BuildCooldownFlash(art.transform);

                if (i != (int)HudSkill.Windstep) continue;
                windstepActive = MakeLabel("Windstep duration", art.transform, 11, new Color(.48f, 1f, .87f));
                Pin(windstepActive.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                    new Vector2(0f, 5f), new Vector2(IconSize, 16f));
            }
        }

        static CanvasGroup BuildCooldownFlash(Transform parent)
        {
            var border = MakeRect("Cooldown reduction border", parent);
            Stretch(border);
            var group = border.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var color = new Color(.78f, 1f, .38f);

            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;
                float edge = i % 2;
                var line = MakeImage("Edge", border, null, color).rectTransform;
                line.anchorMin = vertical ? new Vector2(edge, 0f) : new Vector2(0f, edge);
                line.anchorMax = vertical ? new Vector2(edge, 1f) : new Vector2(1f, edge);
                line.pivot = vertical ? new Vector2(edge, .5f) : new Vector2(.5f, edge);
                line.anchoredPosition = Vector2.zero;
                line.sizeDelta = vertical ? new Vector2(2f, 0f) : new Vector2(0f, 2f);
            }

            return group;
        }

        internal void FlashReducedCooldown(HudSkill skill) =>
            cooldownFlashEnds[(int)skill] = Time.unscaledTimeAsDouble + CooldownFlashDuration;

        internal void ShowDeathDeception(string message)
        {
            if (deathDeceptionNotice == null) return;
            deathDeceptionNotice.text = message;
            deathDeceptionNoticeUntil = Time.timeAsDouble + 1.15d;
            deathDeceptionNotice.enabled = true;
        }

        internal void ShowSeedGrowth(string message, float duration)
        {
            if (seedGrowthNotice == null) return;
            seedGrowthNotice.text = message;
            seedGrowthNoticeUntil = Time.timeAsDouble + duration;
            seedGrowthNotice.enabled = duration > 0f;
        }

        void LateUpdate()
        {
            if (canvas == null) return;

            deathDeceptionNotice.enabled = !health.IsDead && Time.timeAsDouble < deathDeceptionNoticeUntil;
            seedGrowthNotice.enabled = !health.IsDead && Time.timeAsDouble < seedGrowthNoticeUntil;
            SetResourceBar(healthFill, healthLabel, health.CurrentHP, health.MaxHP, true);
            SetResourceBar(manaFill, manaLabel, player.CurrentMP, player.MaxMP, false);

            for (int i = 0; i < cooldownMasks.Length; i++)
            {
                var (remaining, duration) = player.GetHudCooldown((HudSkill)i);
                cooldownMasks[i].enabled = remaining > 0f;
                cooldownMasks[i].fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                var cooldownLabel = cooldownLabels[i];
                if (i == (int)HudSkill.SwordTyphoon && remaining <= 0f && player.SwordTyphoonInProgress)
                {
                    cooldownMasks[i].enabled = true;
                    cooldownMasks[i].fillAmount = 1f;
                    cooldownLabel.font = noticeFont != null ? noticeFont : font;
                    cooldownLabel.fontSize = 14;
                    cooldownLabel.text = noticeFont != null ? "진행 중" : "ACTIVE";
                }
                else
                {
                    cooldownLabel.font = font;
                    cooldownLabel.fontSize = 23;
                    cooldownLabel.text = remaining > 0f ? Seconds(remaining) : string.Empty;
                }

                if (i == (int)HudSkill.Dash) continue;
                cooldownFlashes[i].alpha = Mathf.Clamp01(
                    (float)(cooldownFlashEnds[i] - Time.unscaledTimeAsDouble) / CooldownFlashDuration);
                bool lowMana = player.HasInsufficientManaFeedback((HudSkill)i);
                var key = keyLabels[i];
                key.font = lowMana && noticeFont != null ? noticeFont : font;
                key.fontSize = lowMana ? 12 : 15;
                key.color = lowMana ? ManaWarningColor : LabelColor;
                key.text = lowMana ? noticeFont != null ? "MP 부족" : "LOW MP" : Keys[i];
            }

            windstepActive.text = player.IsWindstepActive ? $"ON {Seconds(player.WindstepTimeRemaining)}" : string.Empty;
        }

        static void SetResourceBar(RectTransform fill, Text label, float current, float maximum, bool roundUp)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(current / Mathf.Max(1f, maximum)), 1f);
            int displayCurrent = roundUp ? Mathf.CeilToInt(current) : Mathf.FloorToInt(current);
            int displayMaximum = roundUp ? Mathf.CeilToInt(maximum) : Mathf.FloorToInt(maximum);
            label.text = $"{displayCurrent} / {displayMaximum}";
        }

        static string Seconds(float time) => time >= 1f
            ? Mathf.CeilToInt(time).ToString()
            : (Mathf.Ceil(time * 10f) / 10f).ToString("0.0");

        Text MakeLabel(string name, Transform parent, int size, Color color)
        {
            var text = MakeRect(name, parent).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectDistance = new Vector2(1f, -1f);
            shadow.effectColor = new Color(.025f, .05f, .08f, .95f);
            return text;
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var image = MakeRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static RectTransform MakeRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        static void Pin(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        void OnDestroy()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            if (noticeFont != null) Destroy(noticeFont);
        }
    }
}
