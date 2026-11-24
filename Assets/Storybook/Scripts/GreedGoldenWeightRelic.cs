using System;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Greed Golden Weight")]
    public sealed class GreedGoldenWeightRelic : FairyTaleRelic
    {
        internal const string RelicId = "greed-golden-weight";

        internal override string DescribeActiveEffect(PlayerHealth player)
            => $"현재 이야기 파편   {player.StoryFragments:N0}개\n공격력 증가량   +{GetAttackBonus(player):P0}";

        protected internal override float GetAttackBonus(PlayerHealth player)
            => Math.Min(player.StoryFragments / 100L, 5L) * .05f;
    }
}
