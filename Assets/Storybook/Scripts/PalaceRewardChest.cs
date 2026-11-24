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
        [SerializeField] Texture2D pickupGlow;
        [SerializeField] ParticleSystem pickupSparks;
        [SerializeField, Min(.1f)] float interactionRange = .95f;

        enum ChestState { Closed, Opening, Opened }
        ChestState state;
        PalaceRewardPickup pickup;
        PalaceChestLoot loot;

        internal bool CanOpen(Vector3 playerPosition)
            => state == ChestState.Closed && PalaceRewardPickup.IsNear(playerPosition, transform.position, interactionRange);

        internal PalaceRewardPickup PickupInRange(Vector3 playerPosition)
            => pickup != null && pickup.CanCollect(playerPosition) ? pickup : null;

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
            pickup = SpawnReward(transform, position, loot);
        }

        internal PalaceRewardPickup SpawnReward(Transform parent, Vector3 position, PalaceChestLoot reward)
        {
            Sprite sprite = reward.Kind switch
            {
                PalaceChestReward.Health => lifeOrb,
                PalaceChestReward.Mana => manaOrb,
                PalaceChestReward.Relic => reward.Relic.Icon,
                _ => null
            };
            bool ownsSprite = sprite == null;
            if (ownsSprite)
                sprite = Sprite.Create(fragmentIcon,
                    new Rect(0, 0, fragmentIcon.width, fragmentIcon.height), new Vector2(.5f, .5f));
            return PalaceRewardPickup.Spawn(parent, position, artwork.transform.rotation, reward, sprite, pickupGlow, pickupSparks, ownsSprite);
        }

    }
}
