using UnityEngine;
using UnityEngine.InputSystem;

namespace Storybook
{
    public sealed class PalaceRecovery : MonoBehaviour
    {
        [SerializeField] GameObject readyLight;
        [SerializeField, Min(.1f)] float interactionRange = 1.25f;

        PalaceDungeonRoom room;
        PlayerHealth hero;
        float fraction;
        public bool Used { get; private set; }

        internal void Initialize(PalaceDungeonRoom owner, PlayerHealth player, float healingFraction)
        {
            room = owner;
            hero = player;
            fraction = Mathf.Clamp01(healingFraction);
        }

        bool InRange => room != null && room.Visited && hero != null && !hero.IsDead &&
            Vector3.ProjectOnPlane(hero.transform.position - transform.position, Vector3.up).sqrMagnitude
            <= interactionRange * interactionRange;

        internal string Prompt => !Used && InRange
            ? hero.CurrentHP < hero.MaxHP ? $"[F]  체력 회복 +{Mathf.RoundToInt(fraction * 100f)}%" : "체력이 이미 가득 찼습니다"
            : null;

        internal bool TryUse()
        {
            if (Used || !InRange || !hero.TryHeal(hero.MaxHP * fraction)) return false;
            Used = true;
            if (readyLight != null) readyLight.SetActive(false);
            return true;
        }

        void Update()
        {
            if (Keyboard.current?.fKey.wasPressedThisFrame == true) TryUse();
        }
    }
}
