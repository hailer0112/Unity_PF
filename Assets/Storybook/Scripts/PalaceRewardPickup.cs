using UnityEngine;

namespace Storybook
{
    public sealed class PalaceRewardPickup : MonoBehaviour
    {
        PalaceChestLoot loot;
        Sprite runtimeSprite;
        PalacePickupVisual effects;
        ParticleSystem pickupSparks;
        bool collected;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool rejectOwnedRelic;
        internal void PreventDuplicateRelicForTesting() => rejectOwnedRelic = true;
#endif

        internal string Prompt(FairyTaleRelicInventory relics) => BlockReason(relics) ?? loot.Prompt;

        string BlockReason(FairyTaleRelicInventory relics)
        {
            if (loot.Kind != PalaceChestReward.Relic) return null;
            if (relics.IsFull) return FairyTaleRelicInventory.FullPrompt;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (rejectOwnedRelic && relics.Contains(loot.Relic.Id)) return "이미 보유한 유물입니다.";
#endif
            return null;
        }

        internal static PalaceRewardPickup Spawn(Transform parent, Vector3 position, Quaternion rotation,
            PalaceChestLoot loot, Sprite sprite, Texture2D glow, ParticleSystem pickupSparks, bool ownsSprite)
        {
            var root = new GameObject("Chest reward pickup");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);

            var icon = new GameObject("Reward sprite").AddComponent<SpriteRenderer>();
            icon.transform.SetParent(root.transform, false);
            icon.sprite = sprite;
            float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            icon.transform.localScale = Vector3.one * (.35f / size);
            icon.sortingOrder = 2;

            var pickup = root.AddComponent<PalaceRewardPickup>();
            pickup.loot = loot;
            pickup.pickupSparks = pickupSparks;
            if (ownsSprite) pickup.runtimeSprite = sprite;
            pickup.effects = PalacePickupVisual.Create(root.transform, icon.transform, glow, loot.Kind);
            return pickup;
        }

        internal bool CanCollect(Vector3 playerPosition)
            => !collected && IsNear(playerPosition, transform.position, .8f);

        internal bool TryCollect(Vector3 playerPosition, PlayerHealth health, HongGildong player,
            PalaceChestRewards rewards, out string result)
        {
            result = null;
            if (!CanCollect(playerPosition) || BlockReason(health.Relics) != null) return false;
            collected = true;
            if (!rewards.TryGrantLoot(loot, health, player, out result))
            {
                collected = false;
                return false;
            }

            effects.PlayCollection(pickupSparks);
            Destroy(gameObject);
            return true;
        }

        internal static bool IsNear(Vector3 playerPosition, Vector3 position, float range)
        {
            Vector3 offset = playerPosition - position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }

        void OnDestroy()
        {
            if (runtimeSprite != null) Destroy(runtimeSprite);
        }
    }
}
