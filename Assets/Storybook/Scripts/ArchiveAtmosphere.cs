using System;
using UnityEngine;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class ArchiveAtmosphere : MonoBehaviour
    {
        [Serializable]
        public struct Drift
        {
            public Transform target;
            public float amplitude, speed, phase, roll;
        }

        [SerializeField] Drift[] floating;
        [SerializeField] Transform portalGlyphs;
        [SerializeField] SpriteRenderer rift, turningBook;
        [SerializeField] Sprite pageRest, pageRaised;

        Vector3[] centers;
        Quaternion[] rotations;
        Quaternion glyphRest;
        Color riftColor;

        void Awake()
        {
            centers = new Vector3[floating.Length];
            rotations = new Quaternion[floating.Length];
            for (int i = 0; i < floating.Length; i++)
            {
                centers[i] = floating[i].target.localPosition;
                rotations[i] = floating[i].target.localRotation;
            }
            glyphRest = portalGlyphs.localRotation;
            riftColor = rift.color;
        }

        void Update()
        {
            float time = Time.time;
            for (int i = 0; i < floating.Length; i++)
            {
                var drift = floating[i];
                float wave = Mathf.Sin(time * drift.speed + drift.phase);
                drift.target.localPosition = centers[i] + Vector3.up * (drift.amplitude * wave);
                drift.target.localRotation = rotations[i] * Quaternion.AngleAxis(drift.roll * wave, Vector3.forward);
            }
            portalGlyphs.localRotation = glyphRest * Quaternion.AngleAxis(time * 12f % 360f, Vector3.forward);
            Color color = riftColor;
            color.a *= .92f + .08f * Mathf.Sin(time * 1.4f);
            rift.color = color;

            // The brief lifted sheet returns to the same resting spread.
            float pageTime = Mathf.Repeat(time, 5.2f);
            turningBook.sprite = pageTime >= 4.55f && pageTime < 4.87f ? pageRaised : pageRest;
        }
    }
}
