using UnityEngine;
namespace Storybook
{
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class HongGildongIdleVisual : MonoBehaviour
    {
        [SerializeField] HongGildong player;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Sprite idleFront, idleBack;
        [SerializeField] Sprite[] renewedIdle = new Sprite[4];
        [SerializeField] Sprite[] hitFrames = new Sprite[16];
        [SerializeField] Sprite[] deathFrames = new Sprite[24];
        const float HitDuration = .30f;
        const float DeathFrameDuration = .08f;
        PlayerHealth health;
        double hitStartedAt = double.NegativeInfinity;
        double deathStartedAt = double.NegativeInfinity;
        int hitDirection, deathDirection;
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
        Sprite[] sheathedIdle;
        AnimationClip[] idleClips;
        Sprite[] idleFrames;
        [SerializeField, Range(4f,6f)] float idleFramesPerSecond = 5f;
        float spriteClock;
        public bool IsPlaying => rendering;
        void Awake()
        {
            if (player != null) health = player.GetComponent<PlayerHealth>();
            sheathedIdle=Resources.LoadAll<Sprite>("HongGildongSheathedIdle");
            System.Array.Sort(sheathedIdle,(a,b)=>string.CompareOrdinal(a.name,b.name));
            idleClips=Resources.LoadAll<AnimationClip>("HongGildongIdle");
            System.Array.Sort(idleClips,(a,b)=>string.CompareOrdinal(a.name,b.name));
            idleFrames=Resources.LoadAll<Sprite>("HongGildongIdle/Idle32");
            System.Array.Sort(idleFrames,(a,b)=>string.CompareOrdinal(a.name,b.name));
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
        void OnEnable()
        {
            if (health == null) return;
            health.Damaged += BeginHit;
            health.Died += BeginDeath;
        }

        void BeginHit()
        {
            hitDirection = GetDamageDirection();
            hitStartedAt = Time.timeAsDouble;
        }

        void BeginDeath()
        {
            deathDirection = GetDamageDirection();
            deathStartedAt = Time.unscaledTimeAsDouble;
        }

        int GetDamageDirection()
        {
            Vector3 direction = player.FacingDirection;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? (direction.x > 0f ? 0 : 1) : (direction.z > 0f ? 3 : 2);
        }

        bool DrawDamagePose()
        {
            if (health == null) return false;
            if (health.IsDead)
            {
                if (double.IsNegativeInfinity(deathStartedAt)) return false;
                int frame = (int)System.Math.Min(5d, (Time.unscaledTimeAsDouble - deathStartedAt) / DeathFrameDuration);
                return DrawDamageSprite(deathFrames, deathDirection, 6, frame);
            }

            double elapsed = Time.timeAsDouble - hitStartedAt;
            if (!player.isActiveAndEnabled || elapsed >= HitDuration || player.Attacking || player.IsDashing ||
                player.IsCastingAzureSlash || player.IsCastingCloneSlash || player.IsCastingSwordTyphoon)
                return false;

            int hitFrame = (int)(elapsed / HitDuration * 4);
            return DrawDamageSprite(hitFrames, hitDirection, 4, hitFrame);
        }

        bool DrawDamageSprite(Sprite[] frames, int direction, int count, int frame)
        {
            if (frames.Length != count * 4 || frames[direction * count + frame] == null) return false;
            Stop();
            body.sprite = frames[direction * count + frame];
            body.flipX = false;
            body.enabled = true;
            return true;
        }

        void LateUpdate()
        {
            if (DrawDamagePose()) return;
            bool eligible=player!=null && player.isActiveAndEnabled && !player.Attacking && !player.IsCastingAzureSlash && !player.IsCastingCloneSlash && !player.IsCastingSwordTyphoon && !player.IsDashing && !player.HasMoveDestination
                && body.gameObject.activeInHierarchy && (body.sprite==idleFront || body.sprite==idleBack);
            if(!eligible || (!rendering && !body.enabled)){spriteClock=0f;Stop();return;}
            if(renewedIdle.Length==4 && System.Array.TrueForAll(renewedIdle,sprite=>sprite!=null))
            {
                Stop();
                Vector3 direction=player.FacingDirection;
                int index=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)
                    ? (direction.x>0f ? 3 : 2) : (direction.z>0f ? 1 : 0);
                body.sprite=renewedIdle[index];
                body.flipX=false;
                body.enabled=true;
                return;
            }
            if(idleClips.Length==4 && idleFrames.Length==32)
            {
                Stop();
                Vector3 direction=player.FacingDirection;
                int index=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)
                    ? (direction.x>0f ? 3 : 2) : (direction.z>0f ? 1 : 0);
                spriteClock=Mathf.Repeat(spriteClock+Time.deltaTime,8f/idleFramesPerSecond);
                body.sprite=idleFrames[index*8+Mathf.FloorToInt(spriteClock*idleFramesPerSecond)];
                body.flipX=false;
                body.enabled=true;
                return;
            }
            if(sheathedIdle.Length==4)
            {
                Stop();
                Vector3 direction=player.FacingDirection;
                int index=Mathf.Abs(direction.x)>Mathf.Abs(direction.z)
                    ? (direction.x>0f ? 1 : 3) : (direction.z>0f ? 2 : 0);
                body.sprite=sheathedIdle[index];
                body.flipX=false;
                body.enabled=true;
                return;
            }
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
        void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= BeginHit;
                health.Died -= BeginDeath;
            }
            Stop();
        }
        void OnDestroy(){if(surface!=null)Destroy(surface.gameObject);if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }
}
