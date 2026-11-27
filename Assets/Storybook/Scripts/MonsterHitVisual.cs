using UnityEngine;
namespace Storybook
{
    [RequireComponent(typeof(MonsterHealth))]
    public sealed class MonsterHitVisual : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] Renderer[] flashRenderers;
        MonsterHealth health;
        Quaternion restRotation;
        Vector3 restScale;
        float hitTime = 1, deathTime;
        bool dying;
        MaterialPropertyBlock block;
        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            restRotation = visualRoot.localRotation;
            restScale = visualRoot.localScale;
            block = new MaterialPropertyBlock();
        }
        void OnEnable() { health.Damaged += Hit; health.Died += Die; }
        void OnDisable() { health.Damaged -= Hit; health.Died -= Die; }
        void Hit() { hitTime = 0; }
        void Die() { dying = true; deathTime = 0; }
        void Update()
        {
            if (health.IsTimeStopped) return;
            if (dying)
            {
                deathTime += Time.deltaTime;
                visualRoot.localRotation = restRotation * Quaternion.Euler(0, 0, Mathf.Lerp(0, 80, deathTime / .5f));
                visualRoot.localScale = restScale * Mathf.Lerp(1, .15f, deathTime / .55f);
                return;
            }
            if (hitTime >= .24f) return;
            hitTime = Mathf.Min(.24f, hitTime + Time.deltaTime);
            float t = hitTime / .24f;
            visualRoot.localRotation = restRotation * Quaternion.Euler(0, 0, Mathf.Sin(t * Mathf.PI * 2) * (1-t) * 17);
            foreach (var r in flashRenderers)
            {
                if (r is SpriteRenderer sprite) { sprite.color = Color.Lerp(new Color(1, .15f, .1f), Color.white, t); continue; }
                Color normal = r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : r.sharedMaterial.color;
                Color color = Color.Lerp(new Color(1, .15f, .1f), normal, t);
                block.SetColor("_BaseColor", color); block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }
    }
}
