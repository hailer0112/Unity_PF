using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Storybook
{
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
    public sealed class PufferfishVisual : MonoBehaviour
    {
        // The third Explode frame is the impact. The preceding two play before the fuse ends.
        const int ExplosionImpactFrame = 2;
        const float ExplosionPreludeStart = .84f;
        const float InflateScale = .76f;
        const float ExplosionScale = .88f;
        const float ReactionScale = .85f;

        [SerializeField] PufferfishBomberAI ai;
        [SerializeField] MonsterHealth health;
        // Directional clips are ordered front, right, back, left.
        [SerializeField] AnimationClip[] idle, move;
        [SerializeField] AnimationClip inflate, explode, hit, die;

        SpriteRenderer body;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable playable;
        AnimationClip activeClip;
        Vector3 previousPosition;
        float motionStarted, hitAt = float.NegativeInfinity, deathAt;
        int facing, baseSortingOrder;
        bool moving;

        float ImpactTime => ExplosionImpactFrame / explode.frameRate;

        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            baseSortingOrder = body.sortingOrder;
        }

        void OnEnable()
        {
            graph = PlayableGraph.Create("Pufferfish sprites");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            output = AnimationPlayableOutput.Create(graph, "Pufferfish sprite", GetComponent<Animator>());
            graph.Play();
            previousPosition = ai.transform.position;
            motionStarted = (float)health.ActionTime;
            health.Damaged += ShowHit;
            health.Defeated += ShowDeath;
        }

        void OnDisable()
        {
            health.Damaged -= ShowHit;
            health.Defeated -= ShowDeath;
            if (graph.IsValid()) graph.Destroy();
            activeClip = null;
        }

        void ShowHit() => hitAt = (float)health.ActionTime;

        void ShowDeath(MonsterDeathCause cause)
        {
            deathAt = (float)health.ActionTime;
            if (cause == MonsterDeathCause.SelfDestruct)
            {
                body.sortingOrder = baseSortingOrder + 2;
                Sample(explode, ImpactTime, ExplosionScale);
            }
            else Sample(die, 0f, ReactionScale);
        }

        void LateUpdate()
        {
            if (health.IsTimeStopped) return;
            Vector3 position = ai.transform.position;
            Vector3 delta = position - previousPosition;
            previousPosition = position;
            float now = (float)health.ActionTime;

            if (health.IsDead)
            {
                if (health.DeathCause == MonsterDeathCause.SelfDestruct)
                {
                    float elapsed = ImpactTime + (now - deathAt);
                    body.enabled = elapsed < 5f / explode.frameRate;
                    if (body.enabled) Sample(explode, elapsed, ExplosionScale);
                }
                else Sample(die, BasicHitPose.DefeatMotionProgress(now - deathAt, die.length) * die.length, ReactionScale);
                return;
            }

            if (ai.CurrentState == PufferfishBomberAI.State.Inflate)
            {
                float progress = ai.InflateProgress;
                if (progress >= ExplosionPreludeStart)
                {
                    float prelude = Mathf.InverseLerp(ExplosionPreludeStart, 1f, progress) * ImpactTime;
                    Sample(explode, Mathf.Min(prelude, ImpactTime - .0001f), ExplosionScale);
                }
                else Sample(inflate, progress / ExplosionPreludeStart * (5f / inflate.frameRate), InflateScale);
                return;
            }

            bool moved = ai.CurrentState == PufferfishBomberAI.State.Chase &&
                delta.x * delta.x + delta.z * delta.z > .00000001f;
            if (moved != moving)
            {
                moving = moved;
                motionStarted = now;
            }
            if (moving)
                facing = Mathf.Abs(delta.x) > Mathf.Abs(delta.z) ? (delta.x > 0f ? 1 : 3) : (delta.z > 0f ? 2 : 0);
            else if (ai.CurrentState == PufferfishBomberAI.State.Chase) facing = ai.Facing;

            if (now - hitAt < 3f / hit.frameRate)
            {
                Sample(hit, now - hitAt, ReactionScale);
                return;
            }

            AnimationClip locomotion = (moving ? move : idle)[facing];
            Sample(locomotion, Mathf.Repeat(now - motionStarted, 4f / locomotion.frameRate), 1f);
        }

        void Sample(AnimationClip clip, float elapsed, float scale)
        {
            // Match the sheet's drawing scales while keeping each authored pose in proportion.
            transform.localScale = Vector3.one * scale;
            if (activeClip != clip)
            {
                var previous = playable;
                playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(false);
                output.SetSourcePlayable(playable);
                if (previous.IsValid()) graph.DestroyPlayable(previous);
                activeClip = clip;
            }

            playable.SetTime(Mathf.Clamp(elapsed, 0f, clip.length));
            graph.Evaluate(0f);
        }
    }
}
