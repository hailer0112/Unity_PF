using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed partial class PalaceDungeon : MonoBehaviour
    {
        const int LastRegularFloor = 3;
        // Well beyond one floor's grid. Any world-space effects still expiring stay at their original position.
        const float FloorSpacing = 1280f;
        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        [SerializeField] HongGildong player;
        [SerializeField] ClearingCamera followCamera;
        [SerializeField] PalaceDungeonRoom startRoom, recoveryRoom, merchantRoom, bossRoom;
        [SerializeField] DragonKingBossHud bossHud;
        [SerializeField] PalaceDungeonRoom[] combatRooms = new PalaceDungeonRoom[2];
        [SerializeField] GameObject passage;
        [SerializeField, Min(1f), Tooltip("Distance between the east and west doorways of neighboring rooms.")]
        float eastWestPassageLength = 4.5f;
        [SerializeField, Min(1f), Tooltip("Distance between the north and south doorways of neighboring rooms.")]
        float northSouthPassageLength = 5f;
        [SerializeField] GameObject floorPortalEffect;
        [SerializeField] PalaceRewardChest rewardChestPrefab;
        [SerializeField] PalaceChestRewards chestRewards = new();
        [SerializeField] PalaceRoomWaves.Settings encounterWaves = new();
        [SerializeField, Min(3)] int minimumCombatRooms = 5;
        [SerializeField, Min(3)] int maximumCombatRooms = 7;
        [SerializeField, Range(0f, 1f)] float recoveryChance = .5f;
        [SerializeField, Range(.01f, 1f)] float recoveryAmount = .4f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance for each merchant room slot on a regular floor.")]
        float merchantChance = .35f;
        [SerializeField, Range(0, 3)] int maximumMerchantRooms = 1;
        [SerializeField] bool useFixedSeed;
        [SerializeField] int fixedSeed = 42;

        readonly List<PalaceDungeonRoom> rooms = new();
        readonly HashSet<string> floorLayouts = new();
        readonly System.Random chestRandom = new();
        PalaceDungeonMinimap minimap;
        PlayerHealth health;
        Transform floorRoot;
        PalaceDungeonRoom portalRoom;
        SphereCollider portalRange;
        GameObject portalVisual;
        int runSeed;

        public IReadOnlyList<PalaceDungeonRoom> Rooms => rooms;
        public PalaceDungeonRoom Current { get; private set; }
        public PalaceDungeonGraph Layout { get; private set; }
        public int Seed { get; private set; }
        public int Floor { get; private set; } = 1;
        public bool IsBossFloor => Floor > LastRegularFloor;
        internal string PortalPrompt => CanUsePortal() ? "F - 다음 구역으로 이동" : null;
        internal string ChestPrompt => !CanUseChest() ? null :
            Current.RewardChest.CanCollect(player.transform.position) ? Current.RewardChest.PickupPrompt :
            Current.RewardChest.CanOpen(player.transform.position) ? "F - 보상 상자 열기" : null;
        internal string ChestResult => Time.unscaledTimeAsDouble < chestResultUntil ? chestResult : null;
        string chestResult;
        double chestResultUntil;

        internal static Vector2Int Direction(int side) => Directions[side];

        void Awake() => bossRoom.gameObject.SetActive(false);

        void Start()
        {
            health = player.GetComponent<PlayerHealth>();
            runSeed = useFixedSeed ? fixedSeed : Guid.NewGuid().GetHashCode();
            minimap = gameObject.AddComponent<PalaceDungeonMinimap>();
            GenerateFloor();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<PalaceDeveloperMenu>().Initialize(this, player, health);
#endif
        }

        void GenerateFloor(float? recoveryChanceOverride = null, bool requireMerchant = false)
        {
            Seed = unchecked(runSeed + (Floor - 1) * 486187739);
            if (IsBossFloor)
            {
                GenerateBossFloor();
                return;
            }

            float shopChance = merchantRoom == null ? 0f : requireMerchant ? 1f : merchantChance;
            int shopLimit = requireMerchant ? 1 : Mathf.Clamp(maximumMerchantRooms, 0, 3);
            for (int attempt = 0; ; attempt++)
            {
                Layout = PalaceDungeonGraph.GenerateFloor(Seed, minimumCombatRooms, maximumCombatRooms,
                    recoveryChanceOverride ?? recoveryChance, combatRooms.Length, shopChance, shopLimit);
                if (floorLayouts.Add(Signature(Layout)) || attempt >= 8) break;
                Seed = unchecked(Seed + 1);
            }

            CreateFloorRoot();
            // The regular room prefabs share the same shell and doorway positions.
            Vector2 gridSpacing = new(
                startRoom.DoorwayCenter(1).x - startRoom.DoorwayCenter(3).x + eastWestPassageLength,
                startRoom.DoorwayCenter(0).z - startRoom.DoorwayCenter(2).z + northSouthPassageLength);
            Vector3 origin = transform.position + Vector3.right * ((Floor - 1) * FloorSpacing);
            try
            {
                foreach (var node in Layout.Rooms)
                {
                    PalaceDungeonRoom prefab = node.Type switch
                    {
                        PalaceRoomType.Combat => combatRooms[node.Template],
                        PalaceRoomType.Recovery => recoveryRoom,
                        PalaceRoomType.Merchant => merchantRoom,
                        _ => startRoom
                    };
                    Vector3 position = origin + new Vector3(node.Grid.x * gridSpacing.x, 0f, node.Grid.y * gridSpacing.y);
                    AddRoom(prefab, node, position);
                }

                foreach (var room in rooms)
                    foreach (var neighbor in room.Layout.Neighbors)
                        if (neighbor.Id > room.Layout.Id) Connect(room, rooms[neighbor.Id]);

                portalRoom = rooms[Layout.Portal.Id];
                CreatePortal();
            }
            catch (Exception)
            {
                Debug.LogError($"Palace floor {Floor}, seed {Seed}: created {rooms.Count}/{Layout.Rooms.Count} rooms " +
                    $"before generation failed. Planned: {Layout.RoomCounts()}.", this);
                throw;
            }

            Debug.Log($"Palace floor {Floor}, seed {Seed}: {rooms.Count} rooms. {Layout.RoomCounts()}.", this);
            Bounds envelope = new(rooms[0].transform.position, Vector3.zero);
            foreach (var room in rooms) envelope.Encapsulate(room.transform.position);
            envelope.Expand(new Vector3(gridSpacing.x, 0f, gridSpacing.y));
            PlacePlayer(rooms[0], rooms[0].transform.position, envelope);
        }

        void GenerateBossFloor()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RememberBossForTesting();
#endif
            Layout = PalaceDungeonGraph.BossArena();
            CreateFloorRoot();
            float northDoorOffset = startRoom.DoorwayCenter(0).z - startRoom.transform.position.z;
            Vector3 approach = bossRoom.transform.position;
            approach.z = bossRoom.DoorwayCenter(2).z - northDoorOffset - northSouthPassageLength;
            PalaceDungeonRoom preparationRoom = AddRoom(startRoom, Layout.Start, approach);
            bossRoom.Initialize(Layout.Boss, health, recoveryAmount, encounterWaves, Seed);
            bossRoom.gameObject.SetActive(true);
            rooms.Add(bossRoom);
            Connect(preparationRoom, bossRoom);

            Bounds envelope = preparationRoom.EntryBounds;
            envelope.Encapsulate(bossRoom.EntryBounds);
            PlacePlayer(preparationRoom, approach, envelope);
        }

        void CreateFloorRoot()
        {
            floorRoot = new GameObject($"Palace Floor {Floor}").transform;
            floorRoot.SetParent(transform, false);
        }

        PalaceDungeonRoom AddRoom(PalaceDungeonRoom prefab, PalaceDungeonGraph.Room node, Vector3 position)
        {
            PalaceDungeonRoom room = Instantiate(prefab, position, Quaternion.identity, floorRoot);
            room.name = $"{node.Id:00} {node.Type} ({node.Grid.x}, {node.Grid.y})";
            rooms.Add(room);
            room.Initialize(node, health, recoveryAmount, encounterWaves, Seed, rewardChestPrefab);
            return room;
        }

        void PlacePlayer(PalaceDungeonRoom room, Vector3 position, Bounds bounds)
        {
            player.PlaceInDungeon(position, Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z));
            Physics.SyncTransforms();
            Enter(room, true);
            minimap.Initialize(this, health);
        }

        void CreatePortal()
        {
            var interaction = new GameObject("Floor portal interaction", typeof(SphereCollider));
            interaction.transform.SetParent(portalRoom.transform, false);
            portalRange = interaction.GetComponent<SphereCollider>();
            portalRange.isTrigger = true;
            portalRange.center = Vector3.up * .55f;
            portalRange.radius = 1.35f;
            if (floorPortalEffect != null)
                portalVisual = Instantiate(floorPortalEffect, interaction.transform);
        }

        bool CanUsePortal()
        {
            if (!isActiveAndEnabled || portalRange == null || Current != portalRoom || Current.InCombat ||
                player == null || !player.isActiveAndEnabled || health.IsDead || StorybookTransition.IsBusy)
                return false;

            Vector3 playerCenter = player.transform.position + Vector3.up * .55f;
            return (portalRange.ClosestPoint(playerCenter) - playerCenter).sqrMagnitude < .0001f;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            if (Keyboard.current?.fKey.wasPressedThisFrame != true || NpcDialogue.BlocksInteraction) return;
            if (CanUsePortal())
                StorybookTransition.TryBeginFloor(player, IntensifyPortal, AdvanceFloor);
            else
                TryInteractWithChest();
        }

        bool CanUseChest()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return false;
