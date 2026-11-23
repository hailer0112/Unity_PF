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

        readonly Queue<(int amount, float expires)> fragmentGains = new();
        PlayerHealth fragmentOwner;
        PalaceDungeon dungeon;
        PalaceDungeonRoom currentRoom;
        Font font;
        Texture2D fragmentIcon;
        GUIStyle label, roomIcon, promptStyle;

        internal void Initialize(PalaceDungeon owner, PlayerHealth playerHealth)
        {
            dungeon = owner;
            if (fragmentOwner != playerHealth)
            {
                if (fragmentOwner != null) fragmentOwner.StoryFragmentsGained -= ShowFragmentGain;
                fragmentGains.Clear();
                fragmentOwner = playerHealth;
                if (fragmentOwner != null) fragmentOwner.StoryFragmentsGained += ShowFragmentGain;
            }
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
            if (roomIcon != null)
                roomIcon.fontSize = Mathf.Clamp(Mathf.FloorToInt(tiles[currentRoom].width * .76f), 8, 12);
        }

        public bool IsVisible(PalaceDungeonRoom room) => discoveredRooms.Contains(room);

        void LateUpdate()
        {
            UpdateDiscovery();
            while (fragmentGains.Count > 0 && fragmentGains.Peek().expires <= Time.unscaledTime)
                fragmentGains.Dequeue();
        }

        void ShowFragmentGain(int amount)
        {
            if (fragmentGains.Count >= 4) fragmentGains.Dequeue();
            fragmentGains.Enqueue((amount, Time.unscaledTime + 1.5f));
        }

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
            Rect map = new(Screen.width / mapScale - MapWidth - 18f, 18f, MapWidth, MapHeight);
            DrawMap(map);
            DrawFragments(map);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var recovery = currentRoom.Recovery;
            string prompt = dungeon.PortalPrompt ?? (recovery != null ? recovery.Prompt : null) ?? dungeon.ChestPrompt;
            if (prompt != null)
                DrawMessage(prompt.Contains('\n')
                    ? new Rect(width * .5f - 210f, height - 185f, 420f, 68f)
                    : new Rect(width * .5f - 155f, height - 150f, 310f, 33f), prompt);
            if (dungeon.ChestResult is string result)
                DrawMessage(new Rect(width * .5f - 210f, height - 215f, 420f, 33f), result);

            GUI.color = tint;
            GUI.matrix = matrix;
        }

        void DrawMessage(Rect bounds, string message)
        {
            Fill(bounds, Trim);
            Fill(new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4), Panel);
            GUI.Label(bounds, message, promptStyle);
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
            fragmentIcon = Resources.Load<Texture2D>("StoryFragment");
        }

        void DrawMap(Rect bounds)
        {
            Fill(bounds, Trim);
            Fill(new Rect(bounds.x + 2, bounds.y + 2, bounds.width - 4, bounds.height - 4), Panel);
            GUI.Label(new Rect(bounds.x, bounds.y + 3, bounds.width, 22), dungeon.IsBossFloor ? "용왕 구역" : $"용궁 · {dungeon.Floor}구역", label);

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
                    PalaceRoomType.Portal => "◎",
                    PalaceRoomType.Merchant => "상",
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

        void DrawFragments(Rect map)
        {
            if (fragmentOwner == null) return;
            Rect panel = new(map.x, map.yMax + 6f, map.width, 34f);
            Fill(panel, Trim);
            Fill(new Rect(panel.x + 1f, panel.y + 1f, panel.width - 2f, panel.height - 2f), Panel);
            if (fragmentIcon != null)
                GUI.DrawTexture(new Rect(panel.x + 7f, panel.y + 1f, 32f, 32f), fragmentIcon, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(panel.x + 39f, panel.y, panel.width - 45f, panel.height),
                $"이야기 파편  {fragmentOwner.StoryFragments:N0}", label);

            float y = panel.yMax + 2f;
            foreach (var gain in fragmentGains)
            {
                var gainRect = new Rect(map.xMax - 68f, y, 68f, 19f);
                Fill(gainRect, Panel);
                GUI.Label(gainRect, $"+{gain.amount}", label);
                y += gainRect.height;
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
            if (fragmentOwner != null) fragmentOwner.StoryFragmentsGained -= ShowFragmentGain;
            if (font != null) Destroy(font);
        }
    }
}
