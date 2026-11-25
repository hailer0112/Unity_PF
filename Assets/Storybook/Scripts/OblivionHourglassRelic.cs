using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Oblivion Hourglass")]
    public sealed class OblivionHourglassRelic : FairyTaleRelic
    {
        const float WaveLifetime = .30f;

        [SerializeField] string effectName;
        [SerializeField, Min(0f)] float cooldownReduction = 1f;
        [SerializeField] Material sandMaterial;

        internal float CooldownReduction => Mathf.Max(0f, cooldownReduction);
        string ReductionText => CooldownReduction.ToString("G", CultureInfo.InvariantCulture) + "초";

        public override string Description => base.Description?.Replace("{reduction}", ReductionText);

        internal override string DescribeActiveEffect(PlayerHealth player) =>
            $"{effectName}\n다른 스킬의 남은 쿨타임 감소   {ReductionText}";

        internal void PlayTimeDistortion(Transform caster)
        {
            if (sandMaterial == null) return;

            var wave = new GameObject("Hourglass time distortion");
            wave.transform.SetParent(caster, false);
            wave.transform.SetPositionAndRotation(caster.position + Vector3.up * .16f, Quaternion.Euler(90f, 0f, 0f));

            var particles = wave.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = .03f;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = WaveLifetime;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.15f, 1.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .076f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(.48f, .88f, .26f, .78f), new Color(.86f, 1f, .50f, .88f));

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .25f;
            shape.radiusThickness = .22f;
            shape.sphericalDirectionAmount = 1f;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)22) });

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.85f, .35f), new GradientAlphaKey(0f, 1f) });
            var color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = fade;

            var renderer = wave.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sandMaterial;
            renderer.sortingOrder = 3;
            particles.Play();
        }
    }
}
