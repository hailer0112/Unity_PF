using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterHealth), typeof(Animator))]
    public sealed class DragonKingPhase : MonoBehaviour
    {
        enum Phase { First, Transition, Second }

        [SerializeField, Range(1f, 1.5f)] float transitionDuration = 1.25f;
        [SerializeField] Shader corruptionShader;
        [SerializeField] SpriteRenderer[] corruptedParts;
        [SerializeField] SpriteRenderer aura, burst, tridentGlow, palmGlow;
        [SerializeField] SpriteRenderer[] embers;

        static readonly int Intensity = Shader.PropertyToID("_CorruptionIntensity");
        static readonly int Purified = Shader.PropertyToID("_Purified");
        SpriteRenderer eyeLeft, eyeRight;
        MonsterHealth health;
        Animator animator;
        Material corruption;
        Material[] originalMaterials;
        Vector3 auraSize, burstSize, tridentSize, palmSize;
        double startedAt;
        float previousAnimatorSpeed;
        Phase phase;

        internal bool IsSecondPhase => isActiveAndEnabled && phase == Phase.Second;

        internal static bool ReachedThreshold(MonsterHealth target) =>
            !target.IsDead && target.CurrentHP <= target.MaxHP * .5f;

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            animator = GetComponent<Animator>();
            corruption = new Material(corruptionShader);
            originalMaterials = new Material[corruptedParts.Length];
            for (int i = 0; i < corruptedParts.Length; i++)
                originalMaterials[i] = corruptedParts[i].sharedMaterial;

            auraSize = aura.transform.localScale;
            burstSize = burst.transform.localScale;
            tridentSize = tridentGlow.transform.localScale;
            palmSize = palmGlow.transform.localScale;
        }

        void OnEnable()
        {
            health.Died += OnBossDied;
            if (phase == Phase.Second && !health.IsDead) ApplyLook(true);
        }

        internal bool HoldAttacks(double now, bool canTransition)
        {
            if (!isActiveAndEnabled || health.IsDead) return false;

            if (phase == Phase.First && canTransition && ReachedThreshold(health))
            {
                startedAt = now;
                previousAnimatorSpeed = animator.speed;
                animator.speed = 0f;
                phase = Phase.Transition;
                ApplyLook(true);
            }

            if (phase == Phase.First) return false;
            float elapsed = (float)(now - startedAt);
            if (phase == Phase.Transition && elapsed >= transitionDuration)
            {
                animator.speed = previousAnimatorSpeed;
                phase = Phase.Second;
            }

            float progress = Mathf.Clamp01(elapsed / transitionDuration);
            UpdateLook(elapsed, progress);
            return phase == Phase.Transition;
        }

        void ApplyLook(bool empowered)
        {
            for (int i = 0; i < corruptedParts.Length; i++)
                corruptedParts[i].sharedMaterial = empowered ? corruption : originalMaterials[i];
            aura.enabled = tridentGlow.enabled = palmGlow.enabled = empowered;
            burst.enabled = empowered && phase == Phase.Transition;
            foreach (var ember in embers) ember.enabled = empowered;
        }

        void UpdateLook(float elapsed, float progress)
        {
            float surge = Mathf.Sin(progress * Mathf.PI);
            float pulse = .5f + .5f * Mathf.Sin(elapsed * 4f);
            corruption.SetFloat(Intensity, Mathf.Lerp(.2f, .82f, progress) + .54f * surge);
            SetGlow(aura, auraSize * (1f + .07f * pulse + .16f * surge), .22f + .06f * pulse + .14f * surge);
            SetGlow(tridentGlow, tridentSize * (1f + .09f * pulse), .48f + .12f * pulse);
            SetGlow(palmGlow, palmSize * (1f + .12f * pulse), .46f + .14f * pulse);

            burst.enabled = phase == Phase.Transition;
            if (burst.enabled)
                SetGlow(burst, burstSize * Mathf.Lerp(.34f, 2.3f, progress), surge * .46f);

            for (int i = 0; i < embers.Length; i++)
            {
                float age = Mathf.Repeat(elapsed * (.38f + .04f * (i % 3)) + (float)i / embers.Length, 1f);
                float angle = i * 2.39996f;
                float spread = 1f + .34f * surge;
                var ember = embers[i];
                ember.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * (1.9f + 1.15f * age) * spread,
                    -.85f + age * 3.15f + Mathf.Sin(angle) * .62f, 0f);
                Color color = ember.color;
                color.a = Mathf.Sin(age * Mathf.PI) * (.58f + surge * .30f);
                ember.color = color;
            }
        }

        static void SetGlow(SpriteRenderer sprite, Vector3 scale, float alpha)
        {
            sprite.transform.localScale = scale;
            Color color = sprite.color;
            color.a = alpha;
            sprite.color = color;
        }

        internal void BeginPurification()
        {
            ApplyLook(true);
            burst.enabled = false;
            eyeLeft = transform.Find("Visual Plane/Upper Body/Head and Beard/Left eye pulse").GetComponent<SpriteRenderer>();
            eyeRight = transform.Find("Visual Plane/Upper Body/Head and Beard/Right eye pulse").GetComponent<SpriteRenderer>();
            foreach (var ember in embers) ember.enabled = false;
        }

        internal void RenderPurification(float progress)
        {
            float surge = Mathf.Sin(Mathf.Clamp01(progress / .54f) * Mathf.PI);
            float release = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.30f, .85f, progress));
            corruption.SetFloat(Intensity, (1f - release) * (.6f + 1.15f * surge));
            corruption.SetFloat(Purified, release);
            SetGlow(aura, auraSize * (1f + .13f * surge), (.22f + .23f * surge) * (1f - release));
            SetGlow(tridentGlow, tridentSize, .48f * (1f - release));
            SetGlow(palmGlow, palmSize, .42f * (1f - release));

            for (int i = 0; i < embers.Length; i++)
            {
                float drift = Mathf.Clamp01((progress - .27f - i * .022f) / .44f);
                var ember = embers[i];
                ember.enabled = drift > 0f && drift < 1f;
                if (!ember.enabled) continue;
                float angle = i * 2.39996f;
                Vector3 origin = ember.transform.parent.TransformPoint(
                    new Vector3(Mathf.Cos(angle) * 1.45f, Mathf.Sin(angle) * .80f, 0f));
                Vector3 scatter = transform.right * Mathf.Cos(angle) * (1.5f + i * .21f) + Vector3.up * (1.1f + i * .16f);
                ember.transform.position = origin + scatter * drift;
                ember.color = Color.Lerp(new Color(.38f, .025f, .065f, 1f), new Color(.74f, .10f, .16f, 0f), drift);
            }

            Color eyes = Color.Lerp(new Color(1f, .05f, .12f, .38f + .4f * surge),
                new Color(.40f, .88f, .86f, .24f), release);
            eyeLeft.color = eyeRight.color = eyes;
            float flash = Mathf.Sin(Mathf.InverseLerp(.72f, .99f, progress) * Mathf.PI);
            burst.enabled = flash > .01f;
            burst.color = new Color(.26f, .91f, .82f, flash * .5f);
            burst.transform.localScale = burstSize * Mathf.Lerp(.8f, 1.95f, progress);
            if (progress >= 1f)
                aura.enabled = tridentGlow.enabled = palmGlow.enabled = false;
        }


        void OnBossDied()
        {
            if (!TryGetComponent(out DragonKingPurification sequence) || !sequence.isActiveAndEnabled) ClearLook();
        }

        void OnDisable()
        {
            if (health == null) return;
            health.Died -= OnBossDied;
            ClearLook();
        }

        void ClearLook()
        {
            if (phase == Phase.Transition)
            {
                animator.speed = previousAnimatorSpeed;
                phase = Phase.Second;
            }
            ApplyLook(false);
        }

        void OnDestroy()
        {
            if (corruption != null) Destroy(corruption);
        }
    }
}
