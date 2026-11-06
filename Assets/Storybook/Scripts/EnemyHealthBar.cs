using UnityEngine;
namespace Storybook
{
    [DefaultExecutionOrder(100)]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] MonsterHealth health;
        [SerializeField] Camera viewCamera;
        [SerializeField] SpriteRenderer body;
        [SerializeField] Canvas barCanvas;
        [SerializeField] RectTransform fill;
        [SerializeField] Vector2 size = new Vector2(.65f,.07f);
        [SerializeField, Min(0)] float headGap = .12f;
        [SerializeField, Min(0)] float heightOverride;
        [SerializeField, Min(0)] float visibleSeconds = 3f;
        float displayHeight, hideAt;
        bool requestedVisible;
        public bool IsVisible => barCanvas != null && barCanvas.enabled;
        public float DisplayedRatio => fill != null ? fill.anchorMax.x : 0;
        void Awake()
        {
            if (health == null) health = GetComponentInParent<MonsterHealth>();
            if (body == null && health != null) body = health.GetComponentInChildren<SpriteRenderer>();
            if (viewCamera == null)
            {
                var follow = FindFirstObjectByType<ClearingCamera>();
                viewCamera = follow != null ? follow.GetComponent<Camera>() : Camera.main;
            }
            // Sample the initial upright image, never the shaking hit/death transform.
            displayHeight = body != null && body.sprite != null
                ? body.sprite.bounds.size.y * body.transform.lossyScale.y : 1f;
            barCanvas.enabled = false;
        }
        void OnEnable()
        {
            requestedVisible = false;
            barCanvas.enabled = false;
            if (health != null) { health.Damaged += OnDamaged; health.Died += Hide; }
        }
        void OnDisable()
        {
            if (health != null) { health.Damaged -= OnDamaged; health.Died -= Hide; }
            Hide();
        }
        void OnDamaged()
        {
            if (health == null || health.IsDead) { Hide(); return; }
            requestedVisible = true;
            hideAt = Time.time + visibleSeconds;
            RefreshFill();
            PositionBar();
        }
        void Hide() { requestedVisible = false; if (barCanvas != null) barCanvas.enabled = false; }
        void RefreshFill()
        {
            float ratio = health.MaxHP > 0 ? Mathf.Clamp01(health.CurrentHP / health.MaxHP) : 0;
            fill.anchorMax = new Vector2(ratio,1);
        }
        void PositionBar()
        {
            if (viewCamera == null || health == null) { barCanvas.enabled = false; return; }
            var rect = (RectTransform)barCanvas.transform;
            rect.position = health.transform.position + viewCamera.transform.up * ((heightOverride > 0 ? heightOverride : displayHeight) + headGap);
            rect.rotation = viewCamera.transform.rotation;
            rect.localScale = Vector3.one * .01f;
            rect.sizeDelta = new Vector2(Mathf.Max(.1f,size.x),Mathf.Max(.02f,size.y)) * 100f;
            barCanvas.enabled = requestedVisible && Vector3.Dot(rect.position-viewCamera.transform.position,viewCamera.transform.forward)>0;
        }
        void LateUpdate()
        {
            if (health == null || health.IsDead || !health.isActiveAndEnabled || Time.time >= hideAt) { Hide(); return; }
            if (!requestedVisible) return;
            RefreshFill();PositionBar();
        }
    }
}
