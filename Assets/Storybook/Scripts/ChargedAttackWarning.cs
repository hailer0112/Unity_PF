using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ChargedAttackWarning : MonoBehaviour
    {
        const int CircleSegments = 64;

        [SerializeField] Material fillMaterial;
        [SerializeField] Color fillColor = new Color(.9f, .025f, .04f, .28f);
        [SerializeField, Min(0)] float groundLift = .001f;
        [SerializeField] int sortingOrder = 1;

        static readonly int Tint = Shader.PropertyToID("_Color");
        readonly Vector3[] rectangle = new Vector3[4];
        LineRenderer outline;
        MeshRenderer fillRenderer;
        Mesh mesh;
        MaterialPropertyBlock properties;
        bool circular;
        float width, length, radius;

        public float Progress { get; private set; }
        public bool IsVisible => (outline != null && outline.enabled) || (fillRenderer != null && fillRenderer.enabled);

        public void BindOutline(LineRenderer existingOutline) => outline = existingOutline;

        void EnsureMesh(bool circle)
        {
            if (fillRenderer == null)
            {
                var child = new GameObject("Charged Ground Warning", typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(transform, false);
                mesh = new Mesh { name = "Charged Ground Warning" };
                mesh.MarkDynamic();
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                fillRenderer = child.GetComponent<MeshRenderer>();
                fillRenderer.sharedMaterial = fillMaterial != null ? fillMaterial : outline.sharedMaterial;
                fillRenderer.shadowCastingMode = ShadowCastingMode.Off;
                fillRenderer.receiveShadows = false;
                properties = new MaterialPropertyBlock();
            }
            else if (circular == circle) return;

            circular = circle;
            mesh.Clear();
            if (!circular)
            {
                mesh.vertices = rectangle;
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                return;
            }

            var vertices = new Vector3[CircleSegments + 1];
            var indices = new int[CircleSegments * 3];
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = 2f * Mathf.PI * i / CircleSegments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                indices[i * 3 + 1] = (i + 1) % CircleSegments + 1;
                indices[i * 3 + 2] = i + 1;
            }
            mesh.vertices = vertices;
            mesh.triangles = indices;
        }

        public void Begin(Vector3 groundOrigin, Vector3 fixedDirection, float fullWidth, float fullLength)
        {
            EnsureMesh(false);
            width = fullWidth;
            length = fullLength;
            fillRenderer.transform.SetPositionAndRotation(groundOrigin + Vector3.up * groundLift,
                Quaternion.LookRotation(fixedDirection, Vector3.up));
            fillRenderer.transform.localScale = Vector3.one;
            SetProgress(0f);
        }

        public void BeginCircle(Vector3 center, float fullRadius)
        {
            EnsureMesh(true);
            radius = fullRadius;
            fillRenderer.transform.SetPositionAndRotation(center + Vector3.up * groundLift, Quaternion.identity);
            SetProgress(0f);
        }

        public void SetProgress(float progress)
        {
            if (fillRenderer == null) return;
            Progress = Mathf.Clamp01(progress);
            if (circular)
            {
                float extent = radius * Progress;
                fillRenderer.transform.localScale = new Vector3(extent, 1f, extent);
            }
            else
            {
                float halfWidth = width * .5f;
                float far = length * Progress;
                rectangle[0] = new Vector3(-halfWidth, 0f, 0f);
                rectangle[1] = new Vector3(halfWidth, 0f, 0f);
                rectangle[2] = new Vector3(halfWidth, 0f, far);
                rectangle[3] = new Vector3(-halfWidth, 0f, far);
                mesh.vertices = rectangle;
                mesh.RecalculateBounds();
            }

            properties.SetColor(Tint, fillColor);
            fillRenderer.SetPropertyBlock(properties);
            fillRenderer.sortingOrder = sortingOrder;
            fillRenderer.enabled = enabled && Progress > 0f && outline != null && outline.enabled;
        }

        public void Hide()
        {
            Progress = 0f;
            if (fillRenderer != null) fillRenderer.enabled = false;
            if (outline != null) outline.enabled = false;
        }

        void LateUpdate()
        {
            if (fillRenderer == null) return;
            // If another system aborts or hides the outline, never leave the fill behind.
            if (outline == null || !outline.enabled)
            {
                fillRenderer.enabled = false;
                return;
            }
            properties.SetColor(Tint, fillColor);
            fillRenderer.SetPropertyBlock(properties);
        }

        void OnDisable() => Hide();

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (fillRenderer != null) Destroy(fillRenderer.gameObject);
        }
    }
}
