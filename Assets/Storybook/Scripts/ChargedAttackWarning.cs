using UnityEngine;
namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ChargedAttackWarning : MonoBehaviour
    {
        [SerializeField] Material fillMaterial;
        [SerializeField] Color fillColor = new Color(.9f,.025f,.04f,.28f);
        [SerializeField, Min(0)] float groundLift = .001f;
        [SerializeField] int sortingOrder = 1;
        LineRenderer outline;
        MeshRenderer fillRenderer;
        Mesh mesh;
        MaterialPropertyBlock properties;
        readonly Vector3[] vertices = new Vector3[4];
        float width,length;
        public float Progress { get; private set; }
        public bool IsVisible => (outline!=null && outline.enabled) || (fillRenderer!=null && fillRenderer.enabled);
        public void BindOutline(LineRenderer existingOutline) { outline=existingOutline; }
        void EnsureMesh()
        {
            if(fillRenderer!=null)return;
            var go=new GameObject("Charged Ground Warning",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
            mesh=new Mesh{name="Charged Rectangular Attack Warning"};go.GetComponent<MeshFilter>().sharedMesh=mesh;
            mesh.vertices=vertices;mesh.triangles=new[]{0,2,1,0,3,2};
            fillRenderer=go.GetComponent<MeshRenderer>();fillRenderer.sharedMaterial=fillMaterial;
            fillRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;fillRenderer.receiveShadows=false;
            fillRenderer.enabled=false;properties=new MaterialPropertyBlock();
        }
        public void Begin(Vector3 groundOrigin,Vector3 fixedDirection,float fullWidth,float fullLength)
        {
            EnsureMesh();width=fullWidth;length=fullLength;
            var t=fillRenderer.transform;t.SetPositionAndRotation(groundOrigin+Vector3.up*groundLift,Quaternion.LookRotation(fixedDirection,Vector3.up));
            t.localScale=Vector3.one;
            SetProgress(0);
        }
        public void SetProgress(float progress)
        {
            if(fillRenderer==null)return;
            Progress=Mathf.Clamp01(progress);
            float halfWidth=width*.5f,far=length*Progress;
            vertices[0]=new Vector3(-halfWidth,0,0);vertices[1]=new Vector3(halfWidth,0,0);
            vertices[2]=new Vector3(halfWidth,0,far);vertices[3]=new Vector3(-halfWidth,0,far);
            mesh.vertices=vertices;mesh.RecalculateBounds();
            properties.SetColor("_Color",fillColor);fillRenderer.SetPropertyBlock(properties);fillRenderer.sortingOrder=sortingOrder;
            fillRenderer.enabled=enabled && Progress>0 && outline!=null && outline.enabled;
        }
        public void Hide()
        {
            Progress=0;if(fillRenderer!=null)fillRenderer.enabled=false;
            if(outline!=null)outline.enabled=false;
        }
        void LateUpdate()
        {
            // If another system aborts or hides the outline, never leave the fill behind.
            if(outline==null || !outline.enabled){if(fillRenderer!=null)fillRenderer.enabled=false;return;}
            if(fillRenderer!=null){properties.SetColor("_Color",fillColor);fillRenderer.SetPropertyBlock(properties);}
        }
        void OnDisable(){Hide();}
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(fillRenderer!=null)Destroy(fillRenderer.gameObject);}
    }
}
