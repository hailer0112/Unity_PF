using System.Collections;
using UnityEngine;

namespace Storybook
{
    public sealed class PalaceRewardChest : MonoBehaviour
    {
        [SerializeField] SpriteRenderer artwork;
        [SerializeField] Sprite openingSprite;
        [SerializeField] Sprite openedSprite;
        [SerializeField] Texture2D fragmentIcon;
        [SerializeField] Sprite lifeOrb;
        [SerializeField] Sprite manaOrb;
        [SerializeField, Min(.1f)] float interactionRange = .95f;

        enum ChestState { Closed, Opening, Opened }
        ChestState state;
        GameObject pickup;
        Sprite fragmentSprite;
        PalaceChestLoot loot;

        internal bool CanOpen(Vector3 playerPosition)
            => state == ChestState.Closed && IsNear(playerPosition, transform.position, interactionRange);

        internal bool CanCollect(Vector3 playerPosition)
            => pickup != null && IsNear(playerPosition, pickup.transform.position, .8f);

        internal string PickupPrompt => loot.Prompt;

        static bool IsNear(Vector3 playerPosition, Vector3 position, float range)
        {
            Vector3 offset = playerPosition - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }

        internal void BeginOpening(PalaceChestLoot reward, Vector3 dropPosition)
        {
            if (state != ChestState.Closed) return;
            state = ChestState.Opening;
            loot = reward;
            artwork.sprite = openingSprite;
            StartCoroutine(FinishOpening(dropPosition));
        }

        IEnumerator FinishOpening(Vector3 position)
        {
            yield return new WaitForSeconds(.22f);
            state = ChestState.Opened;
            artwork.sprite = openedSprite;
            pickup = new GameObject("Chest reward pickup", typeof(SpriteRenderer));
            pickup.transform.SetParent(transform, false);
            pickup.transform.SetPositionAndRotation(position + Vector3.up * .16f, artwork.transform.rotation);
            var icon = pickup.GetComponent<SpriteRenderer>();
            icon.sprite = loot.Kind switch
            {
                PalaceChestReward.Health => lifeOrb,
                PalaceChestReward.Mana => manaOrb,
                PalaceChestReward.Relic when loot.Relic.Icon != null => loot.Relic.Icon,
                _ => fragmentSprite = Sprite.Create(fragmentIcon,
                    new Rect(0, 0, fragmentIcon.width, fragmentIcon.height), new Vector2(.5f, .5f))
            };
            float size = Mathf.Max(icon.sprite.bounds.size.x, icon.sprite.bounds.size.y);
            pickup.transform.localScale = Vector3.one * (.35f / size);
            icon.sortingOrder = 2;
        }

        internal bool TryCollect(Vector3 playerPosition, out PalaceChestLoot reward)
        {
            reward = loot;
            if (!CanCollect(playerPosition)) return false;
            Destroy(pickup);
            pickup = null;
            if (fragmentSprite != null) Destroy(fragmentSprite);
            return true;
        }

        void OnDestroy()
        {
            if (fragmentSprite != null) Destroy(fragmentSprite);
        }
    }
}
