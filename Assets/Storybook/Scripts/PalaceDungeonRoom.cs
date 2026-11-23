using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    public sealed class PalaceDungeonRoom : MonoBehaviour
    {
        [SerializeField] BoxCollider entryArea;
        [SerializeField] GameObject encounter;
        [SerializeField] PalaceRecovery recovery;
        [SerializeField] GameObject[] solidEdges = new GameObject[4];
        [SerializeField] GameObject[] doorways = new GameObject[4];
        [SerializeField] GameObject[] gates = new GameObject[4];

        public PalaceDungeonGraph.Room Layout { get; private set; }
        public bool Visited { get; private set; }
        public bool Cleared { get; private set; }
        public bool InCombat => Visited && !Cleared && (Layout?.Type == PalaceRoomType.Combat || encounter != null);
        public bool RewardUsed => recovery != null && recovery.Used;
        public int LivingEnemies { get; private set; }
        public Bounds EntryBounds => entryArea != null ? entryArea.bounds : default;
        internal PalaceRecovery Recovery => recovery;
        internal PalaceRewardChest RewardChest { get; private set; }
        public bool IsLinked(int side) => side >= 0 && side < doorways.Length && doorways[side] != null && doorways[side].activeSelf;
        public bool IsSealed(int side) => side >= 0 && side < gates.Length && gates[side] != null && gates[side].activeSelf;

        readonly List<MonsterHealth> enemies = new();
        PalaceRoomWaves waves;
        PlayerHealth player;
        PalaceRewardChest rewardChestPrefab;

        internal void Initialize(PalaceDungeonGraph.Room layout, PlayerHealth hero, float healingFraction,
            PalaceRoomWaves.Settings waveSettings, int seed, PalaceRewardChest chestPrefab = null)
        {
            Layout = layout;
            player = hero;
            rewardChestPrefab = chestPrefab;
            for (int side = 0; side < gates.Length; side++)
            {
                Vector2Int direction = PalaceDungeon.Direction(side);
                bool connected = false;
                foreach (var neighbor in layout.Neighbors)
                    connected |= neighbor.Grid == layout.Grid + direction;
                if (solidEdges[side] != null) solidEdges[side].SetActive(!connected);
                if (doorways[side] != null) doorways[side].SetActive(connected);
                if (gates[side] != null) gates[side].SetActive(false);
            }

            if (encounter != null) encounter.SetActive(false);
            if (layout.Type == PalaceRoomType.Combat)
            {
                string error = hero == null ? "missing PlayerHealth" :
                    entryArea == null || !entryArea.enabled ? "missing or disabled entry area collider" :
                    waveSettings == null ? "missing wave settings" : waveSettings.ConfigurationError();
                if (error == null)
                    waves = new PalaceRoomWaves(this, hero, waveSettings, seed);
                else
                    Debug.LogError($"[PalaceDungeonRoom] {name} (seed {seed}): {error}. Combat cannot start.", this);
            }
            else if (encounter != null)
                enemies.AddRange(encounter.GetComponentsInChildren<MonsterHealth>(true));
            LivingEnemies = enemies.Count;
            if (recovery != null) recovery.Initialize(this, hero, healingFraction);
        }

        internal Vector3 DoorwayCenter(int side) => doorways[side].transform.position;

        internal bool EnsureRewardChest()
        {
            if (Layout?.Type != PalaceRoomType.Combat || !Cleared || rewardChestPrefab == null ||
                player == null || player.IsDead) return false;
            if (RewardChest != null) return true;
            if (!TryChestPosition(out Vector3 position)) return false;
            RewardChest = Instantiate(rewardChestPrefab, position, rewardChestPrefab.transform.rotation, transform);
            return true;
        }

        bool TryChestPosition(out Vector3 position)
        {
            Physics.SyncTransforms();
            Bounds bounds = EntryBounds;
            Vector3 center = bounds.center;
            center.y = transform.position.y;
            if (ChestPositionFree(center, bounds))
            {
                position = center;
                return true;
            }

            const float step = .5f;
            float limit = Mathf.Max(bounds.extents.x, bounds.extents.z);
            for (float distance = step; distance <= limit; distance += step)
            for (int side = 0; side < 8; side++)
            {
                float angle = side * Mathf.PI / 4f;
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!ChestPositionFree(candidate, bounds)) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        internal bool TryRewardDropPosition(Vector3 approach, out Vector3 position)
        {
            Vector3 center = RewardChest.transform.position;
            Vector3 direction = Vector3.ProjectOnPlane(approach - center, Vector3.up).normalized;
            if (direction == Vector3.zero) direction = Vector3.back;
            for (float distance = .75f; distance <= 2.25f; distance += .5f)
            for (int side = 0; side < 8; side++)
            {
                Vector3 candidate = center + Quaternion.Euler(0f, side * 45f, 0f) * direction * distance;
                if (!ChestPositionFree(candidate, EntryBounds, .22f, true)) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        static bool ChestPositionFree(Vector3 point, Bounds bounds, float clearance = .45f, bool ignorePlayer = false)
        {
            if (point.x - clearance < bounds.min.x || point.x + clearance > bounds.max.x ||
                point.z - clearance < bounds.min.z || point.z + clearance > bounds.max.z)
                return false;
            foreach (var collider in Physics.OverlapBox(point + Vector3.up * .32f,
                new Vector3(clearance, .29f, clearance), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if ((!ignorePlayer || collider.GetComponentInParent<PlayerHealth>() == null) &&
                    collider.GetComponentInParent<MonsterHealth>() == null) return false;
            return true;
        }

        internal bool Contains(Vector3 position)
        {
            if (entryArea == null || !entryArea.enabled) return false;
            Bounds bounds = EntryBounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x &&
                   position.z >= bounds.min.z && position.z <= bounds.max.z;
        }

        internal void Enter()
        {
            if (Visited) return;
            Visited = true;
            if (!InCombat)
            {
                Cleared = true;
                return;
            }

            SetGates(true);
            if (Layout.Type == PalaceRoomType.Combat)
            {
                if (waves == null) return;
                waves.Begin(enemies);
                TrackCombatKills(0);
                LivingEnemies = enemies.Count;
                return;
            }

            TrackCombatKills(0);
            encounter.SetActive(true);
            foreach (MonsterHealth enemy in enemies)
            {
                enemy.Died += RefreshClear;
                if (enemy.TryGetComponent(out MonsterCombatAI ai)) ai.ActivateInRoom(EntryBounds);
            }
            RefreshClear();
        }

        internal void RefreshClear()
        {
            if (!InCombat || (Layout.Type == PalaceRoomType.Combat && waves == null)) return;
            LivingEnemies = 0;
            foreach (MonsterHealth enemy in enemies)
                if (enemy != null && !enemy.IsDead) LivingEnemies++;
            if (LivingEnemies > 0) return;

            if (waves != null && waves.HasPending)
            {
                int before = enemies.Count;
                waves.Tick(enemies);
                TrackCombatKills(before);
                LivingEnemies = enemies.Count - before;
                if (waves.HasPending || LivingEnemies > 0) return;
            }

            Cleared = true;
            SetGates(false);
            if (Layout.Type == PalaceRoomType.Combat && player != null && !player.IsDead && !EnsureRewardChest())
                Debug.LogWarning($"[PalaceDungeonRoom] {name}: reward chest prefab or free placement is unavailable.", this);
        }

        void TrackCombatKills(int first)
        {
            if (Layout.Type != PalaceRoomType.Combat) return;
            for (int i = first; i < enemies.Count; i++)
            {
                enemies[i].SetFragmentRecipient(player);
                enemies[i].Defeated += OnCombatMonsterDefeated;
            }
        }

        void OnCombatMonsterDefeated(MonsterDeathCause cause)
        {
            if (cause == MonsterDeathCause.PlayerKilled && player != null) player.RecordMonsterKill();
        }

        void SetGates(bool closed)
        {
            for (int side = 0; side < gates.Length; side++)
                if (IsLinked(side) && gates[side] != null) gates[side].SetActive(closed);
        }

        void OnDestroy()
        {
            waves?.Dispose();
            foreach (MonsterHealth enemy in enemies)
            {
                if (enemy == null) continue;
                enemy.Defeated -= OnCombatMonsterDefeated;
                if (waves == null) enemy.Died -= RefreshClear;
            }
        }
    }
}
