using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Storybook
{
    internal sealed class PalaceRoomWaves
    {
        [Serializable]
        internal sealed class Settings
        {
            [SerializeField] MonsterHealth crab, archer, turtle, pufferfish;
            [SerializeField, Range(1, 2)] int maxPufferfishPerWave = 2;
            [SerializeField] Vector2Int small = new(4, 5), standard = new(6, 7), large = new(8, 9);
            [SerializeField, Min(.1f)] float warningSeconds = .65f;
            [SerializeField] Material warningMaterial;

            internal string ConfigurationError()
            {
                string error = PrefabError(crab, nameof(crab)) ?? PrefabError(archer, nameof(archer)) ??
                    PrefabError(turtle, nameof(turtle)) ?? PrefabError(pufferfish, nameof(pufferfish));
                if (error != null) return error;
                if (warningMaterial == null) return "missing wave warning material";
                if (!ValidRange(small) || !ValidRange(standard) || !ValidRange(large))
                    return $"invalid wave count ranges (small {small}, standard {standard}, large {large})";
                return null;
            }

            static bool ValidRange(Vector2Int range) => range.x >= 1 && range.y >= range.x;

            static string PrefabError(MonsterHealth prefab, string slot)
            {
                if (prefab == null) return $"missing {slot} prefab reference";
                if (!prefab.gameObject.activeSelf || !prefab.enabled)
                    return $"{slot} prefab '{prefab.name}' or its MonsterHealth is disabled";
                if (!prefab.TryGetComponent(out CharacterController body) || !body.enabled)
                    return $"{slot} prefab '{prefab.name}' requires an enabled CharacterController";
                if (!prefab.TryGetComponent<MonsterCombatAI>(out _))
                    return $"{slot} prefab '{prefab.name}' is missing MonsterCombatAI";
                return null;
            }

            internal Material WarningMaterial => warningMaterial;
            internal float WarningSeconds => Mathf.Max(.1f, warningSeconds);

            internal MonsterHealth[][] Plan(Random random, Bounds bounds)
            {
                Vector2Int range = random.Next(3) switch { 0 => small, 1 => standard, _ => large };
                int min = Mathf.Clamp(range.x, 1, 10);
                int max = Mathf.Clamp(range.y, min, 10);
                // Reserve roughly six square metres for each concurrent enemy.
                int capacity = Mathf.Clamp(Mathf.FloorToInt(bounds.size.x * bounds.size.z / 6f), 1, 5);
                int total = Mathf.Min(random.Next(min, max + 1), capacity * 2);
                int first = total <= capacity ? total : (total + 1) / 2;
                var waves = total == first ? new[] { new MonsterHealth[first] }
                    : new[] { new MonsterHealth[first], new MonsterHealth[total - first] };
                foreach (MonsterHealth[] wave in waves)
                {
                    bool hasArcher = false, hasTurtle = false;
                    int specialists = 0, pufferfishCount = 0;
                    for (int i = 0; i < wave.Length; i++)
                    {
                        wave[i] = crab;
                        // Keep at least half of each wave as regular melee enemies.
                        if (specialists >= wave.Length / 2) continue;
                        int roll = random.Next(100);
                        if (roll >= 40 && roll < 60 && pufferfish != null &&
                            pufferfishCount < Mathf.Clamp(maxPufferfishPerWave, 1, 2))
                        {
                            wave[i] = pufferfish;
                            pufferfishCount++;
                            specialists++;
                        }
                        else if (roll >= 60 && roll < 80 && !hasArcher)
                        {
                            wave[i] = archer;
                            hasArcher = true;
                            specialists++;
                        }
                        else if (roll >= 80 && !hasTurtle)
                        {
                            wave[i] = turtle;
                            hasTurtle = true;
                            specialists++;
                        }
                    }
                }
                return waves;
            }
        }

        const float Spacing = 1.10f;
        const float Inset = .55f;
        const float PlayerDistance = 1.65f;
        const float DoorDistance = 1.65f;
        const float RetryInterval = .3f;
        static readonly int Tint = Shader.PropertyToID("_Color");

        readonly PalaceDungeonRoom room;
        readonly PlayerHealth player;
        readonly Settings settings;
        readonly MonsterHealth[][] plan;
        readonly List<Vector3> sites = new();
        readonly List<(MonsterHealth prefab, Vector3 point)> reserved = new();
        readonly List<MeshRenderer> markers = new();
        readonly MaterialPropertyBlock properties = new();
        Mesh markerMesh;
        int nextWave;
        float spawnAt, retryAt;
        bool reportedBlocked;

        internal bool HasPending => nextWave < plan.Length;

        internal PalaceRoomWaves(PalaceDungeonRoom room, PlayerHealth player, Settings settings, int seed)
        {
            this.room = room;
            this.player = player;
            this.settings = settings;
            var random = new Random(unchecked(seed * 16777619 ^ room.Layout.Id * 486187739));
            Bounds bounds = room.EntryBounds;
            plan = settings.Plan(random, bounds);
            int across = Mathf.FloorToInt((bounds.size.x - 2 * Inset) / Spacing);
            int along = Mathf.FloorToInt((bounds.size.z - 2 * Inset) / Spacing);
            for (int x = 0; x <= across; x++)
            for (int z = 0; z <= along; z++)
            {
                Vector3 site = bounds.center + new Vector3((x - across * .5f) * Spacing, 0f, (z - along * .5f) * Spacing);
                site.y = room.transform.position.y;
                bool nearDoor = false;
                for (int side = 0; side < 4; side++)
                    if (room.IsLinked(side) && DistanceSquared(site, room.DoorwayCenter(side)) < DoorDistance * DoorDistance)
                        nearDoor = true;
                if (!nearDoor) sites.Add(site);
            }

            for (int i = sites.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (sites[i], sites[j]) = (sites[j], sites[i]);
            }
        }

        internal void Begin(List<MonsterHealth> enemies)
        {
            if (TryReserve()) Spawn(enemies);
            else RetryBlockedWave();
        }

        internal void Tick(List<MonsterHealth> enemies)
        {
            if (!HasPending || player == null || player.IsDead)
            {
                HideWarnings();
                return;
            }
            if (reserved.Count == 0)
            {
                if (Time.time < retryAt) return;
                if (!TryReserve())
                {
                    RetryBlockedWave();
                    return;
                }

                ShowWarnings();
                spawnAt = Time.time + settings.WarningSeconds;
                return;
            }

            TintWarnings(1f - (spawnAt - Time.time) / settings.WarningSeconds);
            if (Time.time < spawnAt) return;
            Physics.SyncTransforms();
            if (reserved.Exists(slot => !IsFree(slot.prefab, slot.point)))
            {
                RetryBlockedWave();
                return;
            }

            Spawn(enemies);
            HideWarnings();
        }

        bool TryReserve()
        {
            Physics.SyncTransforms();
            foreach (MonsterHealth prefab in plan[nextWave])
            {
                int index = sites.FindIndex(site =>
                    !reserved.Exists(slot => DistanceSquared(slot.point, site) < Spacing * Spacing - .0001f) &&
                    IsFree(prefab, site));
                if (index < 0) return false;
                reserved.Add((prefab, sites[index]));
            }
            return true;
        }

        void RetryBlockedWave()
        {
            if (!reportedBlocked)
            {
                Debug.LogError($"[PalaceRoomWaves] {room.transform.parent?.name}/{room.name}: wave " +
                    $"{nextWave + 1}/{plan.Length} could not reserve all {plan[nextWave].Length} spawn positions " +
                    $"from {sites.Count} candidates. Check room colliders and clearance around the player. " +
                    "The wave remains pending and will retry.", room);
                reportedBlocked = true;
            }
            HideWarnings();
            reserved.Clear();
            retryAt = Time.time + RetryInterval;
        }

        bool IsFree(MonsterHealth prefab, Vector3 ground)
        {
            if (DistanceSquared(ground, player.transform.position) < PlayerDistance * PlayerDistance) return false;
            var body = prefab.GetComponent<CharacterController>();
            float radius = body.radius + body.skinWidth + .04f;
            Vector3 lower = ground + Vector3.up * (radius + .035f);
            Vector3 upper = ground + Vector3.up * Mathf.Max(radius + .035f, body.height - radius + .035f);
            return !Physics.CheckCapsule(lower, upper, radius, ~0, QueryTriggerInteraction.Ignore);
        }

        void Spawn(List<MonsterHealth> enemies)
        {
            foreach (var (prefab, point) in reserved)
            {
                var monster = UnityEngine.Object.Instantiate(prefab, point + Vector3.up * .025f, Quaternion.identity, room.transform);
                enemies.Add(monster);
                monster.GetComponent<MonsterCombatAI>().ActivateInRoom(room.EntryBounds);
            }
            nextWave++;
            reportedBlocked = false;
            reserved.Clear();
        }

        void ShowWarnings()
        {
            if (markerMesh == null)
            {
                markerMesh = new Mesh { name = "Corruption spawn rune" };
                const int segments = 12;
                var points = new Vector3[segments * 2];
                var triangles = new List<int>(segments * 6);
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    var spoke = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int a = i * 2;
                    int b = (a + 2) % points.Length;
                    points[a] = spoke * .48f;
                    points[a + 1] = spoke * (i % 3 == 0 ? .25f : .39f);
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
                markerMesh.vertices = points;
                markerMesh.SetTriangles(triangles, 0);
            }

            foreach (var (_, point) in reserved)
            {
                var marker = new GameObject("Corruption spawn warning", typeof(MeshFilter), typeof(MeshRenderer));
                marker.transform.SetParent(room.transform, false);
                marker.transform.position = point + Vector3.up * .029f;
                marker.GetComponent<MeshFilter>().sharedMesh = markerMesh;
                var renderer = marker.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = settings.WarningMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = 1;
                markers.Add(renderer);
            }
            TintWarnings(0f);
        }

        void TintWarnings(float progress)
        {
            properties.SetColor(Tint, new Color(.95f, .085f, .18f, Mathf.Lerp(.48f, .95f, progress)));
            foreach (MeshRenderer marker in markers) marker.SetPropertyBlock(properties);
        }

        void HideWarnings()
        {
            foreach (MeshRenderer marker in markers)
            {
                marker.enabled = false;
                UnityEngine.Object.Destroy(marker.gameObject);
            }
            markers.Clear();
        }

        internal void Dispose()
        {
            if (markerMesh != null) UnityEngine.Object.Destroy(markerMesh);
        }

        static float DistanceSquared(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return x * x + z * z;
        }
    }
}
