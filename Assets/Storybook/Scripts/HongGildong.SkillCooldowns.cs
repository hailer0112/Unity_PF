using System;
using UnityEngine;

namespace Storybook
{
    public sealed partial class HongGildong
    {
        WishEyeVisual wishVisual;

        void NotifySkillCast(HudSkill usedSkill, float spentMana)
        {
            var relics = AttackRelics;
            if (relics == null) return;

            var hourglass = relics.Find<OblivionHourglassRelic>();
            if (hourglass != null && hourglass.CooldownReduction > 0f)
                ApplyTimeDistortion(usedSkill, hourglass);

            var eye = relics.Find<WishDevouringEyeRelic>();
            if (eye == null) return;
            var wish = eye.RollWish();
            if (!wish.HasValue) return;

            bool recovered = false;
            switch (wish.Value)
            {
                case WishKind.Life:
                    recovered = manaHealth.TryHeal(manaHealth.MaxHP * eye.HealFraction);
                    break;
                case WishKind.Mana:
                    recovered = RestoreMana(spentMana * eye.RefundFraction) > 0f;
                    break;
                case WishKind.Time:
                    ResetWishedCooldown(usedSkill);
                    break;
            }

            ClearWishVisual();
            wishVisual = WishEyeVisual.Show(transform, body, idleFront, fieldCamera, eye, wish.Value, recovered);
        }

        void ClearWishVisual()
        {
            if (wishVisual != null) wishVisual.Remove();
            wishVisual = null;
        }

        void ResetWishedCooldown(HudSkill skill)
        {
            switch (skill)
            {
                case HudSkill.AzureSlash: azureSlashReadyAt = 0d; break;
                case HudSkill.CloneSlash: cloneSlashReadyAt = 0d; break;
                case HudSkill.Windstep:
                    windstepReadyAt = 0d;
                    windstepWishRefreshAvailable = true;
                    break;
                case HudSkill.SwordTyphoon: swordTyphoonReadyAt = 0d; break;
            }
            if (TryGetComponent(out HongGildongHud hud)) hud.FlashReducedCooldown(skill);
        }

        void ApplyTimeDistortion(HudSkill usedSkill, OblivionHourglassRelic hourglass)
        {

            double now = Time.timeAsDouble;
            float reduction = hourglass.CooldownReduction;
            var hud = GetComponent<HongGildongHud>();
            bool reduced = ReduceOtherCooldown(HudSkill.AzureSlash, ref azureSlashReadyAt)
                | ReduceOtherCooldown(HudSkill.CloneSlash, ref cloneSlashReadyAt)
                | ReduceOtherCooldown(HudSkill.Windstep, ref windstepReadyAt)
                | ReduceOtherCooldown(HudSkill.SwordTyphoon, ref swordTyphoonReadyAt);
            if (reduced) hourglass.PlayTimeDistortion(transform);

            bool ReduceOtherCooldown(HudSkill skill, ref double readyAt)
            {
                if (skill == usedSkill || readyAt <= now) return false;
                readyAt = Math.Max(now, readyAt - reduction);
                if (hud != null) hud.FlashReducedCooldown(skill);
                return true;
            }
        }
    }
}
