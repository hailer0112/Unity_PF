using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Silent Pearl")]
    public sealed class SilentPearlRelic : FairyTaleRelic
    {
        [SerializeField] string effectName;
        [SerializeField, Range(0f, 1f)] float manaCostReduction = .20f;
        [SerializeField] ParticleSystem acquisitionSparks;

        internal override decimal ManaCostReduction => (decimal)Mathf.Clamp01(manaCostReduction);

        public override string Description => base.Description?
            .Replace("{reduction}", Percent(ManaCostReduction));

        internal override string DescribeActiveEffect(PlayerHealth player)
        {
            var hero = player.GetComponent<HongGildong>();
            return $"{effectName}\n현재 MP 소모량 감소율   {Percent(hero.SkillManaCostReduction)}";
        }

        static string Percent(decimal rate) => rate.ToString("0.##%", CultureInfo.InvariantCulture);

        protected internal override void OnAcquired(PlayerHealth player)
        {
            if (acquisitionSparks == null) return;
            Transform anchor = player.transform;
            var sparks = Instantiate(acquisitionSparks, anchor.position + Vector3.up * .45f,
                Quaternion.identity, anchor);
            var main = sparks.main;
            main.startColor = new Color(.64f, .84f, 1f);
            sparks.Play();
        }
    }
}
