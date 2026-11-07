using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    public sealed class AzureSlashProjectile : MonoBehaviour
    {
        readonly HashSet<AttackTarget> hitTargets = new HashSet<AttackTarget>();
        Transform owner;
        float remainingDistance;
        float speed;
        float damage;
        LayerMask targetMask;
        Vector3 halfExtents;
        Mesh crescent;
        Material crescentMaterial;

        public static void Launch(Transform owner, Vector3 direction, float range, float speed,
            float width, float damage, LayerMask targetMask)
        {
            var root = new GameObject("Azure Slash");
            root.transform.SetPositionAndRotation(owner.position + Vector3.up * .42f,
                Quaternion.LookRotation(direction, Vector3.up));

            var projectile = root.AddComponent<AzureSlashProjectile>();
            projectile.owner = owner;
            projectile.remainingDistance = range;
            projectile.speed = speed;
            projectile.damage = damage;
            projectile.targetMask = targetMask;
            projectile.halfExtents = new Vector3(width * .5f, .25f, .04f);
            projectile.CreateVisual(width);
        }

        void Update() => Advance(Time.deltaTime);

        void Advance(float deltaTime)
        {
            float step = Mathf.Min(speed * deltaTime, remainingDistance);
            if (step <= 0f) return;

            Vector3 direction = transform.forward;
            var hits = Physics.BoxCastAll(transform.position, halfExtents, direction,
                transform.rotation, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            // Resolve the nearest wall before applying hits, since cast results have no ordering.
            float wallDistance = float.PositiveInfinity;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(owner)) continue;
                if (hit.collider.GetComponentInParent<AttackTarget>() == null)
                    wallDistance = Mathf.Min(wallDistance, hit.distance);
            }

            foreach (RaycastHit hit in hits)
            {
                if (hit.distance >= wallDistance || hit.collider.transform.IsChildOf(owner) ||
                    (targetMask.value & (1 << hit.collider.gameObject.layer)) == 0)
                    continue;

                var target = hit.collider.GetComponentInParent<AttackTarget>();
                if (target != null && target.CanTakeDamage && hitTargets.Add(target))
                    target.ReceiveDamage(damage);
            }

            float travelled = Mathf.Min(step, wallDistance);
            transform.position += direction * travelled;
            remainingDistance -= travelled;
            if (wallDistance <= step || remainingDistance <= .0001f)
                Destroy(gameObject);
        }

        void CreateVisual(float width)
        {
            // Three strips form a pale leading edge and a tapered blue crescent.
            const int segments = 24;
            var vertices = new Vector3[(segments + 1) * 3];
            var colors = new Color[vertices.Length];
            var indices = new int[segments * 12];

            for (int i = 0; i <= segments; i++)
            {
                float u = 2f * i / segments - 1f;
                float curve = 1f - u * u;
                float x = width * .5f * u;
                float edge = width * .19f * curve;
                int row = i * 3;
                vertices[row] = new Vector3(x, 0f, edge - width * .18f * curve);
                vertices[row + 1] = new Vector3(x, 0f, edge - width * .035f * curve);
                vertices[row + 2] = new Vector3(x, 0f, edge);
                colors[row] = new Color(.05f, .3f, 1f, 0f);
                colors[row + 1] = new Color(.2f, .8f, 1f, .95f);
                colors[row + 2] = new Color(.8f, .96f, 1f, .9f);

                if (i == segments) continue;
                for (int strip = 0; strip < 2; strip++)
                {
                    int a = row + strip;
                    int index = i * 12 + strip * 6;
                    indices[index] = a;
                    indices[index + 1] = a + 1;
                    indices[index + 2] = a + 3;
                    indices[index + 3] = a + 1;
                    indices[index + 4] = a + 4;
                    indices[index + 5] = a + 3;
                }
            }

            crescent = new Mesh { name = "Azure Slash Crescent" };
            crescent.vertices = vertices;
            crescent.colors = colors;
            crescent.uv = new Vector2[vertices.Length];
            crescent.triangles = indices;
            crescent.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = crescent;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            crescentMaterial = new Material(Shader.Find("Sprites/Default"));
            renderer.sharedMaterial = crescentMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 4;
        }

        void OnDestroy()
        {
            if (crescent != null) Destroy(crescent);
            if (crescentMaterial != null) Destroy(crescentMaterial);
        }
    }
}
