using System.Globalization;
using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Relics/Cursed Footsteps")]
    public sealed class CursedFootstepsRelic : FairyTaleRelic
    {
        [SerializeField] string effectName = "저주의 흔적";
        [SerializeField, Min(.01f)] float duration = 3f, width = .8f;
        [SerializeField, Min(0f)] float tickDamage = 10f;
        [SerializeField, Min(.01f)] float tickInterval = 1f;
        [SerializeField, Range(0f, 1f)] float slowFraction = .3f;
        [SerializeField] Texture2D puddleFrames;
        [SerializeField] Sprite motes;
        [SerializeField, Min(.05f)] float puddlePatternSize = .52f;
        [SerializeField, Min(.01f)] float animationFps = 12f, footprintSpacing = .34f;
        [SerializeField, Min(0f)] float moteSize = .22f;
        [SerializeField, Range(0f, 1f)] float moteOpacity = .6f;

        internal float Duration => Mathf.Max(.01f, duration);
        internal float Radius => Mathf.Max(.01f, width) * .5f;
        internal float TickDamage => Mathf.Max(0f, tickDamage);
        internal float TickInterval => Mathf.Max(.01f, tickInterval);
        internal float SlowFraction => Mathf.Clamp01(slowFraction);
        internal Texture2D PuddleFrames => puddleFrames;
        internal Sprite Motes => motes;
        internal float PuddlePatternSize => Mathf.Max(.05f, puddlePatternSize);
        internal float AnimationFps => Mathf.Max(.01f, animationFps);
        internal float FootprintSpacing => Mathf.Max(.01f, footprintSpacing);
        internal float MoteSize => Mathf.Max(0f, moteSize);
        internal float MoteOpacity => Mathf.Clamp01(moteOpacity);

        public override string Description => base.Description?
            .Replace("{duration}", Format(Duration))
            .Replace("{interval}", TickInterval == 1f ? "매초" : $"{Format(TickInterval)}초마다")
            .Replace("{damage}", Format(TickDamage))
            .Replace("{slow}", SlowFraction.ToString("0.##%", CultureInfo.InvariantCulture));

        internal override string DescribeActiveEffect(PlayerHealth player) => effectName;

        protected internal override void OnAcquired(PlayerHealth player)
        {
            if (!player.TryGetComponent(out CursedFootstepsTrail trail))
                trail = player.gameObject.AddComponent<CursedFootstepsTrail>();
            trail.Initialize(player, this);
        }

        static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
