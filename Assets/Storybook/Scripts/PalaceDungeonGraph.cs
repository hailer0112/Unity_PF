using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Storybook
{
    public enum PalaceRoomType { Start, Combat, Recovery, Boss, Portal = 5 }

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

        const int MinimumPortalDepth = 3;
        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        public IReadOnlyList<Room> Rooms => rooms;
        public Room Start => rooms[0];
        public Room Destination { get; private set; }
        public Room Boss => Destination?.Type == PalaceRoomType.Boss ? Destination : null;
        public Room Portal => Destination?.Type == PalaceRoomType.Portal ? Destination : null;

        internal static PalaceDungeonGraph BossArena()
        {
            var graph = new PalaceDungeonGraph();
            var start = new Room(0, PalaceRoomType.Start, Vector2Int.down, 0);
            graph.Destination = new Room(1, PalaceRoomType.Boss, Vector2Int.zero, 0);
            graph.rooms.Add(start);
            graph.rooms.Add(graph.Destination);
            start.neighbors.Add(graph.Destination);
            graph.Destination.neighbors.Add(start);
            return graph;
        }
        readonly List<Room> rooms = new();

        public static PalaceDungeonGraph Generate(int seed, int minCombat, int maxCombat, int bossDepth, float recoveryChance, int templateCount = 2)
        {
            Random random = SeededRandom(seed);
            int combat = CombatCount(random, minCombat, maxCombat);
            int depth = Mathf.Clamp(bossDepth, 2, combat + 1);
            int pathLength = random.Next(depth, combat + 2);
            bool recovery = random.NextDouble() < Mathf.Clamp01(recoveryChance);
            for (int attempt = 0; attempt < 32; attempt++)
            {
                var graph = BuildBossRoute(random, combat, pathLength, recovery, Mathf.Max(1, templateCount));
                if (graph != null && graph.IsValid(depth)) return graph;
            }

            var fallback = BuildBossRoute(random, combat, combat + 1, recovery, Mathf.Max(1, templateCount), straight: true);
            if (fallback == null || !fallback.IsValid(depth))
                throw new InvalidOperationException("Failed to construct the fallback palace graph.");
            return fallback;
        }

        internal static PalaceDungeonGraph GenerateFloor(int seed, int minCombat, int maxCombat, float recoveryChance, int templateCount)
        {
            Random random = SeededRandom(seed);
            int combat = CombatCount(random, Mathf.Max(3, minCombat), maxCombat);
            templateCount = Mathf.Max(1, templateCount);
            bool recovery = random.NextDouble() < Mathf.Clamp01(recoveryChance);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                var graph = BuildFloor(random, combat, recovery, templateCount, reserveRouteFirst: false);
                if (graph.IsCompleteFloor(combat, recovery)) return graph;
            }

            // Reserve the guaranteed three-passage route before adding branches.
            var fallback = BuildFloor(random, combat, recovery, templateCount, reserveRouteFirst: true);
            if (fallback.IsCompleteFloor(combat, recovery)) return fallback;
            throw new InvalidOperationException($"Palace floor seed {seed} generated {fallback.Rooms.Count} of " +
                $"{combat + 2 + (recovery ? 1 : 0)} rooms. Actual: {fallback.RoomCounts()}.");
        }

        static PalaceDungeonGraph BuildFloor(Random random, int combat, bool recovery, int templateCount, bool reserveRouteFirst)
        {
            var graph = new PalaceDungeonGraph();
            var occupied = new HashSet<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.up * 2 };
            var start = graph.Add(PalaceRoomType.Start, Vector2Int.zero, random, null, templateCount);
            var north = graph.Add(PalaceRoomType.Combat, Vector2Int.up, random, start, templateCount);
            graph.Add(PalaceRoomType.Combat, Vector2Int.up * 2, random, north, templateCount);

            int branches = combat - 3; // Keep one combat room available on the far side of the portal.
            if (!reserveRouteFirst && !graph.AddBranches(random, occupied, branches, false, templateCount))
                return graph;

            var candidates = new List<(Room approach, Vector2Int portal, Vector2Int onward)>();
            foreach (var (room, distance) in graph.DistancesFromStart())
            {
                if (distance < MinimumPortalDepth - 1) continue;
                foreach (var direction in Directions)
                {
                    Vector2Int portal = room.Grid + direction;
                    if (occupied.Contains(portal) || Mathf.Abs(portal.x) + Mathf.Abs(portal.y) <= 1) continue;
                    foreach (var exit in Directions)
                    {
                        Vector2Int onward = portal + exit;
                        if (!occupied.Contains(onward)) candidates.Add((room, portal, onward));
                    }
                }
            }
            if (candidates.Count == 0) return graph;

            var chosen = candidates[random.Next(candidates.Count)];
            graph.Destination = graph.Add(PalaceRoomType.Portal, chosen.portal, random, chosen.approach, templateCount);
            occupied.Add(chosen.portal);
            graph.Add(PalaceRoomType.Combat, chosen.onward, random, graph.Destination, templateCount);
            occupied.Add(chosen.onward);
            graph.AddBranches(random, occupied, reserveRouteFirst ? branches : 0, recovery, templateCount);
            return graph;
        }

        bool IsCompleteFloor(int combat, bool recovery) =>
            rooms.FindAll(room => room.Type == PalaceRoomType.Combat).Count == combat &&
            rooms.Exists(room => room.Type == PalaceRoomType.Recovery) == recovery && IsValid(MinimumPortalDepth);

        internal string RoomCounts()
        {
            int start = 0, combat = 0, recovery = 0, portal = 0, boss = 0;
            foreach (var room in rooms)
                switch (room.Type)
                {
                    case PalaceRoomType.Start: start++; break;
                    case PalaceRoomType.Combat: combat++; break;
                    case PalaceRoomType.Recovery: recovery++; break;
                    case PalaceRoomType.Portal: portal++; break;
                    case PalaceRoomType.Boss: boss++; break;
                }
            return $"Start={start}, Combat={combat}, Recovery={recovery}, Portal={portal}, Boss={boss}";
        }

        static int CombatCount(Random random, int minCombat, int maxCombat)
        {
            int minimum = Mathf.Clamp(minCombat, 1, 20);
            return random.Next(minimum, Mathf.Clamp(maxCombat, minimum, 20) + 1);
        }

        static Random SeededRandom(int seed)
        {
            // Mix nearby seeds before using System.Random so signed and adjacent seeds vary independently.
            uint mixed = unchecked((uint)seed + 0x9E3779B9u);
            mixed = unchecked((mixed ^ (mixed >> 16)) * 0x85EBCA6Bu);
            mixed = unchecked((mixed ^ (mixed >> 13)) * 0xC2B2AE35u);
            return new Random(unchecked((int)(mixed ^ (mixed >> 16))));
        }

        static PalaceDungeonGraph BuildBossRoute(Random random, int combat, int length, bool recovery,
            int templateCount, bool straight = false)
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

            // Orient the terminal room's approach to the south.
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
                if (i == length) graph.Destination = room;
            }

            return graph.AddBranches(random, occupied, combat - (length - 1), recovery, templateCount) ? graph : null;
        }

        bool AddBranches(Random random, HashSet<Vector2Int> occupied, int remainingCombat, bool recovery, int templateCount)
        {
            while (remainingCombat > 0 || recovery)
            {
                var sockets = new List<(Room parent, Vector2Int grid)>();
                foreach (Room room in rooms)
                {
                    if (room.Type is PalaceRoomType.Boss or PalaceRoomType.Recovery) continue;
                    foreach (Vector2Int direction in Directions)
                    {
                        Vector2Int grid = room.Grid + direction;
                        if (!occupied.Contains(grid)) sockets.Add((room, grid));
                    }
                }
                if (sockets.Count == 0) return false;
                var (parent, gridPosition) = sockets[random.Next(sockets.Count)];
                PalaceRoomType type = remainingCombat > 0 ? PalaceRoomType.Combat : PalaceRoomType.Recovery;
                Add(type, gridPosition, random, parent, templateCount);
                occupied.Add(gridPosition);
                if (type == PalaceRoomType.Combat) remainingCombat--;
                else recovery = false;
            }
            return true;
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

        public bool IsValid(int minimumDepth)
        {
            if (rooms.Count < 2 || Start.Type != PalaceRoomType.Start || Destination == null) return false;
            if (Boss != null && (Boss.Neighbors.Count != 1 || Boss.Neighbors[0].Grid != Boss.Grid + Vector2Int.down))
                return false;
            if (Portal != null && (Start.neighbors.Contains(Portal) ||
                Mathf.Abs(Portal.Grid.x) + Mathf.Abs(Portal.Grid.y) <= 1 ||
                Portal.neighbors.Count < 2 ||
                !Start.neighbors.Exists(room => room.Grid == Vector2Int.up && room.Type == PalaceRoomType.Combat)))
                return false;
            var coordinates = new HashSet<Vector2Int>();
            int combatCount = 0, recoveryCount = 0;
            foreach (Room room in rooms)
            {
                if (!coordinates.Add(room.Grid) ||
                    room.Type == PalaceRoomType.Start && room != Start ||
                    (room.Type is PalaceRoomType.Portal or PalaceRoomType.Boss) && room != Destination)
                    return false;
                if (room.Type == PalaceRoomType.Recovery && (++recoveryCount > 1 || room.Neighbors.Count != 1))
                    return false;
                if (room.Type == PalaceRoomType.Combat) combatCount++;
                var directions = new HashSet<Vector2Int>();
                foreach (Room neighbor in room.Neighbors)
                {
                    Vector2Int direction = neighbor.Grid - room.Grid;
                    if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1 ||
                        !directions.Add(direction) || !neighbor.neighbors.Contains(room)) return false;
                }
            }

            Dictionary<Room, int> distances = DistancesFromStart();
            int requiredDepth = Portal != null ? Mathf.Max(MinimumPortalDepth, minimumDepth) : minimumDepth;
            int minimumCombat = Portal != null ? 3 : rooms.Count == 2 ? 0 : 1;
            return combatCount >= minimumCombat && distances.Count == rooms.Count &&
                distances.TryGetValue(Destination, out int depth) && depth >= requiredDepth;
        }

        Dictionary<Room, int> DistancesFromStart()
        {
            var distances = new Dictionary<Room, int> { [Start] = 0 };
            var queue = new Queue<Room>();
            queue.Enqueue(Start);
            while (queue.Count > 0)
            {
                Room room = queue.Dequeue();
                foreach (var neighbor in room.Neighbors)
                    if (distances.TryAdd(neighbor, distances[room] + 1)) queue.Enqueue(neighbor);
            }
            return distances;
        }
    }
}
