using System;
using UnityEngine;

namespace Storybook
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    internal sealed class EnemyTimeStopController : MonoBehaviour
    {
        const float IconHeight = .28f;
        const float BarSpacing = .08f;
        double startedAt, endsAt, immuneUntil, pausedTime;
        double unscaledStart, pausedUnscaledTime;
        float immunityDuration;
        Animator[] animators;
        float[] animatorSpeeds;
        SpriteRenderer icon;
        EnemyHealthBar healthBar;
        DragonKingTridentSlam boss;
        Camera view;

        internal bool IsStopped { get; private set; }
        internal bool CanStart => !IsStopped && Time.timeAsDouble >= immuneUntil;
        internal double ActionTime => Time.timeAsDouble - pausedTime -
            (IsStopped ? Math.Min(Time.timeAsDouble, endsAt) - startedAt : 0d);
        internal double UnscaledActionTime => (IsStopped ? unscaledStart : Time.unscaledTimeAsDouble) - pausedUnscaledTime;

        void Awake()
        {
            healthBar = GetComponentInChildren<EnemyHealthBar>(true);
            boss = GetComponent<DragonKingTridentSlam>();
            var follow = FindAnyObjectByType<ClearingCamera>();
            view = follow != null ? follow.GetComponent<Camera>() : Camera.main;
        }

        internal void Begin(float duration, float immunity, Sprite clockSprite)
        {
            startedAt = Time.timeAsDouble;
            unscaledStart = Time.unscaledTimeAsDouble;
            endsAt = startedAt + duration;
            immunityDuration = immunity;
            IsStopped = true;
            animators = GetComponentsInChildren<Animator>(true);
            animatorSpeeds = new float[animators.Length];
            for (int i = 0; i < animators.Length; i++)
            {
                animatorSpeeds[i] = animators[i].speed;
                animators[i].speed = 0f;
            }

            if (clockSprite != null)
            {
                icon = new GameObject("Time Stop Clock").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(transform, false);
                icon.sprite = clockSprite;
                icon.sortingOrder = 30;
                PositionIcon();
            }
        }

        void Update()
        {
            if (!IsStopped || Time.timeAsDouble < endsAt) return;
            immuneUntil = endsAt + immunityDuration;
            Cancel();
        }

        void LateUpdate()
        {
            if (icon != null) PositionIcon();
        }

        void PositionIcon()
        {
            Vector3 up = view != null ? view.transform.up : Vector3.up;
            Quaternion rotation = view != null ? view.transform.rotation : Quaternion.identity;
            Vector3 anchor = boss != null ? boss.TimeStopAnchor(up) :
                healthBar != null ? healthBar.PositionAboveHead(up) : transform.position + up * 1.05f;
            Vector3 position = anchor + up * (BarSpacing + IconHeight * .5f);
            if (boss != null && view != null)
            {
                // The player camera can crop out the giant boss's head. Reserve the top strip for the boss HUD.
                Vector3 viewport = view.WorldToViewportPoint(position);
                if (viewport.z > view.nearClipPlane)
                {
                    viewport.x = Mathf.Clamp(viewport.x, .08f, .92f);
                    viewport.y = Mathf.Clamp(viewport.y, .10f, .78f);
                    // Keep the status marker in front of the arena geometry at the same screen position.
                    viewport.z = view.nearClipPlane + .5f;
                    position = view.ViewportToWorldPoint(viewport);
                }
            }
            icon.transform.SetPositionAndRotation(position, rotation);
            float scale = IconHeight / icon.sprite.bounds.size.y;
            var parentScale = transform.lossyScale;
            icon.transform.localScale = new Vector3(scale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)),
                scale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), scale);
        }

        internal void Cancel()
        {
            if (!IsStopped) return;
            pausedTime += Math.Min(Time.timeAsDouble, endsAt) - startedAt;
            pausedUnscaledTime += Time.unscaledTimeAsDouble - unscaledStart;
            IsStopped = false;
            for (int i = 0; i < animators.Length; i++)
                if (animators[i] != null) animators[i].speed = animatorSpeeds[i];
            animators = null;
            animatorSpeeds = null;
            if (icon == null) return;
            icon.gameObject.SetActive(false);
            Destroy(icon.gameObject);
            icon = null;
        }

        void OnDisable() => Cancel();
    }
}
