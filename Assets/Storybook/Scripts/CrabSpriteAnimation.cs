using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrabSpriteAnimation : MonoBehaviour
    {
        [SerializeField] Sprite[] idleFrames, walkFrames;
        [SerializeField, Min(1)] float idleFPS = 1.5f, walkFPS = 7;
        [SerializeField, Tooltip("Preview only: enable in Play mode to inspect walk frames without adding AI.")] bool previewWalk;
        SpriteRenderer body;
        MonsterHealth health;
        Vector3 previousPosition;
        float clock;
        bool wasWalking;
        void Awake() { body=GetComponent<SpriteRenderer>();health=GetComponentInParent<MonsterHealth>(); }
        void OnEnable() { previousPosition=transform.root.position;clock=0; }
        void LateUpdate()
        {
            Vector3 position=transform.root.position;
            float speed=Vector3.ProjectOnPlane(position-previousPosition,Vector3.up).magnitude / Mathf.Max(Time.deltaTime,.0001f);
            previousPosition=position;
            if (health!=null && health.IsDead) return;
            bool walking=previewWalk || speed>.05f;
            if (walking!=wasWalking) clock=0;
            wasWalking=walking;
            clock+=Time.deltaTime;
            var frames=walking?walkFrames:idleFrames;
            if(frames!=null && frames.Length>0)body.sprite=frames[(int)(clock*(walking?walkFPS:idleFPS))%frames.Length];
        }
    }
}
