using UnityEngine;
namespace Storybook
{
    public abstract class AttackTarget : MonoBehaviour
    {
        public virtual bool CanTakeDamage => isActiveAndEnabled;
        public abstract void ReceiveDamage(float damage);
    }
}
