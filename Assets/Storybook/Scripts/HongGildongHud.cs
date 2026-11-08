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

        readonly Image[] cooldownMasks = new Image[Keys.Length];
        readonly Text[] cooldownLabels = new Text[Keys.Length];
        HongGildong player;
        PlayerHealth health;
        Canvas canvas;
        RectTransform healthFill;
        Text healthLabel, windstepActive;
        Font font;

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

            BuildHealth(healthFrame);
            BuildSkills(icons);
            Update();
        }

        void BuildHealth(Sprite frame)
        {
            var panel = MakeImage("HP frame", canvas.transform, frame, Color.white);
            Pin(panel.rectTransform, Vector2.up, Vector2.up, new Vector2(20f, -18f), new Vector2(232f, 78f));

            var track = MakeRect("HP fill track", panel.transform);
            track.anchorMin = new Vector2(.264f, .27f);
            track.anchorMax = new Vector2(.895f, .604f);
            track.offsetMin = track.offsetMax = Vector2.zero;

            var fill = MakeImage("HP fill", track, null, new Color(.73f, .17f, .24f));
            healthFill = fill.rectTransform;
            Stretch(healthFill);
            var gleam = MakeImage("HP upper highlight", healthFill, null, new Color(1f, .41f, .44f, .57f));
            gleam.rectTransform.anchorMin = new Vector2(0f, .73f);
            gleam.rectTransform.anchorMax = Vector2.one;
            gleam.rectTransform.offsetMin = gleam.rectTransform.offsetMax = Vector2.zero;
            healthLabel = MakeLabel("Current HP / Max HP", track, 13, Color.white);
            Stretch(healthLabel.rectTransform);
        }

        void BuildSkills(Sprite[] icons)
        {
            var row = MakeRect("Skill row", canvas.transform);
            Pin(row, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 16f),
                new Vector2(SlotSpacing * Keys.Length, 88f));

            for (int i = 0; i < icons.Length; i++)
            {
                var tile = MakeRect(SpriteNames[i] + " slot", row);
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

                if (i != (int)HudSkill.Windstep) continue;
                windstepActive = MakeLabel("Windstep duration", art.transform, 11, new Color(.48f, 1f, .87f));
                Pin(windstepActive.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
                    new Vector2(0f, 5f), new Vector2(IconSize, 16f));
            }
        }

        void Update()
        {
            if (canvas == null) return;

            healthFill.anchorMax = new Vector2(Mathf.Clamp01(health.CurrentHP / Mathf.Max(1f, health.MaxHP)), 1f);
            healthLabel.text = $"{Mathf.CeilToInt(health.CurrentHP)} / {Mathf.CeilToInt(health.MaxHP)}";

            for (int i = 0; i < cooldownMasks.Length; i++)
            {
                var (remaining, duration) = player.GetHudCooldown((HudSkill)i);
                cooldownMasks[i].enabled = remaining > 0f;
                cooldownMasks[i].fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
                cooldownLabels[i].text = remaining > 0f ? Seconds(remaining) : string.Empty;
            }

            windstepActive.text = player.IsWindstepActive ? $"ON {Seconds(player.WindstepTimeRemaining)}" : string.Empty;
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
        }
    }
}
