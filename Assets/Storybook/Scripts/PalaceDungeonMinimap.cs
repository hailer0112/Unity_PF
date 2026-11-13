using UnityEngine;

namespace Storybook
{
    public sealed class PalaceDungeonMinimap : MonoBehaviour
    {
        static readonly Color Panel = new(.035f, .095f, .13f, .95f);
        static readonly Color Trim = new(.58f, .48f, .31f);
        static readonly Color Unvisited = new(.23f, .32f, .37f);
        static readonly Color Visited = new(.24f, .54f, .59f);
        static readonly Color Cleared = new(.24f, .63f, .47f);
        static readonly Color Current = new(.96f, .78f, .39f);
        static readonly Color Ink = new(.95f, .94f, .85f);

        PalaceDungeon dungeon;
        Font font;
        GUIStyle label, promptStyle;

        internal void Initialize(PalaceDungeon owner) => dungeon = owner;

        public bool IsVisible(PalaceDungeonRoom room)
        {
            if (dungeon == null || dungeon.Current == null) return false;
            if (room.Visited) return true;
            foreach (var neighbor in dungeon.Current.Layout.Neighbors)
                if (room.Layout == neighbor) return true;
            return false;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || dungeon == null || dungeon.Current == null ||
                StorybookTransition.IsBusy || (dungeon.Current.Layout.Type == PalaceRoomType.Boss && dungeon.Current.Cleared))
                return;
            if (label == null) InitializeStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .50f, 2f);
            Matrix4x4 matrix = GUI.matrix;
            Color tint = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            // The original boss HUD occupies the top of the screen.
            bool bossActive = dungeon.Current.Layout.Type == PalaceRoomType.Boss;
            DrawMap(new Rect(width - 216f, bossActive ? 104f : 18f, 198f, 186f));

            var recovery = dungeon.Current.Recovery;
            string prompt = recovery != null ? recovery.Prompt : null;
            if (prompt != null)
            {
                var bounds = new Rect(width * .5f - 155f, height - 150f, 310f, 33f);
                Fill(bounds, Trim);
                Fill(new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4), Panel);
                GUI.Label(bounds, prompt, promptStyle);
            }

            GUI.color = tint;
            GUI.matrix = matrix;
        }

        void InitializeStyles()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR" }, 16);
            label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Ink }
            };
            if (font != null) label.font = font;
            promptStyle = new GUIStyle(label) { fontSize = 17 };
        }

        void DrawMap(Rect bounds)
        {
            Fill(bounds, Trim);
            Fill(new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4), Panel);
            GUI.Label(new Rect(bounds.x, bounds.y + 3, bounds.width, 22), "용궁", label);

            Vector2Int min = dungeon.Current.Layout.Grid, max = min;
            foreach (var room in dungeon.Rooms)
            {
                if (!IsVisible(room)) continue;
                min = Vector2Int.Min(min, room.Layout.Grid);
                max = Vector2Int.Max(max, room.Layout.Grid);
            }

            float spacing = Mathf.Min(27f, 145f / Mathf.Max(1, Mathf.Max(max.x - min.x, max.y - min.y) + 1));
            float size = spacing * .7f;
            Vector2 center = bounds.center + Vector2.down * 9f;
            Vector2 gridCenter = ((Vector2)min + (Vector2)max) * .5f;

            Vector2 Position(Vector2Int p) =>
                center + new Vector2(p.x - gridCenter.x, gridCenter.y - p.y) * spacing;

            foreach (var room in dungeon.Rooms)
            {
                if (!IsVisible(room)) continue;
                Vector2 a = Position(room.Layout.Grid);
                foreach (var neighbor in room.Layout.Neighbors)
                {
                    if (neighbor.Id <= room.Layout.Id || !IsVisible(dungeon.Rooms[neighbor.Id])) continue;
                    Vector2 b = Position(neighbor.Grid);
                    Fill(new Rect(Mathf.Min(a.x, b.x) - 1f, Mathf.Min(a.y, b.y) - 1f,
                        Mathf.Abs(a.x - b.x) + 2f, Mathf.Abs(a.y - b.y) + 2f), Visited);
                }
            }

            foreach (var room in dungeon.Rooms)
            {
                if (!IsVisible(room)) continue;
                Vector2 p = Position(room.Layout.Grid);
                Rect tile = new(p.x - size * .5f, p.y - size * .5f, size, size);
                Fill(tile, room == dungeon.Current ? Current : room.Cleared ? Cleared : room.Visited ? Visited : Unvisited);
                Fill(new Rect(tile.x + 2f, tile.y + 2f, size - 4f, size - 4f), Panel);
                string icon = room.Layout.Type switch
                {
                    PalaceRoomType.Start => "◆",
                    PalaceRoomType.Recovery => room.RewardUsed ? "·" : "+",
                    PalaceRoomType.Boss => "♛",
                    _ => room.Cleared ? "·" : ""
                };
                GUI.Label(tile, icon, label);
            }
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
