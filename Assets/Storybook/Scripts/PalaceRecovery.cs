using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed class PalaceRecovery : MonoBehaviour
    {
        [SerializeField] GameObject readyLight;
        [SerializeField] SpriteRenderer fountain;
        [SerializeField] LineRenderer ripple;
        [SerializeField, Min(.1f)] float interactionRange = 1.25f;

        const float RippleDuration = .6f;
        const float WaterFadeDuration = 1.1f;
        const int RippleSegments = 48;
        static readonly int WaterFlow = Shader.PropertyToID("_WaterFlow");
        static readonly int WaterSparks = Shader.PropertyToID("_WaterSparks");
        PlayerHealth hero;
        HongGildong controls;
        PalaceDungeonRoom room;
        float fraction;
        float rippleTime;
        float waterFlow = 1f;
        MaterialPropertyBlock fountainProperties;
        public bool Used { get; private set; }

        internal void Initialize(PalaceDungeonRoom owner, PlayerHealth player, float healingFraction)
        {
            room = owner;
            hero = player;
            controls = hero != null ? hero.GetComponent<HongGildong>() : null;
            fraction = Mathf.Clamp01(healingFraction);
        }

        bool CanInteract => !Used && room != null && room.Visited && hero != null && !hero.IsDead &&
            controls != null && controls.isActiveAndEnabled && !StorybookTransition.IsBusy &&
            Vector3.ProjectOnPlane(hero.transform.position - transform.position, Vector3.up).sqrMagnitude
            <= interactionRange * interactionRange;

        internal string Prompt => CanInteract ? "F — 정화의 샘 사용" : null;

        internal bool TryUse()
        {
            if (!CanInteract || (hero.CurrentHP >= hero.MaxHP && controls.CurrentMP >= controls.MaxMP)) return false;

            Used = true;
            hero.TryHeal(hero.MaxHP * fraction);
            controls.RefillMana();
            if (readyLight != null) readyLight.SetActive(false);
            if (fountain != null)
            {
                fountainProperties = new MaterialPropertyBlock();
                fountain.GetPropertyBlock(fountainProperties);
                fountainProperties.SetFloat(WaterSparks, 0f);
                UpdateWater();
            }
            if (ripple != null)
            {
                ripple.positionCount = RippleSegments;
                UpdateRipple(0f);
                ripple.enabled = true;
            }
            return true;
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            if (!Used && Keyboard.current?.fKey.wasPressedThisFrame == true) TryUse();
            if (fountainProperties != null && waterFlow > 0f)
            {
                waterFlow = Mathf.MoveTowards(waterFlow, 0f, Time.deltaTime / WaterFadeDuration);
                UpdateWater();
            }

            if (ripple == null || !ripple.enabled) return;

            rippleTime += Time.deltaTime;
            float progress = Mathf.Clamp01(rippleTime / RippleDuration);
            if (progress == 1f) ripple.enabled = false;
            else UpdateRipple(progress);
        }

        void UpdateWater()
        {
            fountainProperties.SetFloat(WaterFlow, Mathf.SmoothStep(0f, 1f, waterFlow));
            fountain.SetPropertyBlock(fountainProperties);
        }

        void LateUpdate()
        {
            if (fountain != null && hero != null)
                fountain.sortingOrder = hero.transform.position.z > transform.position.z ? 3 : -1;
        }

        void UpdateRipple(float progress)
        {
            float radius = Mathf.Lerp(.52f, 2.1f, Mathf.SmoothStep(0f, 1f, progress));
            for (int i = 0; i < RippleSegments; i++)
            {
                float angle = i * (Mathf.PI * 2f / RippleSegments);
                ripple.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            ripple.startWidth = ripple.endWidth = Mathf.Lerp(.055f, .016f, progress);
            ripple.startColor = ripple.endColor = new Color(.48f, 1f, .93f, 1f - progress);
        }
    }
}
