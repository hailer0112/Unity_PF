using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CrabSpriteAnimation : MonoBehaviour
    {
        [SerializeField] Sprite[] idleFrames, walkFrames;
        [SerializeField, Min(1)] float idleFPS = 1.5f;
        [SerializeField, Min(.1f)] float walkFramesPerMeter = 2.8f;
        [SerializeField, Tooltip("Preview only: enable in Play mode to inspect walk frames without adding AI.")] bool previewWalk;
        SpriteRenderer body;
        MonsterHealth health;
        Transform movementRoot;
        Vector3 previousPosition;
        float clock;
        bool wasWalking;
        public bool ActionLocked { get; set; }
        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            health = GetComponentInParent<MonsterHealth>();
            movementRoot = health != null ? health.transform : transform;
        }
        void OnEnable() { previousPosition=movementRoot.position;clock=0; }
        void LateUpdate()
        {
            Vector3 position=movementRoot.position;
            float distance = Vector3.ProjectOnPlane(position - previousPosition, Vector3.up).magnitude;
            float speed = distance / Mathf.Max(Time.deltaTime, .0001f);
            previousPosition=position;
            if ((health!=null && health.IsDead) || ActionLocked) return;
            bool walking=previewWalk || speed>.05f;
            if (walking!=wasWalking) clock=0;
            wasWalking=walking;
            float walkedDistance = previewWalk && speed <= .05f ? 1.6f * Time.deltaTime : distance;
            clock += walking ? walkedDistance * walkFramesPerMeter : Time.deltaTime * idleFPS;
            var frames=walking?walkFrames:idleFrames;
            if(frames!=null && frames.Length>0)body.sprite=frames[(int)clock % frames.Length];
        }
    }
}
