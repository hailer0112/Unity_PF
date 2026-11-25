using System;
using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    // Owns the union of the player's trails and a single damage clock per monster.
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class CursedFootstepsTrail : MonoBehaviour
    {
        readonly List<CursedFootstepsSurface> trails = new();
        readonly List<MonsterHealth> monsters = new();
        readonly Dictionary<MonsterHealth, Exposure> exposures = new();
        readonly List<MonsterHealth> inactive = new();
        readonly Collider[] nearbyColliders = new Collider[24];
        readonly List<Collider> blockers = new();

        PlayerHealth owner;
        CursedFootstepsRelic relic;
        PalaceDungeon dungeon;
        PalaceDungeonRoom room, dashRoom;
        MonsterHealth boss;
        BoxCollider[] bossHurtboxes;
        CursedFootstepsSurface drawing;
        Color32[] puddle;
        int puddleWidth;
        internal bool HasTrails => trails.Count != 0;
        internal PalaceDungeonRoom DashRoom => dashRoom;

        sealed class Exposure
        {
            internal MonsterCombatAI AI;
            internal SpriteRenderer Motes;
            internal double NextTick;
            internal bool Inside;
        }

        internal void Initialize(PlayerHealth player, CursedFootstepsRelic data)
        {
            owner = player;
            relic = data;
            dungeon = FindFirstObjectByType<PalaceDungeon>();
            if (data.PuddleFrames == null) return;
            puddleWidth = data.PuddleFrames.width;
            puddle = data.PuddleFrames.GetPixels32();
        }

        internal void BeginDash()
        {
            if (room != null && room != dungeon?.Current) Clear();
            dashRoom = dungeon != null ? dungeon.Current : null;
        }

        internal void RecordStep(Vector3 from, Vector3 to)
        {
            if (relic == null || dungeon == null || owner.IsDead) return;
            var current = dungeon.Current;
            if (current == null || current != dashRoom) return;

            Vector2 start = new(from.x, from.z);
            Vector2 end = new(to.x, to.z);
            if ((end - start).sqrMagnitude <= .00000001f) return;
            room = current;
            double now = Time.timeAsDouble;
            if (drawing == null)
            {
                if (!room.Contains(from)) return;
                drawing = new CursedFootstepsSurface(room, relic, puddle, puddleWidth, now);
                trails.Add(drawing);
            }
            if (now >= drawing.ExpiresAt) return;

            CollectBlockers(start, end);
            drawing.AddStep(start, end, IsUnobstructed);
            blockers.Clear();

            // Resolve contacts before another actor can move off the newly drawn pixels.
            UpdateMonsters(now, drawing);
        }

        void CollectBlockers(Vector2 start, Vector2 end)
        {
            Vector2 midpoint = (start + end) * .5f;
            Vector3 origin = new(midpoint.x, room.transform.position.y + .25f, midpoint.y);
            float radius = relic.Radius;
            Vector3 extents = new(Mathf.Abs(end.x - start.x) * .5f + radius, .02f,
                Mathf.Abs(end.y - start.y) * .5f + radius);
            var colliders = nearbyColliders;
            int count = Physics.OverlapBoxNonAlloc(origin, extents, colliders, Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            if (count == colliders.Length)
            {
                colliders = Physics.OverlapBox(origin, extents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                count = colliders.Length;
            }
            for (int index = 0; index < count; index++)
            {
                var collider = colliders[index];
                if (collider.GetComponentInParent<PlayerHealth>() == null &&
                    collider.GetComponentInParent<MonsterHealth>() == null) blockers.Add(collider);
            }
        }

        bool IsUnobstructed(Vector2 start, Vector2 end)
        {
            if (blockers.Count == 0) return true;
            Vector3 offset = new(end.x - start.x, 0f, end.y - start.y);
            float distance = offset.magnitude;
            if (distance <= .0001f) return true;
            var origin = new Vector3(start.x, room.transform.position.y + .25f, start.y);
            var ray = new Ray(origin, offset / distance);
            foreach (var blocker in blockers)
                if (blocker.Raycast(ray, out _, distance)) return false;
            return true;
        }

        internal void FinishDash()
        {
            dashRoom = null;
            drawing = null;
        }

        void LateUpdate()
        {
            if (room == null || dungeon == null || dungeon.Current != room || owner == null || owner.IsDead)
            {
                if (HasTrails) Clear();
                return;
            }
            if (Time.timeScale == 0f || (!HasTrails && exposures.Count == 0)) return;

            double now = Time.timeAsDouble;
            if (HasTrails) UpdateMonsters(now);
            if (room == null) return;

            inactive.Clear();
            foreach (var entry in exposures)
                if (entry.Key == null || !entry.Key.CanTakeDamage || !entry.Key.transform.IsChildOf(room.transform) ||
                    (!entry.Value.Inside && now >= entry.Value.NextTick))
                    inactive.Add(entry.Key);
            foreach (var monster in inactive)
            {
                SetInside(exposures[monster], false);
                exposures.Remove(monster);
            }
            inactive.Clear();

            for (int index = trails.Count - 1; index >= 0; index--)
            {
                var trail = trails[index];
                if (now >= trail.ExpiresAt)
                {
                    trail.Dispose();
                    trails.RemoveAt(index);
                }
                else trail.Draw(now);
            }
        }

        void UpdateMonsters(double now, CursedFootstepsSurface newSurface = null)
        {
            var activeRoom = room;
            activeRoom.GetComponentsInChildren(false, monsters);
            foreach (var monster in monsters)
            {
                // Defeating the Dragon King can disable the player and clear the trails synchronously.
                if (room != activeRoom) break;
                if (monster == null || !monster.CanTakeDamage) continue;

                Vector3 position = monster.transform.position;
                float footprintRadius = 0f;
                if (monster.TryGetComponent<CharacterController>(out var body) && body.enabled)
                {
                    position = body.transform.TransformPoint(body.center);
                    Vector3 scale = body.transform.lossyScale;
                    footprintRadius = body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                }

                if (newSurface != null)
                {
                    if (Covers(newSurface)) UpdateExposure(monster, now, true);
                    continue;
                }

                bool inside = false;
                foreach (var trail in trails)
                {
                    if (now >= trail.ExpiresAt || !Covers(trail)) continue;
                    inside = true;
                    break;
                }
                UpdateExposure(monster, now, inside);

                bool Covers(CursedFootstepsSurface trail) =>
                    trail.Covers(position, footprintRadius) || CoversBoss(trail, monster);
            }
            monsters.Clear();
        }

        bool CoversBoss(CursedFootstepsSurface trail, MonsterHealth monster)
        {
            if (boss != monster)
            {
                if (!monster.TryGetComponent<DragonKingPhase>(out _)) return false;
                boss = monster;
                var hurtboxes = boss.GetComponentsInChildren<DragonKingHurtbox>();
                bossHurtboxes = Array.ConvertAll(hurtboxes, hurtbox => hurtbox.GetComponent<BoxCollider>());
            }
            foreach (var hurtbox in bossHurtboxes)
                if (hurtbox != null && hurtbox.enabled && trail.Covers(hurtbox)) return true;
            return false;
        }

        void UpdateExposure(MonsterHealth monster, double now, bool inside)
        {
            if (!exposures.TryGetValue(monster, out var exposure))
            {
                if (!inside) return;
                monster.TryGetComponent(out MonsterCombatAI ai);
                exposure = new Exposure { AI = ai, NextTick = now };
                exposures.Add(monster, exposure);
            }

            if (inside && now >= exposure.NextTick)
            {
                // Measure the interval from the actual hit, including after a delayed frame.
                exposure.NextTick = now + relic.TickInterval;
                monster.ReceiveDamage(relic.TickDamage);
                if (room == null) return;
            }

            SetInside(exposure, inside && monster.CanTakeDamage);
            if (exposure.Motes != null)
            {
                float phase = Mathf.Repeat((float)(now * 1.45d), 1f);
                Vector3 ground = monster.transform.position;
                ground.y = room.transform.position.y + .08f + phase * .13f;
                exposure.Motes.transform.position = ground;
                exposure.Motes.color = new Color(1f, 1f, 1f, relic.MoteOpacity * Mathf.Sin(phase * Mathf.PI));
            }
        }

        void SetInside(Exposure exposure, bool inside)
        {
            if (exposure.Inside == inside) return;
            exposure.Inside = inside;
            if (exposure.AI != null)
                exposure.AI.SetCursedFootstepsSlow(inside ? relic.SlowFraction : 0f);
            if (inside && exposure.AI != null && relic.Motes != null && relic.MoteSize > 0f)
                exposure.Motes = CursedFootstepsSurface.CreateMotes(exposure.AI.transform, relic, "Curse slow motes");
            else if (exposure.Motes != null)
            {
                exposure.Motes.enabled = false;
                Destroy(exposure.Motes.gameObject);
                exposure.Motes = null;
            }
        }

        internal void Clear()
        {
            foreach (var exposure in exposures.Values) SetInside(exposure, false);
            exposures.Clear();
            foreach (var trail in trails) trail.Dispose();
            trails.Clear();
            drawing = null;
            room = dashRoom = null;
            boss = null;
            bossHurtboxes = null;
            blockers.Clear();
            Array.Clear(nearbyColliders, 0, nearbyColliders.Length);
        }

        void OnDisable() => Clear();
    }
}
