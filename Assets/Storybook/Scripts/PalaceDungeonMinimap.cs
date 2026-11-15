using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    [DefaultExecutionOrder(100)]
    public sealed class PalaceDungeonMinimap : MonoBehaviour
    {
        const float MapWidth = 198f;
        const float MapHeight = 186f;
        static readonly Rect LayoutArea = new(12f, 28f, MapWidth - 24f, MapHeight - 38f);
        static readonly Color Panel = new(.035f, .095f, .13f, .95f);
        static readonly Color Trim = new(.58f, .48f, .31f);
        static readonly Color Unvisited = new(.23f, .32f, .37f);
        static readonly Color Visited = new(.24f, .54f, .59f);
        static readonly Color Cleared = new(.24f, .63f, .47f);
        static readonly Color Current = new(.96f, .78f, .39f);
        static readonly Color Ink = new(.95f, .94f, .85f);

        readonly Dictionary<PalaceDungeonRoom, Rect> tiles = new();
        readonly HashSet<PalaceDungeonRoom> discoveredRooms = new();
        readonly HashSet<(int from, int to)> discoveredLinks = new();
        readonly List<(PalaceDungeonRoom first, PalaceDungeonRoom second, Rect line)> links = new();

        PalaceDungeon dungeon;
        PalaceDungeonRoom currentRoom;
        Font font;
        GUIStyle label, roomIcon, promptStyle;

        internal void Initialize(PalaceDungeon owner)
        {
            dungeon = owner;
            tiles.Clear();
            links.Clear();
            discoveredRooms.Clear();
            discoveredLinks.Clear();
            currentRoom = null;

            Vector2Int min = owner.Layout.Start.Grid, max = min;
            foreach (var room in owner.Rooms)
            {
                min = Vector2Int.Min(min, room.Layout.Grid);
                max = Vector2Int.Max(max, room.Layout.Grid);
            }

            Vector2Int span = max - min + Vector2Int.one;
            Vector2 center = ((Vector2)min + (Vector2)max) * .5f;
            float spacing = Mathf.Min(27f, LayoutArea.width / span.x, LayoutArea.height / span.y);
            float size = spacing * .7f;
            foreach (var room in owner.Rooms)
            {
                Vector2Int grid = room.Layout.Grid;
                Vector2 position = LayoutArea.center + new Vector2(grid.x - center.x, center.y - grid.y) * spacing;
                tiles.Add(room, new Rect(position - Vector2.one * (size * .5f), Vector2.one * size));
            }

            foreach (var room in owner.Rooms)
                foreach (var neighbor in room.Layout.Neighbors)
                {
                    if (neighbor.Id <= room.Layout.Id) continue;
                    var other = owner.Rooms[neighbor.Id];
                    Vector2 a = tiles[room].center, b = tiles[other].center;
                    var line = new Rect(Mathf.Min(a.x, b.x) - 1f, Mathf.Min(a.y, b.y) - 1f,
                        Mathf.Abs(a.x - b.x) + 2f, Mathf.Abs(a.y - b.y) + 2f);
                    links.Add((room, other, line));
                }

            UpdateDiscovery();
        }

        public bool IsVisible(PalaceDungeonRoom room) => discoveredRooms.Contains(room);

        void LateUpdate() => UpdateDiscovery();

        void UpdateDiscovery()
        {
            if (dungeon == null || dungeon.Current == null || currentRoom == dungeon.Current) return;
            currentRoom = dungeon.Current;
            discoveredRooms.Add(currentRoom);
            foreach (var neighbor in currentRoom.Layout.Neighbors)
            {
                discoveredRooms.Add(dungeon.Rooms[neighbor.Id]);
                discoveredLinks.Add(LinkKey(currentRoom.Layout.Id, neighbor.Id));
            }
        }

        static (int, int) LinkKey(int a, int b) => a < b ? (a, b) : (b, a);

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || currentRoom == null || StorybookTransition.IsBusy ||
                (currentRoom.Layout.Type == PalaceRoomType.Boss && currentRoom.Cleared))
                return;
            if (label == null) InitializeStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), .50f, 2f);
            Matrix4x4 matrix = GUI.matrix;
            Color tint = GUI.color;
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            // Keep the same top-right anchor through room changes, including the boss room.
            float mapScale = Mathf.Min(scale, Screen.width * .21f / (MapWidth + 18f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(mapScale, mapScale, 1f));
            DrawMap(new Rect(Screen.width / mapScale - MapWidth - 18f, 18f, MapWidth, MapHeight));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var recovery = currentRoom.Recovery;
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
            roomIcon = new GUIStyle(label) { padding = new RectOffset(), clipping = TextClipping.Clip,
                fontSize = Mathf.Clamp(Mathf.FloorToInt(tiles[currentRoom].width * .76f), 8, 12) };
            promptStyle = new GUIStyle(label) { fontSize = 17 };
        }

        void DrawMap(Rect bounds)
        {
            Fill(bounds, Trim);
            Fill(new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4), Panel);
            GUI.Label(new Rect(bounds.x, bounds.y + 3, bounds.width, 22), "용궁", label);

            foreach (var (first, second, line) in links)
            {
                if (!discoveredLinks.Contains(LinkKey(first.Layout.Id, second.Layout.Id))) continue;
                Fill(Offset(line, bounds.position), first.Visited && second.Visited ? Visited : Unvisited);
            }

            foreach (var (room, localTile) in tiles)
            {
                if (!IsVisible(room)) continue;
                Rect tile = Offset(localTile, bounds.position);
                bool active = room == currentRoom;
                Fill(tile, active ? Current : room.Cleared ? Cleared : room.Visited ? Visited : Unvisited);
                float border = Mathf.Min(2f, tile.width * .22f);
                Fill(new Rect(tile.x + border, tile.y + border, tile.width - 2f * border,
                    tile.height - 2f * border), Panel);

                string icon = room.Layout.Type switch
                {
                    PalaceRoomType.Start => "◆",
                    PalaceRoomType.Recovery => room.RewardUsed ? "·" : "+",
                    PalaceRoomType.Boss => "♛",
                    _ => room.Cleared ? "·" : ""
                };
                if (tile.width >= 8f && !string.IsNullOrEmpty(icon))
                    GUI.Label(tile, icon, roomIcon);

                if (active)
                {
                    float size = Mathf.Clamp(tile.width * .25f, 2f, 5f);
                    Fill(new Rect(tile.xMax - size, tile.yMin, size, size), Ink);
                }
            }
        }

        static Rect Offset(Rect rect, Vector2 offset) => new(rect.position + offset, rect.size);

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
