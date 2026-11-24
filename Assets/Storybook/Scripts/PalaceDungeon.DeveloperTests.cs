#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
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

        internal string TestRelic(string relicId, bool spawn)
        {
            var relic = chestRewards.FindRelic(relicId);
            if (relic == null) return "보상 목록에 해당 유물이 없습니다.";
            if (health == null || health.IsDead) return "살아 있는 플레이어가 필요합니다.";
            if (health.Relics.Contains(relic.Id)) return $"이미 보유한 유물 · {relic.DisplayName}";
            if (health.Relics.IsFull) return FairyTaleRelicInventory.FullPrompt;
            if (!spawn) return chestRewards.GrantLoot(new(PalaceChestReward.Relic, relic: relic), health, player);
            return SpawnTestPickup(new(PalaceChestReward.Relic, relic: relic));
        }

        internal string TestBloodTemptationPoison()
        {
            if (chestRewards.FindRelic(BloodTemptationRelic.RelicId) is not BloodTemptationRelic relic ||
                !health.Relics.Contains(relic.Id)) return "핏빛 유혹을 먼저 획득하세요.";

            var target = testMonster;
            if (target == null || !target.CanTakeDamage)
            {
                if (Current == null) return "현재 방에 중독시킬 대상이 없습니다.";
                float closest = float.PositiveInfinity;
                foreach (var monster in Current.GetComponentsInChildren<MonsterHealth>())
                {
                    if (!monster.CanTakeDamage) continue;
                    float distance = (player.transform.position - monster.transform.position).sqrMagnitude;
                    if (distance >= closest) continue;
                    target = monster;
                    closest = distance;
                }
                if (closest == float.PositiveInfinity) return "현재 방에 중독시킬 대상이 없습니다.";
            }

            relic.ForcePoisonForTesting(health, target);
            return $"중독 적용 · 현재 HP {target.CurrentHP:0.##} · 틱당 {target.GetComponent<BloodTemptationPoison>().TickDamage:0.##} 피해";
        }

        internal string TestRewardPickup(PalaceChestReward reward)
            => SpawnTestPickup(chestRewards.CreateLoot(reward, health, new System.Random()));

        string SpawnTestPickup(PalaceChestLoot loot)
        {
            if (Current == null || rewardChestPrefab == null || health.IsDead) return "보상을 생성할 수 없습니다.";
            testPickups.RemoveAll(pickup => pickup == null);
            Vector3 center = player.transform.position;
            center.y = Current.transform.position.y;
            Vector3 direction = Quaternion.Euler(0f, testPickups.Count * 72f, 0f) * Vector3.back;
            if (!Current.TryRewardDropPosition(center, center + direction, out var position))
                return "보상을 놓을 수 있는 위치가 없습니다.";

            testPickups.Add(rewardChestPrefab.SpawnReward(Current.transform, position, loot));
            return "바닥 보상 생성 · " + loot.Prompt.Split('\n')[0];
        }

        PalaceRewardPickup FindTestPickup(Vector3 position)
        {
            PalaceRewardPickup nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var pickup in testPickups)
            {
                if (pickup == null || pickup.transform.parent != Current.transform || !pickup.CanCollect(position))
                    continue;
                float sqrDistance = (pickup.transform.position - position).sqrMagnitude;
                if (sqrDistance >= distance) continue;
                nearest = pickup;
                distance = sqrDistance;
            }
            return nearest;
        }

        internal string TestRelicLimit()
        {
            var inventory = new FairyTaleRelicInventory(health);
            var probes = new FairyTaleRelic[FairyTaleRelicInventory.Capacity + 1];
            try
            {
                bool passed = true;
                for (int i = 0; i < probes.Length; i++)
                {
                    probes[i] = FairyTaleRelic.CreateForTesting($"test-relic-{i}");
                    passed &= inventory.TryAcquire(probes[i]) == (i < FairyTaleRelicInventory.Capacity);
                    passed &= !inventory.TryAcquire(probes[i]);
                }
                passed &= inventory.SelectRandom(probes, new System.Random(0)) == null;
                return passed ? "유물 중복 차단 및 6개 한도 검증 통과" : "유물 보유 한도 검증 실패";
            }
            finally
            {
                foreach (var probe in probes)
                    if (probe != null) Destroy(probe);
            }
        }

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
        readonly List<PalaceRewardPickup> testPickups = new();
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
