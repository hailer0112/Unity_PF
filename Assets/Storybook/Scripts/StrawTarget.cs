using UnityEngine;

namespace Storybook
{
    public sealed class StrawTarget : AttackTarget
    {
        [SerializeField] SpriteRenderer body;
        float hitTime = 1f;
        public int HitCount { get; private set; }

        public override void ReceiveDamage(float damage) { if (damage > 0) TakeHit(); }

        public void TakeHit() { HitCount++; hitTime = 0f; }

        void Update()
        {
            if (hitTime >= .24f) return;
            hitTime = Mathf.Min(.24f, hitTime + Time.deltaTime);
            float t = hitTime / .24f;
            body.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * 17f);
            body.color = Color.Lerp(new Color(1f, .4f, .26f), Color.white, t);
        }
    }
}
