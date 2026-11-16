using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed class PalaceRecovery : MonoBehaviour
    {
        [SerializeField] GameObject readyLight;
        [SerializeField] SpriteRenderer fountain;
        [SerializeField] Material spentFountain;
        [SerializeField] LineRenderer ripple;
        [SerializeField, Min(.1f)] float interactionRange = 1.25f;

        const float RippleDuration = .6f;
        const int RippleSegments = 48;
        PlayerHealth hero;
        HongGildong controls;
        PalaceDungeonRoom room;
        float fraction;
        float rippleTime;
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
            if (fountain != null && spentFountain != null) fountain.sharedMaterial = spentFountain;
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
            if (Keyboard.current?.fKey.wasPressedThisFrame == true) TryUse();
            if (ripple == null || !ripple.enabled) return;

            rippleTime += Time.deltaTime;
            float progress = Mathf.Clamp01(rippleTime / RippleDuration);
            if (progress == 1f) ripple.enabled = false;
            else UpdateRipple(progress);
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
