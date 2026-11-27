using UnityEngine;

namespace Storybook
{
    [DefaultExecutionOrder(90), DisallowMultipleComponent]
    public sealed class BasicHitPose : MonoBehaviour
    {
        internal const float DefeatHitStop = .08f;
        static ParticleSystem impactPrefab;
        SpriteRenderer sprite;
        MonsterHealth health;
        Sprite heldSprite;
        Quaternion baseRotation, appliedRotation;
        double startedAt, freezeUntil;
        float duration, tilt;
        SkillHit? activeSkill;
        bool rotated;
        bool defeated;
        double defeatHoldUntil;
        float defeatAt, fallTime, fadeTime, defeatTilt, sink, height;
        Vector3 defeatOffset, restingPosition, restingScale;
        bool pendingTyphoonImpact;
        Vector3 typhoonImpactPoint;
        float typhoonImpactScale;
        Quaternion restingRotation;

        void Awake()
        {
            sprite = GetComponentInChildren<SpriteRenderer>();
            health = GetComponent<MonsterHealth>();
        }

        internal void Play(Vector3 away, float seconds, float degrees, float stop, SkillHit? skill)
        {
            double now = health.UnscaledActionTime;
            if (sprite == null || (now < startedAt + duration && PosePriority(skill) <= PosePriority(activeSkill))) return;
            activeSkill = skill;
            startedAt = now;
            freezeUntil = now + stop;
            heldSprite = sprite.sprite;
            duration = seconds;
            tilt = away.x < 0f ? degrees : -degrees;
        }

        static int PosePriority(SkillHit? skill) => skill switch
        {
            null => 2,
            SkillHit.SwordTyphoon => 0,
            _ => 1
        };

        void LateUpdate()
        {
            if (health.IsTimeStopped) return;
            RestoreRotation();
            if (sprite == null) return;
            if (pendingTyphoonImpact)
            {
                pendingTyphoonImpact = false;
                if (!health.IsDead)
                    SpawnImpact(typhoonImpactPoint, typhoonImpactScale, body: sprite, skill: SkillHit.SwordTyphoon);
            }
            if (defeated)
            {
                DrawDefeat();
                return;
            }
            if (health.IsDead || duration <= 0f) return;
            double now = health.UnscaledActionTime;
            float progress = (float)((now - startedAt) / duration);
            if (progress >= 1f) return;
            if (now < freezeUntil && heldSprite != null) sprite.sprite = heldSprite;
            baseRotation = sprite.transform.localRotation;
            float lean = Mathf.Sin(Mathf.PI * progress) * (1f - progress) * tilt * 2f;
            appliedRotation = baseRotation * Quaternion.Euler(0f, 0f, lean);
            sprite.transform.localRotation = appliedRotation;
            rotated = true;
        }

        internal void PlayDefeat(Vector3 offset, float reaction, float fade, float degrees, float drop, float finalHeight,
            DefeatBurstStyle burst, Vector3 away)
        {
            if (sprite == null || defeated) return;
            RestoreRotation();
            defeated = true;
            heldSprite = sprite.sprite;
            defeatHoldUntil = health.UnscaledActionTime + DefeatHitStop;
            defeatAt = (float)health.ActionTime;
            fallTime = reaction;
            fadeTime = fade;
            defeatTilt = degrees;
            sink = drop;
            height = finalHeight;
            defeatOffset = offset;
            restingPosition = sprite.transform.localPosition;
            restingRotation = sprite.transform.localRotation;
            restingScale = sprite.transform.localScale;
            DefeatPixelBurst.Spawn(sprite, burst, away);
        }

        internal static float DefeatMotionProgress(float elapsed, float duration) =>
            Mathf.Clamp01((elapsed - DefeatHitStop) / Mathf.Max(.001f, duration - DefeatHitStop));

