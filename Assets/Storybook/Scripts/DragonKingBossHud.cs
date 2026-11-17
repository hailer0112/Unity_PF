using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class DragonKingBossHud : MonoBehaviour
    {
        [SerializeField] MonsterHealth boss;
        [SerializeField] Transform player;
        [SerializeField] BoxCollider arenaFloor;
        [SerializeField] RectTransform panel;
        [SerializeField] Image fill;
        [SerializeField] Text bossNameLabel, healthLabel;
        [SerializeField] Image phaseMarker;
        [SerializeField] GameObject phaseBadge;
        [SerializeField] Image[] phaseOrnaments;
        [SerializeField] Color phaseAccent = new Color(.93f, .42f, .38f);
        [SerializeField, Min(0f)] float fillDuration = .2f;
        [SerializeField, Min(0f)] float deathHold = .8f;

        static readonly Color MarkerGold = new Color(1f, .84f, .56f);
        static readonly Color MarkerRed = new Color(1f, .64f, .54f);

        Font nameFont;
        float fromFill, targetFill, hideAt;
        float fillStartedAt;
        bool initialized;
        bool phaseReached;

        void OnEnable()
        {
            if (boss == null) return;
            boss.Damaged += SyncHealth;
            boss.Died += OnBossDied;
            if (initialized) InitializeDisplay();
        }

        void Start()
        {
            if (boss == null || player == null || arenaFloor == null)
            {
                Debug.LogError("Dragon King HUD needs a boss, player and arena floor.", this);
                enabled = false;
                return;
            }

            nameFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, bossNameLabel.fontSize);
            if (nameFont != null) bossNameLabel.font = nameFont;
            initialized = true;
            InitializeDisplay();
        }

        void InitializeDisplay()
        {
            hideAt = float.NegativeInfinity;
            SetPhaseAccent(phaseReached || DragonKingPhase.ReachedThreshold(boss));
            SyncHealth();
            fill.fillAmount = fromFill = targetFill;
            RefreshVisibility();
        }

        void Update()
        {
            if (!initialized) return;
            RefreshVisibility();
            if (!panel.gameObject.activeSelf) return;
            float elapsed = Time.unscaledTime - fillStartedAt;
            float progress = fillDuration > 0f ? Mathf.Clamp01(elapsed / fillDuration) : 1f;
            fill.fillAmount = Mathf.SmoothStep(fromFill, targetFill, progress);
        }

        void RefreshVisibility()
        {
            var bounds = arenaFloor.bounds;
            Vector3 position = player.position;
            bool inside = position.x >= bounds.min.x && position.x <= bounds.max.x &&
                          position.z >= bounds.min.z && position.z <= bounds.max.z;
            bool visible = inside && (!boss.IsDead || Time.unscaledTime < hideAt);
            if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
            if (!visible) return;

            // Reserve the top-left player's HUD footprint on narrow viewports.
            float leftMargin = ((RectTransform)transform).rect.width * panel.anchorMin.x;
            Vector2 offset = new Vector2(0f, leftMargin >= 260f ? -14f : -56f);
            if (panel.anchoredPosition != offset) panel.anchoredPosition = offset;
        }

        void SyncHealth()
        {
            float max = Mathf.Max(1f, boss.MaxHP);
            float hp = Mathf.Clamp(boss.CurrentHP, 0f, max);
            healthLabel.text = Mathf.CeilToInt(hp).ToString("N0", CultureInfo.InvariantCulture) + " / " +
                               Mathf.CeilToInt(max).ToString("N0", CultureInfo.InvariantCulture);
            fromFill = fill.fillAmount;
            targetFill = hp / max;
            if (!phaseReached && DragonKingPhase.ReachedThreshold(boss)) SetPhaseAccent(true);
            fillStartedAt = Time.unscaledTime;
        }

        void OnBossDied()
        {
            SyncHealth();
            hideAt = Time.unscaledTime + Mathf.Max(deathHold, fillDuration);
        }

        void SetPhaseAccent(bool active)
        {
            phaseReached = active;
            phaseMarker.color = active ? MarkerRed : MarkerGold;
            phaseBadge.SetActive(active);
            foreach (Image ornament in phaseOrnaments)
                ornament.color = active ? phaseAccent : Color.white;
        }

        void OnDisable()
        {
            if (boss != null)
            {
                boss.Damaged -= SyncHealth;
                boss.Died -= OnBossDied;
            }
            if (panel != null) panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (nameFont != null) Destroy(nameFont);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void BindTestArena(DragonKingTridentSlam encounter)
        {
            gameObject.SetActive(false);
            boss = encounter.GetComponent<MonsterHealth>();
            arenaFloor = encounter.ArenaFloorForTesting;
            phaseReached = false;
        }
#endif
    }
}
