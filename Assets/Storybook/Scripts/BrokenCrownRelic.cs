using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Broken Crown")]
    public sealed class BrokenCrownRelic : FairyTaleRelic
    {
        [SerializeField] string effectName;
        [SerializeField, Min(0f)] float damageBonus = .5f;
        [SerializeField] ParticleSystem impactSparks;

        internal float DamageBonus => Mathf.Max(0f, damageBonus);

        public override string Description
            => base.Description?.Replace("{bonus}", DamageBonus.ToString("0.##%", CultureInfo.InvariantCulture));

        internal override string DescribeActiveEffect(PlayerHealth player) => effectName;

        internal void ShowImpact(MonsterHealth target)
        {
            if (Icon != null) BrokenCrownImpact.Play(target, Icon, impactSparks);
        }
    }
}
