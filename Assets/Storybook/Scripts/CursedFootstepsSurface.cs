using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Storybook
{
    // A ground pixel is both a rendered cell and a point in the damage/slow area.
    internal sealed class CursedFootstepsSurface : IDisposable
    {
        internal const int PixelsPerUnit = 50;
        internal const int FrameSize = 64;
        const float FadeSeconds = .24f;

        readonly PalaceDungeonRoom room;
        readonly CursedFootstepsRelic relic;
        readonly Color32[] puddle;
        readonly int puddleWidth;
        readonly double startedAt;
        readonly Dictionary<Vector2Int, Cell> cells = new();
        readonly SpriteRenderer renderer;
        readonly SpriteRenderer mist;
        readonly List<Footprint> footprints = new();
        Texture2D texture;
        Sprite sprite;
        Color32[] pixels;
        Vector2Int min = new(int.MaxValue, int.MaxValue);
        Vector2Int max = new(int.MinValue, int.MinValue);
        int drawnFrame = -1;
        float distanceToNextFootprint;
        bool dirty;

        internal double ExpiresAt { get; }

        readonly struct Cell
        {
            internal readonly Vector2 Relative;
            internal readonly Vector2 Direction;
            internal readonly float Distance, Edge;
            internal Cell(Vector2 relative, Vector2 direction, float distance, float edge)
            {
                Relative = relative;
                Direction = direction;
                Distance = distance;
                Edge = edge;
            }
        }

        readonly struct Footprint
        {
            internal readonly Vector2 Center, Direction;
            internal Footprint(Vector2 center, Vector2 direction)
            {
                Center = center;
                Direction = direction;
            }
        }

        internal CursedFootstepsSurface(PalaceDungeonRoom room, CursedFootstepsRelic relic, Color32[] puddle,
            int puddleWidth, double time)
        {
            this.room = room;
            this.relic = relic;
            this.puddle = puddle;
            this.puddleWidth = puddleWidth;
            startedAt = time;
            ExpiresAt = time + relic.Duration;
            distanceToNextFootprint = relic.FootprintSpacing * .4f;

            var surface = new GameObject("Cursed Footsteps ground");
            surface.transform.SetParent(room.transform);
            surface.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            renderer = surface.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            if (relic.Motes != null && relic.MoteSize > 0f)
                mist = CreateMotes(room.transform, relic, "Curse ground motes");
        }

        internal void AddStep(Vector2 start, Vector2 end, Func<Vector2, Vector2, bool> unobstructed)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            Vector2 direction = delta / length;
            Vector2 side = new(-direction.y, direction.x);
            float radius = relic.Radius;
            Bounds bounds = room.EntryBounds;
            var padding = Vector2.one * radius;
            Vector2Int first = ToCell(Vector2.Min(start, end) - padding);
            Vector2Int last = ToCell(Vector2.Max(start, end) + padding);
            for (int x = first.x; x <= last.x; x++)
            for (int y = first.y; y <= last.y; y++)
            {
                var key = new Vector2Int(x, y);
                Vector2 center = CenterOf(key);
                float along = Mathf.Clamp(Vector2.Dot(center - start, direction), 0f, length);
                Vector2 nearest = start + direction * along;
                Vector2 relative = center - nearest;
                float distance = relative.sqrMagnitude;
                if (distance > radius * radius || cells.TryGetValue(key, out var existing) && existing.Distance <= distance)
                    continue;
                // Scallop the contour inside the configured maximum radius.
                float contour = Mathf.PerlinNoise(center.x * 7.2f + 31f, center.y * 7.2f + 17f);
                float edge = radius - Mathf.Min(.085f, radius * .22f) * Mathf.SmoothStep(0f, 1f, (contour - .27f) * 2.3f);
                if (distance > edge * edge || center.x < bounds.min.x || center.x > bounds.max.x ||
                    center.y < bounds.min.z || center.y > bounds.max.z || !unobstructed(nearest, center)) continue;
                cells[key] = new Cell(relative, direction, distance, edge);
                min = Vector2Int.Min(min, key);
                max = Vector2Int.Max(max, key);
                dirty = true;
            }

            float spacing = relic.FootprintSpacing;
            for (; distanceToNextFootprint <= length; distanceToNextFootprint += spacing)
            {
                float lateral = radius * (footprints.Count % 2 == 0 ? -.34f : .34f);
                footprints.Add(new Footprint(start + direction * distanceToNextFootprint + side * lateral, direction));
                dirty = true;
            }
            distanceToNextFootprint -= length;
        }

        internal bool Covers(Vector3 position, float footprintRadius = 0f)
        {
            var feet = new Vector2(position.x, position.z);
            if (cells.ContainsKey(ToCell(feet))) return true;
            if (footprintRadius <= 0f) return false;

            Vector2 radius = Vector2.one * footprintRadius;
            Vector2Int first = Vector2Int.Max(min, ToCell(feet - radius));
            Vector2Int last = Vector2Int.Min(max, ToCell(feet + radius));
            float radiusSquared = footprintRadius * footprintRadius;
            const float halfPixel = .5f / PixelsPerUnit;
            for (int x = first.x; x <= last.x; x++)
            for (int y = first.y; y <= last.y; y++)
            {
                var key = new Vector2Int(x, y);
                if (!cells.ContainsKey(key)) continue;
                Vector2 offset = CenterOf(key) - feet;
                float dx = Mathf.Max(0f, Mathf.Abs(offset.x) - halfPixel);
                float dy = Mathf.Max(0f, Mathf.Abs(offset.y) - halfPixel);
                if (dx * dx + dy * dy <= radiusSquared) return true;
            }
            return false;
        }

        internal bool Covers(BoxCollider hurtbox)
        {
            Bounds bounds = hurtbox.bounds;
            Vector2Int first = Vector2Int.Max(min, ToCell(new Vector2(bounds.min.x, bounds.min.z)));
            Vector2Int last = Vector2Int.Min(max, ToCell(new Vector2(bounds.max.x, bounds.max.z)));
            for (int x = first.x; x <= last.x; x++)
            for (int y = first.y; y <= last.y; y++)
            {
                var key = new Vector2Int(x, y);
                if (!cells.ContainsKey(key)) continue;
                Vector2 center = CenterOf(key);
                var ray = new Ray(new Vector3(center.x, bounds.max.y + .05f, center.y), Vector3.down);
                if (hurtbox.Raycast(ray, out _, bounds.size.y + .1f)) return true;
            }
            return false;
        }

        internal void Draw(double now)
        {
            if (cells.Count == 0) return;
            int frameCount = Mathf.Max(1, puddleWidth / FrameSize);
            int frame = (int)((now - startedAt) * relic.AnimationFps) % frameCount;
            if (dirty || drawnFrame != frame)
            {
                Paint(frame);
                dirty = false;
                drawnFrame = frame;
            }

            float alpha = Mathf.Clamp01((float)((ExpiresAt - now) / FadeSeconds));
            renderer.color = new Color(1f, 1f, 1f, alpha);
            if (mist == null) return;
            float phase = Mathf.Repeat((float)((now - startedAt) * 1.25d), 1f);
            mist.transform.position = renderer.transform.position + Vector3.up * (.05f + phase * .12f);
            mist.color = new Color(1f, 1f, 1f, alpha * relic.MoteOpacity * Mathf.Sin(phase * Mathf.PI));
        }

        void Paint(int frame)
        {
            int width = max.x - min.x + 1;
            int height = max.y - min.y + 1;
            if (texture == null || texture.width != width || texture.height != height)
            {
                ReleaseTexture();
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Cursed Footsteps pixels",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                pixels = new Color32[width * height];
                sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f),
                    PixelsPerUnit, 0, SpriteMeshType.FullRect);
                renderer.sprite = sprite;
            }
            renderer.transform.position = new Vector3((min.x + width * .5f) / PixelsPerUnit,
                room.transform.position.y + .035f, (min.y + height * .5f) / PixelsPerUnit);

            float patternSize = relic.PuddlePatternSize;
            foreach (var entry in cells)
            {
                Vector2Int key = entry.Key;
                var cell = entry.Value;
                int index = (max.y - key.y) * width + key.x - min.x;
                Vector2 center = CenterOf(key);
                Vector2 side = new(-cell.Direction.y, cell.Direction.x);
                float along = Vector2.Dot(center, cell.Direction);
                float u = Mathf.PingPong(along / patternSize, 1f);
                float v = .48f + Vector2.Dot(cell.Relative, side) * .48f / patternSize;
                int x = Mathf.Clamp((int)(u * FrameSize), 0, FrameSize - 1);
                int y = Mathf.Clamp((int)(v * FrameSize), 0, FrameSize - 1);
                Color32 color = puddle == null ? default : puddle[y * puddleWidth + frame * FrameSize + x];
                float grain = Mathf.PerlinNoise(key.x * .12f, key.y * .12f);
                var liquid = Color32.Lerp(new Color32(17, 10, 25, 233), new Color32(41, 20, 54, 230), grain);
                color = Color32.Lerp(liquid, color, color.a * (.88f / 255f));
                float rimWidth = (1f + grain * 2f) / PixelsPerUnit;
                if (Mathf.Sqrt(cell.Distance) >= cell.Edge - rimWidth)
                    color = Color32.Lerp(color, new Color32(102, 45, 140, 213), .73f);
                pixels[index] = color;
            }

            foreach (var footprint in footprints)
            {
                float size = Mathf.Min(.09f, relic.Radius * .32f);
                Vector2 extent = Vector2.one * (size * 1.4f);
                Vector2Int first = ToCell(footprint.Center - extent);
                Vector2Int last = ToCell(footprint.Center + extent);
                for (int x = first.x; x <= last.x; x++)
                for (int y = first.y; y <= last.y; y++)
                {
                    var key = new Vector2Int(x, y);
                    if (!cells.ContainsKey(key)) continue;
                    Vector2 offset = (CenterOf(key) - footprint.Center) / size;
                    float forward = Vector2.Dot(offset, footprint.Direction);
                    float side = Vector2.Dot(offset, new Vector2(-footprint.Direction.y, footprint.Direction.x));
                    bool toe = forward >= .10f && Mathf.Abs(forward - .58f) + Mathf.Abs(side) * 1.34f <= .80f;
                    bool heel = Mathf.Abs(forward + .58f) + Mathf.Abs(side) * 1.40f <= .58f;
                    if (!toe && !heel) continue;
                    int index = (max.y - y) * width + x - min.x;
                    pixels[index] = Color32.Lerp(pixels[index], new Color32(115, 61, 147, 245), .66f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        internal static SpriteRenderer CreateMotes(Transform parent, CursedFootstepsRelic relic, string name)
        {
            var visual = new GameObject(name, typeof(SpriteRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.rotation = Quaternion.Euler(52f, 0f, 0f);
            visual.transform.localScale = Vector3.one * (relic.MoteSize / relic.Motes.bounds.size.x);
            var motes = visual.GetComponent<SpriteRenderer>();
            motes.sprite = relic.Motes;
            motes.sortingOrder = 1;
            motes.color = Color.clear;
            return motes;
        }

        static Vector2Int ToCell(Vector2 point) =>
            new(Mathf.FloorToInt(point.x * PixelsPerUnit), Mathf.FloorToInt(point.y * PixelsPerUnit));

        static Vector2 CenterOf(Vector2Int cell) =>
            new((cell.x + .5f) / PixelsPerUnit, (cell.y + .5f) / PixelsPerUnit);

        void ReleaseTexture()
        {
            if (sprite != null) Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }

        public void Dispose()
        {
            if (renderer != null)
            {
                renderer.enabled = false;
                Object.Destroy(renderer.gameObject);
            }
            if (mist != null)
            {
                mist.enabled = false;
                Object.Destroy(mist.gameObject);
            }
            ReleaseTexture();
        }
    }
}
