using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    // Owns the circular telegraph, burst or poison area for its complete lifetime.
    public sealed class PufferfishHazard : MonoBehaviour
    {
        const int Segments = 64;
        const float BurstSeconds = .22f;
        const float AuthoredPoisonRadius = 2.25f;
        const float AuthoredPoisonDuration = 3f;
        static readonly int Tint = Shader.PropertyToID("_Color");

        Mesh mesh;
        MeshRenderer surface;
        MaterialPropertyBlock color;
        PlayerHealth target;
        float radius, damage, tickInterval;
        double startedAt, expiresAt, nextTick;
        bool burst;

        internal static PufferfishHazard Create(string objectName, Transform parent, Vector3 center, float radius, Material material)
        {
            var area = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer), typeof(PufferfishHazard));
            area.transform.SetParent(parent);
            area.transform.position = center + Vector3.up * .025f;
            var effect = area.GetComponent<PufferfishHazard>();
            effect.radius = radius;
            effect.enabled = false;
            effect.BuildSurface(material);
            return effect;
        }

        internal void SetCharge(float progress) =>
            SetColors(new Color(.78f, .024f, .05f, Mathf.Lerp(.21f, .64f, progress)),
                      new Color(1f, .15f, .19f, .94f));

        internal void ShowBurst()
        {
            burst = true;
            surface.sortingOrder = 3;
            startedAt = Time.timeAsDouble;
            expiresAt = startedAt + BurstSeconds;
            RefreshBurst(0f);
            enabled = true;
        }

        internal static void CreatePoison(Transform parent, Vector3 center, float radius, PlayerHealth player,
            float duration, float interval, float tickDamage, ParticleSystem smokePrefab)
        {
            var area = new GameObject("Pufferfish poison cloud", typeof(PufferfishHazard));
            area.transform.SetParent(parent);
            area.transform.position = center + Vector3.up * .025f;
            var hazard = area.GetComponent<PufferfishHazard>();
            hazard.radius = radius;
            hazard.target = player;
            hazard.damage = tickDamage;
            hazard.tickInterval = interval;
            hazard.startedAt = Time.timeAsDouble;
            hazard.expiresAt = hazard.startedAt + duration;
            hazard.nextTick = hazard.startedAt + interval;

            if (smokePrefab == null) return;
            var smoke = Instantiate(smokePrefab, area.transform, false);
            smoke.transform.localScale *= radius / AuthoredPoisonRadius;
            foreach (var particles in smoke.GetComponentsInChildren<ParticleSystem>())
            {
                var main = particles.main;
                main.simulationSpeed *= AuthoredPoisonDuration / Mathf.Max(.01f, duration);
            }
            smoke.Play(true);
        }

        void Update()
        {
            double now = Time.timeAsDouble;
            float elapsed = (float)(now - startedAt);
            if (burst) RefreshBurst(elapsed / BurstSeconds);
            else
            {
                if (now >= nextTick && nextTick <= expiresAt)
                {
                    // A rendered frame samples the player once, including after a hitch.
                    // Advance to the next scheduled tick even if the dash rejected damage.
                    double nextScheduled = startedAt + (Math.Floor((now - startedAt) / tickInterval) + 1) * tickInterval;
                    nextTick = Math.Max(nextTick + tickInterval, nextScheduled);
                    DamageInside(target, transform.position, radius, damage);
                }
            }
            if (now < expiresAt) return;
            enabled = false;
            if (surface != null) surface.enabled = false;
            Destroy(gameObject);
        }

        internal static void DamageInside(PlayerHealth player, Vector3 center, float radius, float damage)
        {
            if (player == null) return;
            Vector3 offset = player.transform.position - center;
            if (offset.x * offset.x + offset.z * offset.z <= radius * radius)
                player.TryTakeDamage(damage);
        }

        void RefreshBurst(float progress)
        {
            float alpha = 1f - Mathf.Clamp01(progress);
            SetColors(new Color(1f, .38f, .13f, .68f * alpha), new Color(1f, .75f, .48f, alpha));
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (surface != null || radius <= 0f || UnityEditor.SceneView.currentDrawingSceneView == null) return;
            UnityEditor.Handles.color = new Color(.54f, .75f, .39f);
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.up, radius);
        }
#endif

        void SetColors(Color fill, Color edge)
        {
            color.SetColor(Tint, fill);
            surface.SetPropertyBlock(color, 0);
            color.SetColor(Tint, edge);
            surface.SetPropertyBlock(color, 1);
        }

        void BuildSurface(Material material)
        {
            surface = GetComponent<MeshRenderer>();
            surface.sharedMaterials = new[] { material, material };
            surface.shadowCastingMode = ShadowCastingMode.Off;
            surface.receiveShadows = false;
            surface.sortingOrder = 2;
            color = new MaterialPropertyBlock();

            var vertices = new Vector3[1 + Segments * 2];
            var interior = new int[Segments * 3];
            var border = new int[Segments * 6];
            float inner = Mathf.Max(radius - .06f, radius * .5f);
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                int a = 1 + i * 2;
                int b = 1 + ((i + 1) % Segments) * 2;
                vertices[a] = direction * inner;
                vertices[a + 1] = direction * radius;

                int t = i * 3;
                interior[t + 1] = b;
                interior[t + 2] = a;

                int r = i * 6;
                border[r] = a;
                border[r + 1] = b;
                border[r + 2] = a + 1;
                border[r + 3] = a + 1;
                border[r + 4] = b;
                border[r + 5] = b + 1;
            }

            mesh = new Mesh { name = "Pufferfish ground area", vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(interior, 0);
            mesh.SetTriangles(border, 1);
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
