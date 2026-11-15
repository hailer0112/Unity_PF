using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Storybook
{
    public enum PalaceRoomType { Start, Combat, Recovery, Boss }

    public sealed class PalaceDungeonGraph
    {
        public sealed class Room
        {
            public int Id { get; }
            public PalaceRoomType Type { get; }
            public Vector2Int Grid { get; }
            public int Template { get; }
            public IReadOnlyList<Room> Neighbors => neighbors;
            internal readonly List<Room> neighbors = new();

            internal Room(int id, PalaceRoomType type, Vector2Int grid, int template)
            {
                Id = id;
                Type = type;
                Grid = grid;
                Template = template;
            }
        }

        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public IReadOnlyList<Room> Rooms => rooms;
        public Room Start => rooms[0];
        public Room Boss { get; private set; }
        readonly List<Room> rooms = new();

        public static PalaceDungeonGraph Generate(int seed, int minCombat, int maxCombat, int bossDepth, float recoveryChance, int templateCount = 2)
        {
            minCombat = Mathf.Clamp(minCombat, 1, 20);
            maxCombat = Mathf.Clamp(maxCombat, minCombat, 20);
            // Mix nearby seeds before using System.Random so signed and adjacent seeds vary independently.
            uint mixed = unchecked((uint)seed + 0x9E3779B9u);
            mixed = unchecked((mixed ^ (mixed >> 16)) * 0x85EBCA6Bu);
            mixed = unchecked((mixed ^ (mixed >> 13)) * 0xC2B2AE35u);
            var random = new Random(unchecked((int)(mixed ^ (mixed >> 16))));
            int combat = random.Next(minCombat, maxCombat + 1);
            int depth = Mathf.Clamp(bossDepth, 2, combat + 1);
            int pathLength = random.Next(depth, combat + 2);
            bool recovery = random.NextDouble() < Mathf.Clamp01(recoveryChance);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var graph = Build(random, combat, pathLength, recovery, Mathf.Max(1, templateCount));
                if (graph != null && graph.IsValid(depth)) return graph;
            }

            // A straight spine always leaves room for side branches.
            var fallback = Build(random, combat, combat + 1, recovery, Mathf.Max(1, templateCount), straight: true);
            if (fallback == null || !fallback.IsValid(depth))
                throw new InvalidOperationException("Failed to construct the fallback palace graph.");
            return fallback;
        }

        static PalaceDungeonGraph Build(Random random, int combat, int length, bool recovery, int templateCount, bool straight = false)
        {
            var path = new List<Vector2Int> { Vector2Int.zero };
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero };
            for (int i = 0; i < length; i++)
            {
                Vector2Int last = path[^1];
                var choices = new List<Vector2Int>(4);
                foreach (Vector2Int direction in Directions)
                    if ((!straight || direction == Vector2Int.up) && !occupied.Contains(last + direction))
                        choices.Add(last + direction);
                if (choices.Count == 0) return null;
                Vector2Int next = choices[random.Next(choices.Count)];
                path.Add(next);
                occupied.Add(next);
            }

            // Rotate the graph so the existing north-facing arena is always entered from the south.
            Vector2Int north = path[^1] - path[^2];
            Vector2Int east = new(north.y, -north.x);
            var graph = new PalaceDungeonGraph();
            occupied.Clear();
            for (int i = 0; i < path.Count; i++)
            {
                Vector2Int p = path[i];
                Vector2Int grid = new(p.x * east.x + p.y * east.y, p.x * north.x + p.y * north.y);
                occupied.Add(grid);
                Room parent = i == 0 ? null : graph.rooms[^1];
                PalaceRoomType type = i == 0 ? PalaceRoomType.Start :
                    i == length ? PalaceRoomType.Boss : PalaceRoomType.Combat;
                Room room = graph.Add(type, grid, random, parent, templateCount);
                if (type == PalaceRoomType.Boss) graph.Boss = room;
            }

            int remainingCombat = combat - (length - 1);
            while (remainingCombat > 0 || recovery)
            {
                var sockets = new List<(Room parent, Vector2Int grid)>();
                foreach (Room room in graph.rooms)
                {
                    if (room.Type == PalaceRoomType.Boss || room.Type == PalaceRoomType.Recovery) continue;
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int grid = room.Grid + direction;
                        if (!occupied.Contains(grid)) sockets.Add((room, grid));
                    }
                }
                if (sockets.Count == 0) return null;
                var (parent, gridPosition) = sockets[random.Next(sockets.Count)];
                PalaceRoomType type = remainingCombat > 0 ? PalaceRoomType.Combat : PalaceRoomType.Recovery;
                graph.Add(type, gridPosition, random, parent, templateCount);
                occupied.Add(gridPosition);
                if (type == PalaceRoomType.Combat) remainingCombat--;
                else recovery = false;
            }
            return graph;
        }

        Room Add(PalaceRoomType type, Vector2Int grid, Random random, Room parent, int templateCount)
        {
            int template = type == PalaceRoomType.Combat ? random.Next(templateCount) : 0;
            var room = new Room(rooms.Count, type, grid, template);
            rooms.Add(room);
            if (parent != null)
            {
                parent.neighbors.Add(room);
                room.neighbors.Add(parent);
            }
            return room;
        }

        public bool IsValid(int bossDepth)
        {
            if (rooms.Count < 3 || Start.Type != PalaceRoomType.Start || Boss == null ||
                Boss.Neighbors.Count != 1 || Boss.Neighbors[0].Grid != Boss.Grid + Vector2Int.down)
                return false;
            var coordinates = new HashSet<Vector2Int>();
            foreach (Room room in rooms)
            {
                if (!coordinates.Add(room.Grid) || room.Type == PalaceRoomType.Recovery && room.Neighbors.Count != 1)
                    return false;
                var directions = new HashSet<Vector2Int>();
                foreach (Room neighbor in room.Neighbors)
                {
                    Vector2Int direction = neighbor.Grid - room.Grid;
                    if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 ||
                        !directions.Add(direction) || !neighbor.neighbors.Contains(room)) return false;
                }
            }

            var visited = new HashSet<Room> { Start };
            var queue = new Queue<(Room room, int depth)>();
            queue.Enqueue((Start, 0));
            int foundDepth = -1;
            while (queue.Count > 0)
            {
                var (room, depth) = queue.Dequeue();
                if (room == Boss) foundDepth = depth;
                foreach (Room neighbor in room.Neighbors)
                    if (visited.Add(neighbor)) queue.Enqueue((neighbor, depth + 1));
            }
            return visited.Count == rooms.Count && foundDepth >= bossDepth;
        }
    }
}
