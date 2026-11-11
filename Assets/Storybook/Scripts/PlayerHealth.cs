using System;
using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(HongGildong))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] float maxHP = 100;
        [SerializeField] float currentHP;
        HongGildong player;
        public float CurrentHP => currentHP;
        public float MaxHP => maxHP;
        public bool IsDead => currentHP <= 0;
        public event Action Damaged, Died;
        internal bool ChapterClearProtected { get; set; }
        void Awake() { player=GetComponent<HongGildong>();currentHP=maxHP; }
        public bool TryTakeDamage(float damage)
        {
            if(!isActiveAndEnabled || IsDead || ChapterClearProtected || player.IsInvulnerable || damage<=0 || float.IsNaN(damage) || float.IsInfinity(damage))return false;
            currentHP=Mathf.Max(0,currentHP-damage);
            if(IsDead){player.enabled=false;Died?.Invoke();}else Damaged?.Invoke();
            return true;
        }
    }
}
