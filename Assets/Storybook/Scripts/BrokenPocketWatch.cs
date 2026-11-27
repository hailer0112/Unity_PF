using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Broken Pocket Watch")]
    public sealed class BrokenPocketWatch : FairyTaleRelic
    {
        [SerializeField, Range(0f, 1f)] float chance = .10f;
        [SerializeField, Min(.01f)] float stopDuration = 2f;
        [SerializeField, Min(0f)] float cooldown = 5f, targetImmunity = 2f;
        [SerializeField] Sprite clockIcon;

        public override string Description => base.Description?
            .Replace("{chance}", Mathf.Clamp01(chance).ToString("0.##%"))
            .Replace("{stop}", Mathf.Max(.01f, stopDuration).ToString("0.##"))
            .Replace("{cooldown}", Mathf.Max(0f, cooldown).ToString("0.##"));

        internal override void OnBasicAttackHit(PlayerHealth player, MonsterHealth target)
        {
            double now = Time.timeAsDouble;
            if (player.IsDead || now < player.PocketWatchReadyAt || !target.CanTimeStop) return;
            float rate = Mathf.Clamp01(chance);
            if (rate <= 0f || (rate < 1f && Random.value >= rate)) return;
            if (target.TryTimeStop(Mathf.Max(.01f, stopDuration), Mathf.Max(0f, targetImmunity), clockIcon))
                player.PocketWatchReadyAt = now + Mathf.Max(0f, cooldown);
        }
    }
}
