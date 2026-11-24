using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Blood Temptation")]
    public sealed class BloodTemptationRelic : FairyTaleRelic
    {
        internal const string RelicId = "blood-temptation";
        internal const float ProcChance = .25f;
        [SerializeField] Material poisonParticles;

        internal override void OnBasicAttackHit(PlayerHealth player, MonsterHealth target)
        {
            if (Random.value < ProcChance) BloodTemptationPoison.Apply(player, target, poisonParticles);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void ForcePoisonForTesting(PlayerHealth player, MonsterHealth target)
            => BloodTemptationPoison.Apply(player, target, poisonParticles);
#endif
    }
}
