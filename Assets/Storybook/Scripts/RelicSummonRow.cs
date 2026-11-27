using System;
using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    internal sealed class RelicSummonRow : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] Text title, description, availability, spawnLabel, acquireLabel;
        [SerializeField] Button spawn, acquire;

        internal FairyTaleRelic Relic { get; private set; }

        internal void Bind(FairyTaleRelic relic, Font font, ColorBlock colors, Action summon, Action grant)
        {
            Relic = relic;
            gameObject.name = relic.DisplayName;
            title.font = description.font = availability.font = spawnLabel.font = acquireLabel.font = font;
            title.text = relic.DisplayName;
            description.text = relic.Description;
            var descriptionRect = description.rectTransform;
            float height = Mathf.Ceil(description.preferredHeight) + 4f;
            descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(106f, height + 51f));

            spawn.colors = acquire.colors = colors;
            spawn.onClick.AddListener(() => summon());
            acquire.onClick.AddListener(() => grant());
        }

        internal void Refresh(bool owned, string blocked, Sprite displayIcon)
        {
            icon.preserveAspect = true;
            icon.sprite = displayIcon;
            icon.enabled = displayIcon != null;
            availability.resizeTextForBestFit = true;
            availability.resizeTextMinSize = 9;
            availability.resizeTextMaxSize = 12;
            availability.text = owned ? "보유 중 · 중복 획득 불가" :
                blocked != null ? $"미보유\n{blocked}" : "미보유";
            acquire.interactable = blocked == null;
        }
    }
}
