using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Undying Ember")]
    public sealed class UndyingEmberRelic : FairyTaleRelic
    {
        internal const string RelicId = "undying-ember";
        [SerializeField, Range(0f, 1f)] float procChance = .25f;
        [SerializeField, Min(.01f)] float duration = 5f;
        [SerializeField, Min(.01f)] float tickInterval = 1f;
        [SerializeField, Min(0f)] float targetDamage = 3f;
        [SerializeField, Min(0f)] float splashDamage = 6f;
        [SerializeField, Min(0f)] float splashRadius = 1.5f;
        [SerializeField] Material emberParticles;

        internal float Duration => Mathf.Max(.01f, duration);
        internal float TickInterval => Mathf.Max(.01f, tickInterval);
        internal float TargetDamage => Mathf.Max(0f, targetDamage);
        internal float SplashDamage => Mathf.Max(0f, splashDamage);
        internal float SplashRadius => Mathf.Max(0f, splashRadius);
        internal Material EmberParticles => emberParticles;
        float ProcChance => Mathf.Clamp01(procChance);

        public override string Description => base.Description?
            .Replace("{chance}", ProcChance.ToString("0.##%", CultureInfo.InvariantCulture))
            .Replace("{duration}", Number(Duration))
            .Replace("{interval}", TickInterval == 1f ? "매초" : $"{Number(TickInterval)}초마다")
            .Replace("{damage}", Number(TargetDamage))
            .Replace("{radius}", Number(SplashRadius))
            .Replace("{splash}", Number(SplashDamage));

        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        internal override void OnBasicAttackHit(PlayerHealth player, MonsterHealth target)
        {
            if (ProcChance > 0f && (ProcChance >= 1f || Random.value < ProcChance))
                UndyingEmberBurn.Apply(player, target, this);
        }
    }
}
