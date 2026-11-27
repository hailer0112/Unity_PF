using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Sprites;

namespace Storybook
{
    [DefaultExecutionOrder(-600)]
    internal sealed class RelicInventoryPanel : MonoBehaviour
    {
        static readonly Color Gold = new(.80f, .64f, .36f);
        static readonly Color Ink = new(.94f, .90f, .80f);
        static readonly Color Muted = new(.72f, .69f, .73f);
        static readonly Color Panel = new(.075f, .064f, .12f);
        static readonly Color Well = new(.105f, .093f, .16f);
        static RelicInventoryPanel active;
        static int closedFrame = -1;

        PlayerHealth owner;
        HongGildong player;
        float previousTimeScale;
        bool tabHeld;
        int selected;
        Vector2 scroll;
        Font font;
        GUIStyle title, body, heading, right;

        internal static bool BlocksGameplay => active != null || closedFrame == Time.frameCount;

        internal void Initialize(PlayerHealth health)
        {
            owner = health;
            player = health.GetComponent<HongGildong>();
        }

        void Update()
        {
            bool held = Keyboard.current?.tabKey.isPressed == true;
            bool pressed = held && !tabHeld;
            tabHeld = held;
            if (!pressed) return;

            if (active == this) Close();
            else if (active == null && CanOpen()) Open();
        }

        bool CanOpen()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return false;
#endif
            return player != null && player.isActiveAndEnabled && owner.isActiveAndEnabled && !owner.IsDead &&
                !StorybookTransition.IsBusy && !NpcDialogue.BlocksInteraction;
        }

        void Open()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            active = this;
            scroll = Vector2.zero;
        }

        void Close()
        {
            if (active != this) return;
            active = null;
            closedFrame = Time.frameCount;
            Time.timeScale = previousTimeScale;
        }

        void OnDisable() => Close();

        void OnDestroy()
        {
            if (font != null) Destroy(font);
        }

        void OnGUI()
        {
            if (active != this) return;
            if (body == null) InitializeStyles();

            Matrix4x4 matrix = GUI.matrix;
            int depth = GUI.depth;
            GUI.depth = -1000;
            GUI.matrix = Matrix4x4.identity;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.018f, .013f, .033f, .78f));

            const float width = 820f;
            const float height = 466f;
            float scale = Mathf.Min(1.6f, (Screen.width - 24f) / width, (Screen.height - 24f) / height);
            if (scale > 0f)
            {
                Vector3 offset = new((Screen.width - width * scale) * .5f, (Screen.height - height * scale) * .5f);
                GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));
                DrawPanel(width, height);
            }
            GUI.matrix = matrix;
            GUI.depth = depth;
        }

        void DrawPanel(float width, float height)
        {
            var relics = owner.Relics.Owned;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, relics.Count - 1));
            Frame(new Rect(0, 0, width, height), Gold, Panel);
            GUI.Label(new Rect(27, 15, 520, 44), "동화 유물", title);
            GUI.Label(new Rect(width - 142, 19, 114, 36), $"{relics.Count} / {FairyTaleRelicInventory.Capacity}", right);
            Fill(new Rect(25, 68, width - 50, 1), Gold);

            for (int i = 0; i < FairyTaleRelicInventory.Capacity; i++)
            {
                var slot = new Rect(27 + i % 3 * 96, 99 + i / 3 * 104, 84, 91);
                bool occupied = i < relics.Count;
                Frame(slot, occupied && i == selected ? Gold : new Color(.32f, .26f, .32f), Well);
                if (!occupied) continue;

                if (GUI.Button(slot, new GUIContent("", relics[i].DisplayName), GUIStyle.none))
                {
                    selected = i;
                    scroll = Vector2.zero;
                }
                DrawIcon(relics[i].GetOwnedIcon(owner), new Rect(slot.x + 12, slot.y + 13, 60, 65));
            }

            var detail = new Rect(327, 88, width - 355, 319);
            Frame(detail, new Color(.39f, .30f, .31f), Well);
            if (relics.Count == 0)
                GUI.Label(new Rect(detail.x + 19, detail.y + 19, detail.width - 38, 55),
                    "획득한 동화 유물이 없습니다.", body);
            else
                DrawDetail(detail, relics[selected]);

            GUI.Label(new Rect(27, height - 48, width - 54, 32), "TAB - 닫기", body);
        }

        void DrawDetail(Rect bounds, FairyTaleRelic relic)
        {
            var picture = new Rect(bounds.x + 16, bounds.y + 16, 92, 88);
            Frame(picture, Gold, Panel);
            DrawIcon(relic.GetOwnedIcon(owner), new Rect(picture.x + 10, picture.y + 10, 72, 68));
            var name = new Rect(picture.xMax + 14, picture.y + 2, bounds.width - 140, 56);
            GUI.Label(name, relic.DisplayName, heading);
            GUI.Label(new Rect(name.x, name.y + 60, name.width, 23), relic.CategoryLabel, body);

            var view = new Rect(bounds.x + 16, picture.yMax + 15, bounds.width - 32, bounds.height - 130);
            string activeEffect = relic.DescribeActiveEffect(owner);
            float textWidth = view.width - 22;
            float descriptionHeight = TextHeight(relic.Description, textWidth);
            float effectHeight = string.IsNullOrEmpty(activeEffect) ? 0f : TextHeight(activeEffect, textWidth);
            float flavorHeight = TextHeight(relic.FlavorText, textWidth);
            float contentHeight = descriptionHeight + effectHeight + flavorHeight + 24f;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, textWidth, contentHeight));
            float y = 0f;
            GUI.Label(new Rect(0, y, textWidth, descriptionHeight), relic.Description, body);
            y += descriptionHeight + 10f;
            if (effectHeight > 0f)
            {
                Color previous = GUI.contentColor;
                GUI.contentColor = Gold;
                GUI.Label(new Rect(0, y, textWidth, effectHeight), activeEffect, body);
                GUI.contentColor = previous;
                y += effectHeight + 10f;
            }
            GUI.Label(new Rect(0, y, textWidth, flavorHeight), relic.FlavorText, body);
            GUI.EndScrollView();
        }

        float TextHeight(string text, float width) => Mathf.Ceil(body.CalcHeight(new GUIContent(text), width)) + 3f;

        static void DrawIcon(Sprite sprite, Rect bounds)
        {
            if (sprite == null) return;
            Vector4 uv = DataUtility.GetOuterUV(sprite);
            float scale = Mathf.Min(bounds.width / sprite.rect.width, bounds.height / sprite.rect.height);
            Vector2 size = sprite.rect.size * scale;
            GUI.DrawTextureWithTexCoords(new Rect(bounds.center - size * .5f, size), sprite.texture,
                Rect.MinMaxRect(uv.x, uv.y, uv.z, uv.w));
        }

        void InitializeStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 18);
            body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                wordWrap = true,
                normal = { textColor = Ink }
            };
            if (font != null) body.font = font;
            heading = new GUIStyle(body) { fontSize = 23, fontStyle = FontStyle.Bold };
            title = new GUIStyle(heading) { fontSize = 30, normal = { textColor = Gold } };
            right = new GUIStyle(body) { alignment = TextAnchor.MiddleRight, fontSize = 21, normal = { textColor = Muted } };
        }

        static void Frame(Rect rect, Color edge, Color background)
        {
            Fill(rect, edge);
            Fill(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), background);
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
