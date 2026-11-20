using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleButtonArea : MaskableGraphic, ICanvasRaycastFilter
    {
        // Clockwise outline of the painted button, in its own normalized image coordinates.
        static readonly Vector2[] Outline =
        {
            new Vector2(.10f, 1f), new Vector2(.90f, 1f), new Vector2(.95f, .80f),
            new Vector2(1f, .50f), new Vector2(.95f, .20f), new Vector2(.90f, 0f),
            new Vector2(.10f, 0f), new Vector2(.05f, .20f), new Vector2(0f, .50f),
            new Vector2(.05f, .80f)
        };

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint,
                    eventCamera, out Vector2 local)) return false;

            Rect rect = rectTransform.rect;
            Vector2 point = Rect.PointToNormalized(rect, local);
            for (int i = 0; i < Outline.Length; i++)
            {
                Vector2 start = Outline[i];
                Vector2 edge = Outline[(i + 1) % Outline.Length] - start;
                Vector2 delta = point - start;
                if (edge.x * delta.y - edge.y * delta.x > 0f) return false;
            }
            return rect.Contains(local);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Rect outer = Rect.MinMaxRect(rect.xMin + 1f, rect.yMin + 1f, rect.xMax - 1f, rect.yMax - 1f);
            Rect inner = Rect.MinMaxRect(rect.xMin + 6f, rect.yMin + 4f, rect.xMax - 6f, rect.yMax - 4f);
            for (int i = 0; i < Outline.Length; i++)
            {
                mesh.AddVert(Rect.NormalizedToPoint(outer, Outline[i]), color, Vector2.zero);
                mesh.AddVert(Rect.NormalizedToPoint(inner, Outline[i]), color, Vector2.zero);
            }

            for (int i = 0; i < Outline.Length; i++)
            {
                int outerIndex = i * 2;
                int next = (outerIndex + 2) % (Outline.Length * 2);
                mesh.AddTriangle(outerIndex, next, next + 1);
                mesh.AddTriangle(outerIndex, next + 1, outerIndex + 1);
            }
        }
    }
}
