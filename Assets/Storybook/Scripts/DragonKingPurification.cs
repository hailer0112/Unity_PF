using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MonsterHealth), typeof(DragonKingPhase), typeof(DragonKingTridentSlam))]
    public sealed class DragonKingPurification : MonoBehaviour
    {
        [SerializeField] PlayerHealth player;
        [SerializeField] ChapterClearScreen clearScreenPrefab;
        [SerializeField, Min(.1f)] float duration = 3.2f;

        MonsterHealth health;
        DragonKingPhase look;
        DragonKingTridentSlam combat;
        Animator animator;
        HongGildong controls;
        ChapterClearScreen clearScreen;
        float elapsed;
        bool started, finished, ownsPlayerLock, previousProtection, controlsWereEnabled;

        void Awake()
        {
            health = GetComponent<MonsterHealth>();
            look = GetComponent<DragonKingPhase>();
            combat = GetComponent<DragonKingTridentSlam>();
            animator = GetComponent<Animator>();
            controls = player.GetComponent<HongGildong>();
        }

        void OnEnable() => health.Died += Begin;

        void Begin()
        {
            if (started) return;
            started = true;
            previousProtection = player.ChapterClearProtected;
            controlsWereEnabled = controls.enabled;
            ownsPlayerLock = true;
            player.ChapterClearProtected = true;
            controls.enabled = false;
            combat.BeginDefeat();
            animator.enabled = false;
            combat.PoseDefeat(0f);
            look.BeginPurification();
            look.RenderPurification(0f);
        }

        void LateUpdate()
        {
            if (!started || finished) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            combat.PoseDefeat(Mathf.InverseLerp(0f, .47f, progress));
            look.RenderPurification(progress);
            if (progress < 1f) return;

            finished = true;
            clearScreen = Instantiate(clearScreenPrefab);
            clearScreen.Show(ReleasePlayer);
        }

        void ReleasePlayer()
        {
            if (!ownsPlayerLock) return;
            ownsPlayerLock = false;
            if (player == null) return;
            player.ChapterClearProtected = previousProtection;
            if (!player.IsDead) controls.enabled = controlsWereEnabled;
        }

        void OnDisable()
        {
            if (health != null) health.Died -= Begin;
            if (clearScreen != null) Destroy(clearScreen.gameObject);
            ReleasePlayer();
        }
    }
}
