using UnityEngine;
using UnityEngine.UI;

namespace Storybook
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TitleAmbientAnimator : MaskableGraphic
    {
        const float ArtworkWidth = 1672f;
        const float ArtworkHeight = 941f;
        [SerializeField] Image background;
        [SerializeField] Shader ambientShader;
        [SerializeField] RectTransform sparkleRoot;

        [Header("Floating book")]
        [SerializeField, Range(0, 12)] float floatAmplitude = 9f;
        [SerializeField, Min(1)] float floatPeriod = 7.6f;
        [SerializeField, Range(0, 2)] float rotationAmplitude = 1.55f;
        [SerializeField, Min(1)] float rotationPeriod = 10.2f;
        [SerializeField, Range(0, .15f)] float bookGlow = .055f;
        [SerializeField, Range(0, .2f)] float ringGlow = .085f;
        [SerializeField, Min(1)] float glowPeriod = 6.8f;

        [Header("Archive lights")]
        [SerializeField, Range(0, .1f)] float titleGlow = .033f;
        [SerializeField, Min(1)] float titlePeriod = 10f;
        [SerializeField, Range(0, .15f)] float corruptionGlow = .065f;
        [SerializeField, Min(1)] float corruptionPeriod = 8.6f;
        [SerializeField, Range(0, .1f)] float lanternGlow = .048f;
        [SerializeField, Min(.2f)] float lanternPeriod = 2.9f;

        [Header("Pixel motes")]
        [SerializeField, Range(0, 1)] float sparkleOpacity = .54f;
        [SerializeField, Min(1)] float sparklePeriod = 5.2f;
        [SerializeField, Range(0, 20)] float sparkleRise = 12f;

        // Coordinates in the source PNG, from its top-left corner.
        static readonly Rect[] Patches =
        {
            Rect.MinMaxRect(628, 402, 1169, 757),    // Book and rings
            Rect.MinMaxRect(530, 173, 1195, 355),   // Gold lettering
            Rect.MinMaxRect(1220, 0, 1672, 941),    // Red branches
            Rect.MinMaxRect(69, 151, 170, 303),     // Left hanging lantern
            Rect.MinMaxRect(258, 343, 322, 437),    // Left shelf lantern
            Rect.MinMaxRect(10, 690, 123, 860),     // Left foreground lantern
            Rect.MinMaxRect(1503, 306, 1606, 440),  // Right hanging lantern
            Rect.MinMaxRect(1328, 506, 1392, 595),  // Right shelf lantern
            Rect.MinMaxRect(472, 763, 1286, 869)    // Floor circle
        };
        static readonly Vector2[] MoteOrigins =
        {
            new Vector2(690, 589), new Vector2(784, 727), new Vector2(1063, 464),
            new Vector2(1130, 609), new Vector2(989, 731)
        };
        static readonly int Book = Shader.PropertyToID("_Book");
        static readonly int Glow = Shader.PropertyToID("_Glow");
        static readonly int Lanterns = Shader.PropertyToID("_Lanterns");

        Material ambientMaterial;
        Image[] motes;
        double startedAt;

        public override Texture mainTexture =>
            background != null && background.sprite != null ? background.sprite.texture : base.mainTexture;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
            if (!Application.isPlaying || ambientShader == null || background == null || sparkleRoot == null) return;

            ambientMaterial = new Material(ambientShader) { hideFlags = HideFlags.DontSave };
            material = ambientMaterial;
            motes = new Image[MoteOrigins.Length];
            for (int i = 0; i < motes.Length; i++)
            {
                var mote = new GameObject($"Mote {i + 1}", typeof(RectTransform), typeof(Image));
                mote.transform.SetParent(sparkleRoot, false);
                motes[i] = mote.GetComponent<Image>();
                motes[i].raycastTarget = false;
                motes[i].color = Color.clear;
            }
            startedAt = Time.unscaledTimeAsDouble;
            Update();
        }

        void Update()
        {
            if (ambientMaterial == null) return;
            float elapsed = (float)(Time.unscaledTimeAsDouble - startedAt);
            float glow = Pulse(elapsed, glowPeriod);
            float angle = rotationAmplitude * Mathf.Deg2Rad * Wave(elapsed, rotationPeriod);
            ambientMaterial.SetVector(Book, new Vector4(floatAmplitude * Wave(elapsed, floatPeriod),
                Mathf.Cos(angle), Mathf.Sin(angle), glow * bookGlow));
            ambientMaterial.SetVector(Glow, new Vector4(ringGlow * glow, titleGlow * Pulse(elapsed, titlePeriod),
                corruptionGlow * Pulse(elapsed, corruptionPeriod), 0f));
            ambientMaterial.SetVector(Lanterns, new Vector4(lanternGlow * Flicker(elapsed, 0f),
                lanternGlow * Flicker(elapsed, 1.9f), lanternGlow * Flicker(elapsed, 3.4f), 0f));
            UpdateMotes(elapsed);
        }

        void UpdateMotes(float elapsed)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                float phase = Mathf.Repeat(elapsed / sparklePeriod + i * .217f, 1f);
                float visibility = phase < .33f ? Mathf.Sin(Mathf.PI * phase / .33f) : 0f;
                motes[i].color = new Color(1f, .84f, .51f, visibility * sparkleOpacity);

                Vector2 center = MoteOrigins[i];
                center.y -= Mathf.Round(sparkleRise * phase);
                float halfSize = i % 2 == 0 ? 1.5f : 1f;
                RectTransform rect = motes[i].rectTransform;
                rect.anchorMin = new Vector2((center.x - halfSize) / ArtworkWidth,
                    1f - (center.y + halfSize) / ArtworkHeight);
                rect.anchorMax = new Vector2((center.x + halfSize) / ArtworkWidth,
                    1f - (center.y - halfSize) / ArtworkHeight);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
        }

        static float Wave(float elapsed, float period) => Mathf.Sin(2f * Mathf.PI * elapsed / period);
        static float Pulse(float elapsed, float period) => .5f + .5f * Wave(elapsed, period);

        float Flicker(float elapsed, float phase) =>
            .62f * Wave(elapsed + phase, lanternPeriod) + .38f * Wave(elapsed + phase, lanternPeriod / 1.83f);

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (ambientMaterial == null) return;
            Rect bounds = rectTransform.rect;
            for (int i = 0; i < Patches.Length; i++)
            {
                Rect patch = Patches[i];
                float left = patch.xMin / ArtworkWidth;
                float right = patch.xMax / ArtworkWidth;
                float bottom = 1f - patch.yMax / ArtworkHeight;
                float top = 1f - patch.yMin / ArtworkHeight;
                // Pack the patch index in UV0 so no additional canvas shader channels are needed.
                int first = mesh.currentVertCount;
                AddVertex(mesh, bounds, left, bottom, i);
                AddVertex(mesh, bounds, left, top, i);
                AddVertex(mesh, bounds, right, top, i);
                AddVertex(mesh, bounds, right, bottom, i);
                mesh.AddTriangle(first, first + 1, first + 2);
                mesh.AddTriangle(first + 2, first + 3, first);
            }
        }

        static void AddVertex(VertexHelper mesh, Rect bounds, float x, float y, int patch)
        {
            var uv = new Vector2(x, y);
            mesh.AddVert(Rect.NormalizedToPoint(bounds, uv), Color.white, new Vector2(x + patch * 2f, y));
        }

        protected override void OnDestroy()
        {
            if (ambientMaterial != null) Destroy(ambientMaterial);
            base.OnDestroy();
        }
    }
}
