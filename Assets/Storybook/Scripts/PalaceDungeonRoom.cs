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
        public bool InCombat => Visited && !Cleared && (waves != null || encounter != null);
        public bool RewardUsed => recovery != null && recovery.Used;
        public int LivingEnemies { get; private set; }
        public Bounds EntryBounds => entryArea.bounds;
        internal PalaceRecovery Recovery => recovery;
        public bool IsLinked(int side) => side >= 0 && side < doorways.Length && doorways[side] != null && doorways[side].activeSelf;
        public bool IsSealed(int side) => side >= 0 && side < gates.Length && gates[side] != null && gates[side].activeSelf;

        readonly List<MonsterHealth> enemies = new();
        PalaceRoomWaves waves;

        internal void Initialize(PalaceDungeonGraph.Room layout, PlayerHealth hero, float healingFraction,
            PalaceRoomWaves.Settings waveSettings, int seed)
        {
            Layout = layout;
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
            if (layout.Type == PalaceRoomType.Combat && waveSettings != null && waveSettings.IsConfigured)
                waves = new PalaceRoomWaves(this, hero, waveSettings, seed);
            if (waves == null && encounter != null)
                enemies.AddRange(encounter.GetComponentsInChildren<MonsterHealth>(true));
            LivingEnemies = enemies.Count;
            if (recovery != null) recovery.Initialize(this, hero, healingFraction);
        }

        internal Vector3 DoorwayCenter(int side) => doorways[side].transform.position;

        internal bool Contains(Vector3 position)
        {
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
            if (waves != null)
            {
                waves.Begin(enemies);
                LivingEnemies = enemies.Count;
                return;
            }

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
            if (!InCombat) return;
            LivingEnemies = 0;
            foreach (MonsterHealth enemy in enemies)
                if (enemy != null && !enemy.IsDead) LivingEnemies++;
            if (LivingEnemies > 0) return;

            if (waves != null && waves.HasPending)
            {
                int before = enemies.Count;
                waves.Tick(enemies);
                LivingEnemies = enemies.Count - before;
                if (waves.HasPending || LivingEnemies > 0) return;
            }

            Cleared = true;
            SetGates(false);
        }

        void SetGates(bool closed)
        {
            for (int side = 0; side < gates.Length; side++)
                if (IsLinked(side) && gates[side] != null) gates[side].SetActive(closed);
        }

        void OnDestroy()
        {
            waves?.Dispose();
            if (waves != null) return;
            foreach (MonsterHealth enemy in enemies)
                if (enemy != null) enemy.Died -= RefreshClear;
        }
    }
}
