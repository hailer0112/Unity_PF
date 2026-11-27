using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Deceiver's Heart")]
    public sealed class DeceiversHeartRelic : FairyTaleRelic
    {
        [SerializeField] string effectName = "죽음의 기만";
        [SerializeField, Range(.01f, 1f)] float healthFraction = .25f;
        [SerializeField, Min(0f)] float invulnerabilityDuration = 2f;
        [SerializeField, Min(.1f)] float visualDuration = .8f;
        [SerializeField, Min(.01f)] float visualHeight = .46f;
        [SerializeField, Min(0f)] float headHeight = 1.13f;
        [SerializeField] ParticleSystem recoverySparks;
        [SerializeField] Material shardMaterial;

        internal override string CategoryLabel => "소모형 패시브 유물";
        internal string EffectName => effectName;
        internal float HealthFraction => Mathf.Clamp(healthFraction, .01f, 1f);
        internal float InvulnerabilityDuration => Mathf.Max(0f, invulnerabilityDuration);
        internal float VisualDuration => Mathf.Max(.1f, visualDuration);
        internal float VisualHeight => Mathf.Max(.01f, visualHeight);
        internal float HeadHeight => Mathf.Max(0f, headHeight);
        internal ParticleSystem RecoverySparks => recoverySparks;
        internal Material ShardMaterial => shardMaterial;

        public override string Description => base.Description?
            .Replace("{health}", HealthFraction.ToString("0.##%", CultureInfo.InvariantCulture))
            .Replace("{duration}", InvulnerabilityDuration.ToString("G", CultureInfo.InvariantCulture));

        internal override string DescribeActiveEffect(PlayerHealth player) => effectName;
    }
}
