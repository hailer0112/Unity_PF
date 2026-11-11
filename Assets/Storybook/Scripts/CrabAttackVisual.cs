using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrabAttackVisual : MonoBehaviour
    {
        [SerializeField, Tooltip("Shoulder position in the original sprite rectangle, normalized from bottom left.")]
        Vector2 shoulderAnchor = new Vector2(.32f,.69f);
        SpriteRenderer body;
        CrabSpriteAnimation animation;
        readonly SpriteRenderer[] pieces=new SpriteRenderer[4];
        readonly Sprite[] temporarySprites=new Sprite[4];
        Transform shoulderJoint;
        SpriteRenderer shoulderCover;
        Sprite coverSprite;
        bool active;
        void Awake(){body=GetComponent<SpriteRenderer>();animation=GetComponent<CrabSpriteAnimation>();}
        public void Begin(Vector3 direction)
        {
            End();active=true;animation.ActionLocked=true;
            if (animation.HasDirectionalAnimator)
            {
                animation.BeginDirectionalAttack(direction);
                return;
            }

            var sprite=body.sprite;Rect r=sprite.rect;
            float x=r.width*.34f,y=r.height*.16f,top=r.height*.72f;
            Rect[] parts={new Rect(0,top,r.width,r.height-top),new Rect(0,0,r.width,y),new Rect(x,y,r.width-x,top-y),new Rect(0,y,x,top-y)};
            shoulderJoint=new GameObject("Right Shoulder Joint").transform;shoulderJoint.SetParent(transform,false);
            shoulderJoint.localPosition=new Vector3(r.width*shoulderAnchor.x-sprite.pivot.x,r.height*shoulderAnchor.y-sprite.pivot.y,0)/sprite.pixelsPerUnit;
            for(int i=0;i<4;i++)
            {
                Rect p=parts[i];Vector2 pivot=i==3?new Vector2(1,.73f):new Vector2(.5f,.5f);
                temporarySprites[i]=Sprite.Create(sprite.texture,new Rect(r.x+p.x,r.y+p.y,p.width,p.height),pivot,sprite.pixelsPerUnit,0,SpriteMeshType.FullRect);
                var go=new GameObject(i==3?"Attached Right Claw":"Temporary Attack Body "+i);go.transform.SetParent(i==3?shoulderJoint:transform,false);
                Vector3 restPosition=new Vector3(p.x+p.width*pivot.x-sprite.pivot.x,p.y+p.height*pivot.y-sprite.pivot.y,0)/sprite.pixelsPerUnit;
                go.transform.localPosition=i==3?restPosition-shoulderJoint.localPosition:restPosition;
                pieces[i]=go.AddComponent<SpriteRenderer>();pieces[i].sprite=temporarySprites[i];pieces[i].sharedMaterial=body.sharedMaterial;
                pieces[i].sortingLayerID=body.sortingLayerID;pieces[i].sortingOrder=body.sortingOrder+(i==3?1:0);
            }
            // The original proximal arm/shoulder pixels overlap the moving crop.
            // Keep the small joint cap above the forward arm, without covering the claw. Instead of
            // translating a detached wrist patch through empty space.
            Rect cover=new Rect(r.width*.26f,r.height*.61f,r.width*.14f,r.height*.14f);
            coverSprite=Sprite.Create(sprite.texture,new Rect(r.x+cover.x,r.y+cover.y,cover.width,cover.height),new Vector2(.5f,.5f),sprite.pixelsPerUnit,0,SpriteMeshType.FullRect);
            var cap=new GameObject("Shoulder Overlap");cap.transform.SetParent(transform,false);
            cap.transform.localPosition=new Vector3(cover.center.x-sprite.pivot.x,cover.center.y-sprite.pivot.y,0)/sprite.pixelsPerUnit;
            shoulderCover=cap.AddComponent<SpriteRenderer>();shoulderCover.sprite=coverSprite;shoulderCover.sharedMaterial=body.sharedMaterial;
            shoulderCover.sortingLayerID=body.sortingLayerID;shoulderCover.sortingOrder=body.sortingOrder+2;
            body.enabled=false;
        }
        public void Pose(float elapsed,float windup,float slam,float recovery)
        {
            if(!active)return;
            if (animation.HasDirectionalAnimator)
            {
                // The clip's midpoint is the same contact instant used by the damage check.
                float progress = elapsed < windup ? .5f * elapsed / Mathf.Max(.001f, windup)
                    : .5f + .49f * Mathf.Clamp01((elapsed - windup - slam) / Mathf.Max(.001f, recovery));
                animation.SampleDirectionalAttack(progress);
                return;
            }

            float angle;
            if(elapsed<windup)angle=Mathf.Lerp(0,-115,Mathf.SmoothStep(0,1,elapsed/Mathf.Max(.001f,windup)));
            else if(elapsed<windup+slam)angle=5;
            else angle=Mathf.Lerp(5,0,Mathf.Clamp01((elapsed-windup-slam)/Mathf.Max(.001f,recovery)));
            // Rotate the shoulder only. Its position, the arm offset and all scales
            // remain fixed for windup, impact and recovery.
            if(shoulderJoint!=null)shoulderJoint.localRotation=Quaternion.Euler(0,0,angle);
            foreach(var piece in pieces)if(piece!=null)piece.color=body.color;
            if(shoulderCover!=null)shoulderCover.color=body.color;
        }
        public void End()
        {
            active=false;if(body!=null)body.enabled=true;if(animation!=null)animation.ActionLocked=false;
            for(int i=0;i<4;i++)
            {
                if(pieces[i]!=null){pieces[i].gameObject.SetActive(false);Destroy(pieces[i].gameObject);}
                if(temporarySprites[i]!=null)Destroy(temporarySprites[i]);pieces[i]=null;temporarySprites[i]=null;
            }
            if(shoulderJoint!=null){shoulderJoint.gameObject.SetActive(false);Destroy(shoulderJoint.gameObject);shoulderJoint=null;}
            if(shoulderCover!=null){shoulderCover.gameObject.SetActive(false);Destroy(shoulderCover.gameObject);shoulderCover=null;}
            if(coverSprite!=null){Destroy(coverSprite);coverSprite=null;}
        }
        void OnDisable(){End();}
    }
}
