using UnityEngine;
namespace Storybook
{
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class HongGildongIdleVisual : MonoBehaviour
    {
        [SerializeField] HongGildong player;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite idleFront, idleBack;
        [SerializeField, Min(.1f)] float loopDuration = 1.5f;
        [SerializeField, Range(0,2)] float strength = 1;
        [SerializeField, Range(0,2)] float breathPixels = .85f;
        [SerializeField, Range(0,2)] float clothPixels = .6f;
        [SerializeField, Range(0,1)] float swordPixels = .35f;
        [SerializeField, Range(0,1)] float hatPixels = .15f;
        const int Columns=12, Rows=20;
        Mesh mesh;
        MeshRenderer surface;
        Material material;
        Sprite source;
        Vector3[] rest, deformed;
        Vector2[] normalized;
        float clock;
        bool rendering;
        public bool IsPlaying => rendering;
        void Awake()
        {
            var go=new GameObject("Idle Motion Surface",typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(body.transform,false);
            surface=go.GetComponent<MeshRenderer>();
            mesh=new Mesh{name="Hong Gildong Idle Visual Mesh"};go.GetComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(body.sharedMaterial);surface.sharedMaterial=material;
            surface.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;surface.receiveShadows=false;
            go.SetActive(false);
        }
        void Build(Sprite sprite)
        {
            source=sprite;int count=(Columns+1)*(Rows+1);
            rest=new Vector3[count];deformed=new Vector3[count];normalized=new Vector2[count];var uv=new Vector2[count];
            Rect rect=sprite.rect;Vector2 pivot=sprite.pivot;float ppu=sprite.pixelsPerUnit;
            for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
            {
                int i=y*(Columns+1)+x;float u=(float)x/Columns,v=(float)y/Rows;normalized[i]=new Vector2(u,v);
                rest[i]=new Vector3((u*rect.width-pivot.x)/ppu,(v*rect.height-pivot.y)/ppu,0);
                uv[i]=new Vector2((rect.x+u*rect.width)/sprite.texture.width,(rect.y+v*rect.height)/sprite.texture.height);
            }
            var triangles=new int[Columns*Rows*6];int t=0;
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
            {
                int a=y*(Columns+1)+x,b=a+1,c=a+Columns+1,d=c+1;
                triangles[t++]=a;triangles[t++]=c;triangles[t++]=b;triangles[t++]=b;triangles[t++]=c;triangles[t++]=d;
            }
            mesh.Clear();mesh.vertices=rest;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            material.mainTexture=sprite.texture;
        }
        void LateUpdate()
        {
            bool eligible=player!=null && player.isActiveAndEnabled && !player.Attacking && !player.IsDashing && !player.HasMoveDestination
                && body.gameObject.activeInHierarchy && (body.sprite==idleFront || body.sprite==idleBack);
            if(!eligible || (!rendering && !body.enabled)){Stop();return;}
            if(source!=body.sprite)Build(body.sprite);
            clock=Mathf.Repeat(clock+Time.deltaTime,Mathf.Max(.1f,loopDuration));
            ApplyPose(clock/Mathf.Max(.1f,loopDuration));
            surface.sortingLayerID=body.sortingLayerID;surface.sortingOrder=body.sortingOrder;
            if(material.HasProperty("_Color"))material.SetColor("_Color",body.color);
            surface.gameObject.SetActive(true);body.enabled=false;rendering=true;
        }
        void ApplyPose(float phase)
        {
            float angle=phase*Mathf.PI*2,wave=Mathf.Sin(angle),breath=(1-Mathf.Cos(angle))*.5f;
            for(int i=0;i<rest.Length;i++)
            {
                Vector2 n=normalized[i];Vector3 p=rest[i];
                // Bottom 20% stays rigid: feet and the root/collider never move.
                float aboveFeet=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.20f,.45f,n.y));
                float robe=Mathf.Clamp01(1-Mathf.Abs(n.y-.37f)/.20f)*aboveFeet;
                float hat=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.80f,.95f,n.y));
                float sword=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.9f,n.x))*Mathf.Clamp01(1-Mathf.Abs(n.y-.48f)/.23f);
                p.y+=(breath*breathPixels*aboveFeet+wave*swordPixels*sword)*strength/source.pixelsPerUnit;
                p.x+=(wave*clothPixels*robe-wave*hatPixels*hat)*strength/source.pixelsPerUnit;
                if(body.flipX)p.x=-p.x;
                deformed[i]=p;
            }
            mesh.vertices=deformed;mesh.RecalculateBounds();
        }
        void Stop()
        {
            clock=0;
            if(surface!=null)surface.gameObject.SetActive(false);
            if(rendering && body!=null)body.enabled=true;
            rendering=false;
        }
        void OnDisable(){Stop();}
        void OnDestroy(){if(surface!=null)Destroy(surface.gameObject);if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
