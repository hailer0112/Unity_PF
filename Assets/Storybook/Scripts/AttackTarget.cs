using UnityEngine;
namespace Storybook
{
    public abstract class AttackTarget : MonoBehaviour
    {
        public virtual bool CanTakeDamage => isActiveAndEnabled;
        public abstract void ReceiveDamage(float damage);

        internal static AttackTarget FromDamageCollider(Collider collider, out bool useSurface)
        {
            useSurface = collider.TryGetComponent<DragonKingHurtbox>(out _);
            if (collider.isTrigger && !useSurface) return null;
            return collider.GetComponentInParent<AttackTarget>();
        }
    }
}
