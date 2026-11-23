using UnityEngine;

namespace Storybook
{
    [CreateAssetMenu(menuName = "Storybook/Fairy Tale Relic")]
    public class FairyTaleRelic : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField, TextArea] string description;
        [SerializeField] string sourceStory;
        [SerializeField] Sprite icon;

        public string Id => id?.Trim();
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public string SourceStory => sourceStory;
        public Sprite Icon => icon;

        protected internal virtual void OnAcquired(PlayerHealth player) { }
    }
}