#endif
            return Current?.RewardChest != null && player != null && player.isActiveAndEnabled &&
                !health.IsDead && !StorybookTransition.IsBusy && !NpcDialogue.BlocksInteraction;
        }

        internal bool TryInteractWithChest()
        {
            if (!CanUseChest()) return false;
            var chest = Current.RewardChest;
            Vector3 position = player.transform.position;
            if (chest.TryCollect(position, out var collected))
            {
                ShowChestReward(chestRewards.GrantLoot(collected, health, player));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Palace chest] collected {chestResult}", this);
#endif
                return true;
            }
            if (!chest.CanOpen(position) || !Current.TryRewardDropPosition(position, out var drop)) return false;
            var reward = chestRewards.Roll(chestRandom);
            chest.BeginOpening(chestRewards.CreateLoot(reward, health, chestRandom), drop);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Palace chest] {chestRewards.DescribeOdds()}, rolled {reward}", this);
#endif
            return true;
        }

        void ShowChestReward(string result)
        {
            chestResult = result;
            chestResultUntil = Time.unscaledTimeAsDouble + 2.4d;
        }

        void IntensifyPortal()
        {
            if (portalVisual == null || !portalVisual.activeInHierarchy) return;
            portalVisual.transform.localScale *= 1.12f;
            foreach (var particles in portalVisual.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.simulationSpeed *= 1.6f;
            }
        }

        void AdvanceFloor() => ReplaceFloor(Floor + 1);

        void ReplaceFloor(int floor, float? recoveryChanceOverride = null, bool requireMerchant = false)
        {
            Current = null;
            portalRoom = null;
            portalRange = null;
            portalVisual = null;
            floorRoot.gameObject.SetActive(false);
            Destroy(floorRoot.gameObject);
            floorRoot = null;
            rooms.Clear();
            Floor = floor;
            GenerateFloor(recoveryChanceOverride, requireMerchant);
        }

        void Connect(PalaceDungeonRoom a, PalaceDungeonRoom b)
        {
            Vector2Int direction = b.Layout.Grid - a.Layout.Grid;
            int side = Array.IndexOf(Directions, direction);
            Vector3 first = a.DoorwayCenter(side);
            Vector3 last = b.DoorwayCenter((side + 2) % Directions.Length);
            var link = Instantiate(passage, (first + last) * .5f, Quaternion.LookRotation(last - first), floorRoot);
            link.name = $"Passage {a.Layout.Id:00}–{b.Layout.Id:00}";
            link.transform.localScale = new Vector3(1f, 1f, Vector3.Distance(first, last) + .12f);
        }

        void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            if (Current == null || player == null) return;
            Current.RefreshClear();
            if (Current.InCombat || !player.isActiveAndEnabled) return;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Current != room) RemoveTestMonster();
#endif
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
                    if (neighbor.Id > room.Id && room.Type != PalaceRoomType.Merchant && neighbor.Type != PalaceRoomType.Merchant)
                    {
                        string a = $"{room.Grid}/{room.Type}";
                        string b = $"{neighbor.Grid}/{neighbor.Type}";
                        nodes.Add(string.CompareOrdinal(a, b) < 0 ? a + b : b + a);
                    }
            nodes.Sort(StringComparer.Ordinal);
            return string.Join("|", nodes);
        }
    }
}
