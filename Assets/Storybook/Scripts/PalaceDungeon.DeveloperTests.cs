#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace Storybook
{
    internal enum TestMonsterKind { Crab, Archer, Turtle, Pufferfish }

    public sealed partial class PalaceDungeon
    {
        internal bool TestRewardChest() => Current != null && Current.EnsureRewardChest();

        internal string TestChestReward(PalaceChestReward reward) =>
            health == null || health.IsDead ? "살아 있는 플레이어가 필요합니다." :
            chestRewards.Grant(reward, health, player, new System.Random());

        internal string TestChestOdds()
        {
            const int samples = 10000;
            var random = new System.Random();
            var results = new int[4];
            for (int i = 0; i < samples; i++) results[(int)chestRewards.Roll(random)]++;
            return $"{chestRewards.DescribeOdds()} | {samples} rolls: {string.Join("/", results)}";
        }

        PalaceDungeonRoom testBossTemplate;
        GameObject testMonsterRoot;
        MonsterHealth testMonster;
        TestMonsterKind testMonsterKind;

        internal bool CanSpawnTestMonster => !IsBossFloor &&
            (Current?.Layout.Type is PalaceRoomType.Start or PalaceRoomType.Combat);

        internal bool TestFloor(int floor)
        {
            if (floor < 1 || floor > LastRegularFloor + 1) throw new ArgumentOutOfRangeException(nameof(floor));
            return BeginTestTravel(() => ReplaceTestFloor(floor));
        }

        internal bool TestRoom(PalaceRoomType type) => BeginTestTravel(() =>
        {
            int destinationFloor = type == PalaceRoomType.Boss ? LastRegularFloor + 1 : IsBossFloor ? 1 : Floor;
            PalaceDungeonRoom destination = destinationFloor == Floor ? FindTestRoom(type) : null;
            // Abandoning an active encounter must also clear its waves, gates and effects.
            if (destination == null || Current.InCombat && destination != Current)
            {
                ReplaceTestFloor(destinationFloor, type == PalaceRoomType.Recovery ? 1f : null,
                    requireMerchant: type == PalaceRoomType.Merchant);
                destination = FindTestRoom(type);
            }
            if (destination == null) throw new InvalidOperationException($"Floor {Floor} has no {type} test room.");

            Vector3 position = TestArrival(destination);
            player.MoveToTestRoom(position);
            Physics.SyncTransforms();
            Enter(destination, true);
        });

        PalaceDungeonRoom FindTestRoom(PalaceRoomType type) =>
            rooms.Find(room => room.Layout.Type == type && !room.Visited) ??
            rooms.Find(room => room.Layout.Type == type);

        static Vector3 TestArrival(PalaceDungeonRoom room)
        {
            Vector3 center = room.EntryBounds.center;
            center.y = room.transform.position.y;
            if (room.Recovery != null)
                return room.Recovery.transform.position + Vector3.back * .95f;
            if (room.Layout.Type == PalaceRoomType.Merchant) return center;

            foreach (var neighbor in room.Layout.Neighbors)
            {
                int side = Array.IndexOf(Directions, neighbor.Grid - room.Layout.Grid);
                Vector3 entrance = room.EntryBounds.ClosestPoint(room.DoorwayCenter(side));
                entrance.y = center.y;
                return Vector3.MoveTowards(entrance, center, 1.1f);
            }

            return center;
        }

        bool BeginTestTravel(Action travel) => StorybookTransition.TryBeginFloor(player, () => { }, () =>
        {
            try { travel(); }
            catch (Exception error) { Debug.LogException(error, this); }
        });

        void ReplaceTestFloor(int floor, float? recoveryChanceOverride = null, bool requireMerchant = false)
        {
            RemoveTestMonster();
            if (IsBossFloor)
            {
                // This copy carries the scene's existing player references and pristine encounter state.
                var replacement = Instantiate(testBossTemplate, bossRoom.transform.position,
                    bossRoom.transform.rotation, transform);
                replacement.name = bossRoom.name;
                var encounter = replacement.GetComponentInChildren<DragonKingTridentSlam>(true);
                bossHud.BindTestArena(encounter);
                bossRoom.gameObject.SetActive(false);
                Destroy(bossRoom.gameObject);
                bossRoom = replacement;
            }
            ReplaceFloor(floor, recoveryChanceOverride, requireMerchant);
        }

        void RememberBossForTesting()
        {
            if (testBossTemplate != null) return;
            testBossTemplate = Instantiate(bossRoom, bossRoom.transform.position, bossRoom.transform.rotation, transform);
            testBossTemplate.name = "Inactive developer arena template";
        }

        internal bool SpawnTestMonster(TestMonsterKind kind)
        {
            if (!CanSpawnTestMonster)
                throw new InvalidOperationException("Choose a regular start or combat room before spawning an enemy.");
            if (testMonster != null && !testMonster.IsDead && testMonsterKind == kind) return false;
            var prefab = encounterWaves.GetTestPrefab(kind);
            Bounds bounds = Current.EntryBounds;
            if (!TryFindTestMonsterSite(prefab, bounds, out var site))
                throw new InvalidOperationException($"No free spawn point for {kind} inside {Current.name}.");

            RemoveTestMonster();
            testMonsterRoot = new GameObject("Developer test encounter");
            testMonsterRoot.transform.SetParent(Current.transform, false);
            testMonster = Instantiate(prefab, site + Vector3.up * .025f, Quaternion.identity, testMonsterRoot.transform);
            testMonsterKind = kind;
            testMonster.SetFragmentRecipient(health);
            testMonster.Defeated += RecordTestMonsterKill;
            testMonster.GetComponent<MonsterCombatAI>().ActivateInRoom(bounds);
            return true;
        }

        bool TryFindTestMonsterSite(MonsterHealth prefab, Bounds bounds, out Vector3 site)
        {
            Physics.SyncTransforms();
            Vector3 center = bounds.center;
            center.y = Current.transform.position.y;
            // Prefer sites near the center so the encounter is visible immediately.
            for (int ring = 0; ring <= 3; ring++)
            for (int direction = 0; direction < 8; direction++)
            {
                float angle = direction * Mathf.PI / 4f;
                site = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * .85f);
                if (Mathf.Abs(site.x - center.x) > bounds.extents.x - .55f ||
                    Mathf.Abs(site.z - center.z) > bounds.extents.z - .55f) continue;
                if (PalaceRoomWaves.IsSpawnPointFree(prefab, site, health)) return true;
            }
            site = default;
            return false;
        }

        void RecordTestMonsterKill(MonsterDeathCause cause)
        {
            if (cause == MonsterDeathCause.PlayerKilled) health.RecordMonsterKill();
        }

        internal void RemoveTestMonster()
        {
            if (testMonsterRoot == null) return;
            if (testMonster != null) testMonster.Defeated -= RecordTestMonsterKill;
            testMonsterRoot.SetActive(false);
            Destroy(testMonsterRoot);
            testMonsterRoot = null;
            testMonster = null;
        }
    }
}
#endif
