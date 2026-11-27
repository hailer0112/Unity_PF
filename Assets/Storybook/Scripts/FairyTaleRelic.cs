using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Fairy Tale Relic")]
    public class FairyTaleRelic : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, TextArea] string description;
        [SerializeField, TextArea] string flavorText;
        [SerializeField] string sourceStory;
        [SerializeField] Sprite icon;

        public string Id => id?.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public virtual string Description => description;
        internal virtual string CategoryLabel => "패시브 유물";
        public string FlavorText => flavorText;
        public string SourceStory => sourceStory;
        public Sprite Icon => icon;
        internal virtual Sprite GetOwnedIcon(PlayerHealth player) => Icon;

        internal virtual void OnBasicAttackHit(PlayerHealth player, MonsterHealth target) { }

        internal virtual string DescribeActiveEffect(PlayerHealth player) => string.Empty;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static FairyTaleRelic CreateForTesting(string testId)
        {
            var relic = CreateInstance<FairyTaleRelic>();
            relic.id = testId;
            return relic;
        }
#endif

        internal virtual decimal ManaCostReduction => 0m;

        protected internal virtual float GetAttackBonus(PlayerHealth player) => 0f;

        protected internal virtual void OnAcquired(PlayerHealth player) { }
    }
}