        void DrawDefeat()
        {
            float elapsed = (float)health.ActionTime - defeatAt;
            bool holding = health.UnscaledActionTime < defeatHoldUntil;
            if (holding && defeatTilt != 0f && heldSprite != null) sprite.sprite = heldSprite;
            float motionProgress = holding ? 0f : DefeatMotionProgress(elapsed, fallTime);
            float fall = Mathf.SmoothStep(0f, 1f, motionProgress);
            float slide = 1f - Mathf.Pow(1f - motionProgress, 3f);
            Transform body = sprite.transform;
            Vector3 motion = defeatOffset * slide + Vector3.down * (sink * fall);
            body.localPosition = restingPosition + body.parent.InverseTransformVector(motion);
            // A zero tilt leaves the pufferfish's authored Die clip in control of its pose.
            if (defeatTilt != 0f)
            {
                body.localRotation = restingRotation * Quaternion.Euler(0f, 0f, defeatTilt * fall);
                body.localScale = Vector3.Scale(restingScale, new Vector3(1f, Mathf.Lerp(1f, height, fall), 1f));
            }
            sprite.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, (elapsed - fallTime) / fadeTime));
        }

        void OnDisable() => RestoreRotation();

        void RestoreRotation()
        {
            if (!rotated) return;
            // Some monsters write their own reaction rotation before this component runs.
            if (sprite != null && sprite.transform.localRotation == appliedRotation)
                sprite.transform.localRotation = baseRotation;
            rotated = false;
        }

        internal void ShowSkillImpact(SkillHit skill, Vector3 point, float scale)
        {
            if (skill == SkillHit.SwordTyphoon)
            {
                // Several ticks can be resolved in one frame after a hitch. Show a single pulse.
                pendingTyphoonImpact = true;
                typhoonImpactPoint = point;
                typhoonImpactScale = scale;
                return;
            }
            SpawnImpact(point, scale, body: sprite, skill: skill);
        }

        internal static void SpawnImpact(Vector3 point, float scale) => SpawnImpact(point, scale, null);

        static void SpawnImpact(Vector3 point, float scale, SpriteRenderer body, SkillHit? skill = null)
        {
            if (impactPrefab == null) impactPrefab = Resources.Load<ParticleSystem>("AzureSlashImpact");
            if (impactPrefab == null) return;
            var impact = Instantiate(impactPrefab, point, Quaternion.identity);
            if (body != null)
            {
                var renderer = impact.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 1;
            }
            impact.name = skill switch
            {
                SkillHit.AzureSlash => "Azure Slash Hit Impact",
                SkillHit.CloneSlash => "Clone Slash Hit Impact",
                SkillHit.SwordTyphoon => "Sword Typhoon Hit Impact",
                _ => "Basic Hit Impact"
            };
            impact.transform.localScale *= scale;
            var main = impact.main;
            main.stopAction = ParticleSystemStopAction.None;
            impact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color32(0x35, 0xb9, 0xff, 0xd8), new Color32(0xe0, 0xf8, 0xff, 0xf0));
            if (skill == SkillHit.CloneSlash)
                main.startColor = new ParticleSystem.MinMaxGradient(new Color32(0x50, 0xb9, 0xff, 0xf0),
                    new Color32(0xc3, 0xf0, 0xff, 0xff));
            if (skill == SkillHit.SwordTyphoon)
            {
                main.startColor = new ParticleSystem.MinMaxGradient(new Color32(0x83, 0xd9, 0xff, 0xc0),
                    new Color32(0xe8, 0xfb, 0xff, 0xf4));
                main.startLifetime = .13f;
                main.startSpeed = 1.4f;
                var emission = impact.emission;
                var burst = emission.GetBurst(0);
                burst.count = new ParticleSystem.MinMaxCurve(3f);
                emission.SetBurst(0, burst);
            }
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            impact.Play();
            if (skill is SkillHit.AzureSlash or SkillHit.CloneSlash)
            {
                float flashSize = skill == SkillHit.AzureSlash ? .39f : .25f;
                impact.Emit(new ParticleSystem.EmitParams
                {
                    velocity = Vector3.zero,
                    startColor = Color.white,
                    startSize = flashSize,
                    startLifetime = .055f
                }, 1);
            }
            main.stopAction = ParticleSystemStopAction.Destroy;
        }
    }
}
