using UnityEngine;

namespace Storybook
{
    internal readonly struct DragonKingWaveLane
    {
        internal const float CrestDepth = .52f;
        internal readonly Vector3 Origin, Forward, Right;
        internal readonly float Width, Length;
        internal Quaternion Rotation => Quaternion.LookRotation(Forward, Vector3.up);

        DragonKingWaveLane(Vector3 origin, Vector3 forward, float width, float length)
        {
            Origin = origin;
            Forward = forward;
            Right = Vector3.Cross(Vector3.up, forward);
            Width = width;
            Length = length;
        }

        internal static bool TryBuild(BoxCollider floor, Rect movementBounds, Vector3 target, float widthRatio,
            out DragonKingWaveLane lane)
        {
            lane = default;
            Bounds bounds = floor.bounds;
            // The arena floor overlaps the surrounding walls. Use the same X/Z limits as player movement.
            float left = Mathf.Max(bounds.min.x, movementBounds.xMin);
            float right = Mathf.Min(bounds.max.x, movementBounds.xMax);
            float top = Mathf.Min(bounds.max.z, movementBounds.yMax);
            float bottom = Mathf.Max(bounds.min.z, movementBounds.yMin);
            float width = (right - left) * Mathf.Clamp01(widthRatio);
            float length = top - bottom;
            if (width < .1f || length < .1f) return false;

            float halfWidth = width * .5f;
            float center = Mathf.Clamp(target.x, left + halfWidth, right - halfWidth);
            // The crest travels from the upper edge to the lower edge of the arena, past the dais.
            var origin = new Vector3(center, bounds.max.y, top);
            lane = new DragonKingWaveLane(origin, Vector3.back, width, length);
            return true;
        }

        internal bool Contains(Vector3 point)
        {
            Vector3 delta = point - Origin;
            float along = Vector3.Dot(delta, Forward);
            return along >= 0f && along <= Length && Mathf.Abs(Vector3.Dot(delta, Right)) <= Width * .5f;
        }

        internal bool CrossesCrest(Vector3 playerBefore, Vector3 playerAfter, float frontBefore, float frontAfter)
        {
            Vector3 a = playerBefore - Origin;
            Vector3 b = playerAfter - Origin;
            float alongA = Vector3.Dot(a, Forward), alongB = Vector3.Dot(b, Forward);
            float enter = 0f, exit = 1f;
            return IntersectAxis(Vector3.Dot(a, Right), Vector3.Dot(b, Right), -Width * .5f, Width * .5f, ref enter, ref exit)
                && IntersectAxis(alongA, alongB, 0f, Length, ref enter, ref exit)
                && IntersectAxis(alongA - frontBefore, alongB - frontAfter, -CrestDepth, 0f, ref enter, ref exit);
        }

        static bool IntersectAxis(float a, float b, float min, float max, ref float enter, ref float exit)
        {
            float step = b - a;
            if (Mathf.Abs(step) < .00001f) return a >= min && a <= max;
            float near = (min - a) / step, far = (max - a) / step;
            enter = Mathf.Max(enter, Mathf.Min(near, far));
            exit = Mathf.Min(exit, Mathf.Max(near, far));
            return enter <= exit;
        }
    }
}
