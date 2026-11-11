using System;
using System.Collections;
using UnityEngine;
namespace Storybook
{
    public sealed class MonsterHealth : AttackTarget
    {
        [SerializeField, Min(1)] float maxHP = 100;
        [SerializeField] float currentHP;
        [SerializeField, Min(0)] float deathDelay = .55f;
        [SerializeField] bool despawnOnDeath = true;
        public float MaxHP => maxHP;
        public float CurrentHP => currentHP;
        public bool IsDead => currentHP <= 0;
        public override bool CanTakeDamage => base.CanTakeDamage && !IsDead;
        public event Action Damaged;
        public event Action Died;
        void Awake() { currentHP = maxHP; }
        public override void ReceiveDamage(float damage)
        {
            if (!CanTakeDamage || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            currentHP = Mathf.Max(0, currentHP - damage);
            if (!IsDead) { Damaged?.Invoke(); return; }
            foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            Died?.Invoke();
            if (despawnOnDeath) StartCoroutine(DisableAfterDeath());
        }
        IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(deathDelay);
            gameObject.SetActive(false);
        }
    }
}
