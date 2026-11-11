using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class DragonKingEruptionVisual : MonoBehaviour
    {
        [SerializeField] Material surfaceMaterial, dropletMaterial;
        [SerializeField, Min(.01f)] float duration = .5f;
        [SerializeField, Min(.1f)] float columnHeight = 3.2f;
        [SerializeField] Color deepWater = new Color(.17f, .018f, .071f, .86f);
        [SerializeField] Color crimson = new Color(.66f, .056f, .17f, .78f);

        const int RingSegments = 48;
        readonly Vector3[] ring = new Vector3[RingSegments];
        ParticleSystem charge, droplets;
        SpriteRenderer palmGlow;
        Sprite glowSprite;
        MeshRenderer column;
        Mesh columnMesh;
        LineRenderer splash;
        MaterialPropertyBlock tint;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        double impactTime;
        float impactRadius;
        bool impacting;

        public bool IsActive => impacting || charge.isPlaying;

        void Awake()
        {
            charge = CreateParticles("Palm energy", true);
            var emission = charge.emission;
            emission.rateOverTime = 24f;
            var shape = charge.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .17f;

            var glow = new GameObject("Palm glow");
            glow.transform.SetParent(charge.transform, false);
            palmGlow = glow.AddComponent<SpriteRenderer>();
            var glowTexture = (Texture2D)dropletMaterial.mainTexture;
            glowSprite = Sprite.Create(glowTexture, new Rect(0f, 0f, glowTexture.width, glowTexture.height),
                new Vector2(.5f, .5f), glowTexture.width, 0, SpriteMeshType.FullRect);
            palmGlow.sprite = glowSprite;
            palmGlow.sharedMaterial = surfaceMaterial;
            palmGlow.sortingOrder = 20;

            droplets = CreateParticles("Water spray", false);
            var stream = new GameObject("Water column");
            stream.transform.SetParent(transform, false);
            columnMesh = BuildColumn();
            stream.AddComponent<MeshFilter>().sharedMesh = columnMesh;
            column = stream.AddComponent<MeshRenderer>();
            column.sharedMaterial = surfaceMaterial;
            column.sortingOrder = 8;
            column.shadowCastingMode = ShadowCastingMode.Off;
            column.receiveShadows = false;
            tint = new MaterialPropertyBlock();

            var wave = new GameObject("Ground splash");
            wave.transform.SetParent(transform, false);
            wave.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            splash = wave.AddComponent<LineRenderer>();
            splash.sharedMaterial = surfaceMaterial;
            splash.loop = true;
            splash.useWorldSpace = false;
            splash.alignment = LineAlignment.TransformZ;
            splash.positionCount = RingSegments;
            splash.sortingOrder = 8;
            splash.shadowCastingMode = ShadowCastingMode.Off;
            splash.receiveShadows = false;
            Clear();
        }

        public void BeginCharge(Vector3 palm)
        {
            UpdateCharge(palm, 0f);
            palmGlow.enabled = true;
            charge.Play();
        }

        public void UpdateCharge(Vector3 palm, float progress)
        {
            charge.transform.position = palm;
            var main = charge.main;
            main.startSize = Mathf.Lerp(.075f, .23f, progress);
            float gather = Mathf.SmoothStep(0f, 1f, progress);
            palmGlow.transform.localScale = Vector3.one * Mathf.Lerp(.34f, 1.15f, gather);
            palmGlow.color = Color.Lerp(new Color(.36f, .015f, .09f, 0f), new Color(.94f, .13f, .24f, .94f), gather);
        }

        public void PlayImpact(Vector3 ground, float radius)
        {
            Clear();
            transform.position = ground + Vector3.up * .046f;
            impactTime = Time.timeAsDouble;
            impactRadius = radius;
            impacting = true;

            droplets.Play();
            for (int i = 0; i < 48; i++)
            {
                float angle = i * 2.3999632f;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float spread = (i % 7) / 6f;
                bool groundSpray = i >= 28;
                var drop = new ParticleSystem.EmitParams
                {
                    position = outward * radius * (.06f + spread * .13f),
                    velocity = outward * radius * (groundSpray ? 2.3f : .65f) +
                               Vector3.up * (groundSpray ? 1.2f + spread * 1.6f : 5f + spread * 4f),
                    startSize = Mathf.Lerp(.12f, .28f, spread),
                    startLifetime = duration * Mathf.Lerp(.55f, 1f, spread),
                    startColor = Color.Lerp(crimson, new Color(.95f, .27f, .37f, .92f), spread)
                };
                droplets.Emit(drop, 1);
            }

            column.enabled = splash.enabled = true;
            UpdateImpact(0f);
        }

        void Update()
        {
            if (!impacting) return;
            float progress = (float)(Time.timeAsDouble - impactTime) / Mathf.Max(.01f, duration);
            if (progress >= 1f) StopImpact();
            else UpdateImpact(progress);
        }

        void UpdateImpact(float progress)
        {
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .26f, progress));
            float collapse = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, 1f, progress));
            float width = impactRadius * Mathf.Lerp(.45f, .23f, collapse);
            column.transform.localScale = new Vector3(width, columnHeight * Mathf.Max(.04f, rise - .75f * collapse), width);
            tint.SetColor(ColorId, new Color(1f, 1f, 1f, 1f - collapse));
            column.SetPropertyBlock(tint);

            float radius = impactRadius * Mathf.Lerp(.16f, .96f, Mathf.Sqrt(progress));
            for (int i = 0; i < ring.Length; i++)
            {
                float angle = i * 2f * Mathf.PI / ring.Length;
                float edge = radius * (1f + .035f * Mathf.Sin(9f * angle + 4f * progress));
                ring[i] = new Vector3(edge * Mathf.Cos(angle), edge * Mathf.Sin(angle), 0f);
            }

            var splashColor = crimson;
            splashColor.a *= (1f - progress) * .75f;
            splash.startColor = splash.endColor = splashColor;
            splash.startWidth = splash.endWidth = Mathf.Lerp(.14f, .045f, progress);
            splash.SetPositions(ring);
        }

        ParticleSystem CreateParticles(string objectName, bool hand)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = hand;
            main.startLifetime = hand ? .24f : duration;
            main.startSpeed = hand ? .34f : 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(deepWater, crimson);
            main.gravityModifier = hand ? 0f : .88f;
            main.maxParticles = hand ? 24 : 48;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particles.emission;
            emission.enabled = hand;
            var shape = particles.shape;
            shape.enabled = false;

            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0f, 1f) });
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            colors.color = fade;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dropletMaterial;
            renderer.sortingOrder = hand ? 19 : 9;
            renderer.renderMode = hand ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = hand ? 0f : .16f;
            renderer.lengthScale = 1.5f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return particles;
        }

        Mesh BuildColumn()
        {
            // Intersecting, tapered sheets keep the water silhouette visible from either side.
            const int sheets = 3, levels = 8, columns = 7;
            var vertices = new Vector3[sheets * levels * columns];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[sheets * (levels - 1) * (columns - 1) * 6];
            float[] widths = { .82f, .96f, .72f, .67f, .57f, .48f, .36f, .09f };
            float[] crossSection = { -1f, -.56f, -.43f, -.31f, .18f, .36f, 1f };
            Color streak = Color.Lerp(crimson, new Color(1f, .42f, .52f, .92f), .55f);
            int index = 0;
            for (int sheet = 0; sheet < sheets; sheet++)
            {
                float angle = sheet * Mathf.PI / sheets;
                var across = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (int row = 0; row < levels; row++)
                for (int edge = 0; edge < columns; edge++)
                {
                    int vertex = (sheet * levels + row) * columns + edge;
                    float y = row / (float)(levels - 1);
                    float flow = .21f * Mathf.Sin(1.15f * row + sheet);
                    vertices[vertex] = Vector3.up * y + across * (flow + crossSection[edge] * widths[row]);
                    bool rim = edge == 0 || edge == columns - 1;
                    Color water = edge == 2 ? streak : edge == 5 ? crimson : deepWater;
                    water.a *= (rim ? .23f : 1f) * (row == levels - 1 ? .16f : 1f - .38f * y);
                    colors[vertex] = water;
                    uv[vertex] = new Vector2((crossSection[edge] + 1f) * .5f, y);
                    if (row == levels - 1 || edge == columns - 1) continue;
                    triangles[index++] = vertex;
                    triangles[index++] = vertex + columns;
                    triangles[index++] = vertex + 1;
                    triangles[index++] = vertex + 1;
                    triangles[index++] = vertex + columns;
                    triangles[index++] = vertex + columns + 1;
                }
            }

            var mesh = new Mesh { name = "Corrupted water sheets", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            return mesh;
        }

        void StopImpact()
        {
            impacting = false;
            column.enabled = splash.enabled = false;
            droplets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void Clear()
        {
            palmGlow.enabled = false;
            charge.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            StopImpact();
        }

        void OnDestroy()
        {
            Destroy(glowSprite);
            Destroy(columnMesh);
        }
    }
}
