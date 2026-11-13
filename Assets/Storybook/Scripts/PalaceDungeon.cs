using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class PalaceDungeon : MonoBehaviour
    {
        [SerializeField] HongGildong player;
        [SerializeField] ClearingCamera followCamera;
        [SerializeField] PalaceDungeonRoom startRoom, recoveryRoom, bossRoom;
        [SerializeField] DragonKingBossHud bossHud;
        [SerializeField] PalaceDungeonRoom[] combatRooms = new PalaceDungeonRoom[2];
        [SerializeField] GameObject passage;
        static readonly Vector2 GridSpacing = new(20f, 19f);
        [SerializeField, Min(1)] int minimumCombatRooms = 3;
        [SerializeField, Min(1)] int maximumCombatRooms = 5;
        [SerializeField, Min(2)] int minimumBossDepth = 3;
        [SerializeField, Range(0f, 1f)] float recoveryChance = .5f;
        [SerializeField, Range(.01f, 1f)] float recoveryAmount = .3f;
        [SerializeField] bool useFixedSeed;
        [SerializeField] int fixedSeed = 42;

        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        static int? previousSeed;
        static string previousLayout;
        readonly List<PalaceDungeonRoom> rooms = new();
        public IReadOnlyList<PalaceDungeonRoom> Rooms => rooms;
        public PalaceDungeonRoom Current { get; private set; }
        public PalaceDungeonGraph Layout { get; private set; }
        public int Seed { get; private set; }

        internal static Vector2Int Direction(int side) => Directions[side];

        void Start()
        {
            Generate();
            gameObject.AddComponent<PalaceDungeonMinimap>().Initialize(this);
        }

        void Generate()
        {
            Seed = useFixedSeed ? fixedSeed : System.Guid.NewGuid().GetHashCode();
            Layout = PalaceDungeonGraph.Generate(Seed, minimumCombatRooms, maximumCombatRooms, minimumBossDepth, recoveryChance);
            if (!useFixedSeed)
            {
                // Scene reloads reset run state. Avoid an identical layout on consecutive departures as well.
                string signature = Signature(Layout);
                for (int attempt = 0; attempt < 8 && (Seed == previousSeed || signature == previousLayout); attempt++)
                {
                    Seed = unchecked(Seed + 1);
                    Layout = PalaceDungeonGraph.Generate(Seed, minimumCombatRooms, maximumCombatRooms, minimumBossDepth, recoveryChance);
                    signature = Signature(Layout);
                }
                previousSeed = Seed;
                previousLayout = signature;
            }
            Debug.Log($"Palace dungeon seed {Seed}, {Layout.Rooms.Count} rooms.", this);

            var health = player.GetComponent<PlayerHealth>();
            Vector3 origin = bossRoom.transform.position;
            foreach (var node in Layout.Rooms)
            {
                PalaceDungeonRoom room = node.Type switch
                {
                    PalaceRoomType.Start => Instantiate(startRoom, Position(node), Quaternion.identity, transform),
                    PalaceRoomType.Combat => Instantiate(combatRooms[node.Template], Position(node), Quaternion.identity, transform),
                    PalaceRoomType.Recovery => Instantiate(recoveryRoom, Position(node), Quaternion.identity, transform),
                    _ => bossRoom
                };
                room.name = $"{node.Id:00} {node.Type} ({node.Grid.x}, {node.Grid.y})";
                room.Initialize(node, health, recoveryAmount);
                rooms.Add(room);
            }

            foreach (var room in rooms)
                foreach (var neighbor in room.Layout.Neighbors)
                    if (neighbor.Id > room.Layout.Id) Connect(room, rooms[neighbor.Id]);

            Bounds envelope = new(rooms[0].transform.position, Vector3.zero);
            foreach (var room in rooms) envelope.Encapsulate(room.transform.position);
            envelope.Expand(new Vector3(GridSpacing.x, 0f, GridSpacing.y));
            player.PlaceInDungeon(rooms[0].transform.position,
                Rect.MinMaxRect(envelope.min.x, envelope.min.z, envelope.max.x, envelope.max.z));
            Physics.SyncTransforms();
            Enter(rooms[0], true);

            Vector3 Position(PalaceDungeonGraph.Room node)
            {
                Vector2Int delta = node.Grid - Layout.Boss.Grid;
                return origin + new Vector3(delta.x * GridSpacing.x, 0f, delta.y * GridSpacing.y);
            }
        }

        void Connect(PalaceDungeonRoom a, PalaceDungeonRoom b)
        {
            Vector2Int direction = b.Layout.Grid - a.Layout.Grid;
            int side = System.Array.IndexOf(Directions, direction);
            Vector3 first = a.DoorwayCenter(side);
            Vector3 last = b.DoorwayCenter((side + 2) % Directions.Length);
            var link = Instantiate(passage, (first + last) * .5f, Quaternion.LookRotation(last - first), transform);
            link.name = $"Passage {a.Layout.Id:00}–{b.Layout.Id:00}";
            link.transform.localScale = new Vector3(1f, 1f, Vector3.Distance(first, last) + .12f);
        }

        void LateUpdate()
        {
            if (Current == null || player == null) return;
            Current.RefreshClear();
            if (Current.InCombat || !player.isActiveAndEnabled) return;

            // Only an adjacent room can receive the player, and only after the player is
            // clear of the door frame. Travel through a corridor keeps the last room selected.
            foreach (var neighbor in Current.Layout.Neighbors)
            {
                var next = rooms[neighbor.Id];
                if (!next.Contains(player.transform.position)) continue;
                Enter(next);
                break;
            }
        }

        void Enter(PalaceDungeonRoom room, bool snapCamera = false)
        {
            Current = room;
            room.Enter();
            if (room == bossRoom) bossHud.gameObject.SetActive(true);
            followCamera.SetDungeonRoom(room == bossRoom, snapCamera);
        }

        static string Signature(PalaceDungeonGraph layout)
        {
            var nodes = new List<string>();
            foreach (var room in layout.Rooms)
                foreach (var neighbor in room.Neighbors)
                    if (neighbor.Id > room.Id)
                    {
                        string a = $"{room.Grid}/{room.Type}";
                        string b = $"{neighbor.Grid}/{neighbor.Type}";
                        nodes.Add(string.CompareOrdinal(a, b) < 0 ? a + b : b + a);
                    }
            nodes.Sort(System.StringComparer.Ordinal);
            return string.Join("|", nodes);
        }
    }
}
