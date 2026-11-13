using System.Collections.Generic;
using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PalaceCover : MonoBehaviour
    {
        static readonly List<PalaceCover> active = new();
        BoxCollider obstacle;
        SpriteRenderer artwork;
        HongGildong player;

        void Awake()
        {
            obstacle = GetComponent<BoxCollider>();
            artwork = GetComponent<SpriteRenderer>();
        }

        void Start() => player = FindAnyObjectByType<HongGildong>();

        void LateUpdate()
        {
            if (player == null || artwork == null) return;
            // Preserve the existing character orders while the player passes the pillar.
            artwork.sortingOrder = player.transform.position.z > transform.position.z + .02f ? 3 : -1;
        }
        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        internal static Vector3 NextPoint(Vector3 start, Vector3 goal, float clearance)
        {
            start.y = goal.y = 0f;
            foreach (var cover in active)
            {
                if (!cover.obstacle.enabled) continue;
                Bounds box = cover.Footprint(clearance);
                if (!Crosses(start, goal, box)) continue;

                // A square cover has four possible corners. Take the cheapest visible corner
                // with a clear continuation around either adjacent side.
                Vector3 best = goal;
                float bestLength = float.PositiveInfinity;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 corner = Corner(box, i);
                    if (Crosses(start, corner, box)) continue;
                    float remainder = float.PositiveInfinity;
                    if (!Crosses(corner, goal, box)) remainder = Vector3.Distance(corner, goal);
                    else
                    {
                        for (int step = 1; step <= 3; step += 2)
                        {
                            Vector3 adjacent = Corner(box, (i + step) % 4);
                            if (!Crosses(adjacent, goal, box))
                                remainder = Mathf.Min(remainder, Vector3.Distance(corner, adjacent) + Vector3.Distance(adjacent, goal));
                        }
                    }
                    float length = Vector3.Distance(start, corner) + remainder;
                    if (length >= bestLength) continue;
                    best = corner;
                    bestLength = length;
                }
                if (best != goal) return best;
            }
            return goal;
        }

        internal static Vector3 WalkableGoal(Vector3 start, Vector3 goal, float clearance)
        {
            Vector3 flatGoal = new Vector3(goal.x, 0f, goal.z);
            foreach (var cover in active)
            {
                if (!cover.obstacle.enabled) continue;
                Bounds box = cover.Footprint(clearance + .10f);
                if (!box.Contains(flatGoal)) continue;
                Vector3 from = new Vector3(start.x, 0f, start.z);
                Vector3 edge = box.ClosestPoint(from);
                if (edge == from)
                    edge.x = from.x < box.center.x ? box.min.x : box.max.x;
                return new Vector3(edge.x, goal.y, edge.z);
            }
            return goal;
        }

        Bounds Footprint(float clearance)
        {
            Bounds box = obstacle.bounds;
            box.center = new Vector3(box.center.x, 0f, box.center.z);
            box.Expand(new Vector3(2f * clearance, 2f, 2f * clearance));
            return box;
        }

        static Vector3 Corner(Bounds box, int i)
        {
            const float margin = .10f;
            return new Vector3(i is 0 or 3 ? box.min.x - margin : box.max.x + margin, 0f,
                i < 2 ? box.min.z - margin : box.max.z + margin);
        }

        static bool Crosses(Vector3 from, Vector3 to, Bounds box)
        {
            Vector3 offset = to - from;
            return offset.sqrMagnitude > .0001f &&
                box.IntersectRay(new Ray(from, offset.normalized), out float distance) && distance < offset.magnitude;
        }
    }
}
