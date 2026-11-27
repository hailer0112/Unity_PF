using System;
using UnityEngine;

namespace Storybook
{
    internal static class ShopPurchase
    {
        internal static string BlockReason(ShopPedestal stand, PlayerHealth health, HongGildong hero, int price)
        {
            if (stand == null || health == null || hero == null || health.IsDead || !hero.CanPurchaseFromShop)
                return "지금은 구매할 수 없습니다.";
            if (stand.IsSold) return "이미 판매된 상품입니다.";
            if (stand.PurchaseInProgress) return "상품을 처리 중입니다.";
            var product = stand.Display != null ? stand.Display.Product : null;
            if (product == null || string.IsNullOrEmpty(product.Id) || price < 0) return "구매할 수 있는 상품이 없습니다.";
            if (health.StoryFragments < price) return "이야기 파편이 부족합니다.";

            switch (product.Kind)
            {
                case ShopProductKind.HealthOrb:
                    if (health.CurrentHP >= health.MaxHP) return "생명력이 가득 찼습니다.";
                    break;
                case ShopProductKind.ManaOrb:
                    if (hero.CurrentMP >= hero.MaxMP) return "마력이 가득 찼습니다.";
                    break;
                case ShopProductKind.Relic:
                    if (product.Relic == null || string.IsNullOrEmpty(product.RelicId)) return "유물 정보를 확인할 수 없습니다.";
                    if (health.Relics.Contains(product.RelicId)) return "이미 보유 중인 유물입니다.";
                    if (health.Relics.IsFull) return "더 이상 유물을 보유할 수 없습니다.";
                    break;
                default:
                    return "구매할 수 있는 상품이 없습니다.";
            }
            return null;
        }

        internal static bool TryBuy(ShopPedestal stand, PlayerHealth health, HongGildong hero, out string message)
        {
            var product = stand != null && stand.Display != null ? stand.Display.Product : null;
            int price = product != null ? product.Price : 0;
            message = BlockReason(stand, health, hero, price);
            if (message != null) return false;

            float recovered = 0f;
            stand.PurchaseInProgress = true;
            try
            {
                if (!health.TrySpendStoryFragments(price, ApplyProduct))
                {
                    message = "상품 효과를 적용할 수 없습니다.";
                    return false;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, stand);
                message = "상품 효과를 적용할 수 없습니다.";
                return false;
            }

            finally
            {
                stand.PurchaseInProgress = false;
            }

            stand.CompleteSale(product.Id);
            message = product.Kind switch
            {
                ShopProductKind.HealthOrb => $"HP +{recovered:0.##}",
                ShopProductKind.ManaOrb => $"MP +{recovered:0.##}",
                _ => $"{product.DisplayName} 획득"
            };
            return true;

            bool ApplyProduct()
            {
                switch (product.Kind)
                {
                    case ShopProductKind.HealthOrb:
                        float before = health.CurrentHP;
                        if (!health.TryHeal(health.MaxHP * product.RecoveryFraction)) return false;
                        recovered = health.CurrentHP - before;
                        return recovered > 0f;
                    case ShopProductKind.ManaOrb:
                        float amount = hero.MaxMP * product.RecoveryFraction;
                        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
                        recovered = hero.RestoreMana(amount);
                        return recovered > 0f;
                    default:
                        return health.Relics.TryAcquire(product.Relic);
                }
            }
        }
    }
}
