using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [DisallowMultipleComponent]
    public sealed class DragonKingTidalWaveVisual : MonoBehaviour
    {
        [SerializeField] Material waterMaterial, sprayMaterial;

        const int Segments = 24;
        const int Rows = 5;
        readonly Vector3[] points = new Vector3[(Segments + 1) * Rows];
        Mesh mesh;
        MeshRenderer water;
        ParticleSystem foam;
        float sprayCarry;
        int sprayIndex;

        void Awake()
        {
            var sheet = new GameObject("Water crest", typeof(MeshFilter), typeof(MeshRenderer));
            sheet.transform.SetParent(transform, false);
            mesh = new Mesh { name = "Tidal wave crest" };
            mesh.MarkDynamic();
            var colors = new Color[points.Length];
            var uv = new Vector2[points.Length];
            var indices = new int[Segments * (Rows - 1) * 6];
            Color[] palette =
            {
                new Color(.024f, .28f, .31f, 0f),
                new Color(.035f, .40f, .47f, .70f),
                new Color(.13f, .71f, .74f, .82f),
                new Color(.77f, 1f, .92f, .91f),
                new Color(.26f, .73f, .77f, .10f)
            };
            int cursor = 0;
            for (int row = 0; row < Rows; row++)
            for (int x = 0; x <= Segments; x++)
            {
                int i = row * (Segments + 1) + x;
                colors[i] = palette[row];
                if (x == 0 || x == Segments) colors[i].a *= .38f;
                uv[i] = new Vector2(x / (float)Segments, row / (float)(Rows - 1));
                if (row == Rows - 1 || x == Segments) continue;
                int next = i + Segments + 1;
                indices[cursor++] = i;
                indices[cursor++] = next;
                indices[cursor++] = i + 1;
                indices[cursor++] = i + 1;
                indices[cursor++] = next;
                indices[cursor++] = next + 1;
            }
            mesh.vertices = points;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.triangles = indices;
            sheet.GetComponent<MeshFilter>().sharedMesh = mesh;
            water = sheet.GetComponent<MeshRenderer>();
            water.sharedMaterial = waterMaterial;
            water.sortingOrder = 8;
            water.shadowCastingMode = ShadowCastingMode.Off;
            water.receiveShadows = false;

            var spray = new GameObject("Tidal wave foam");
            spray.transform.SetParent(transform, false);
            foam = spray.AddComponent<ParticleSystem>();
            foam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = foam.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;
            main.gravityModifier = 1.35f;
            var emission = foam.emission;
            emission.enabled = false;
            var shape = foam.shape;
            shape.enabled = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.75f, .35f), new GradientAlphaKey(0, 1) });
            var color = foam.colorOverLifetime;
            color.enabled = true;
            color.color = gradient;
            var renderer = spray.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sprayMaterial;
            renderer.sortingOrder = 9;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Clear();
        }

        internal void Launch(DragonKingWaveLane lane, Vector3 weaponTip)
        {
            Clear();
            sprayIndex = 0;
            sprayCarry = 0f;
            foam.Play();
            Vector3 launch = lane.Origin + Vector3.up * .22f;
            // A short spray at the blade joins the physical swing to the ground crest.
            for (int i = 0; i < 14; i++)
            {
                float t = i / 13f;
                Vector3 position = Vector3.Lerp(weaponTip, launch, t);
                Emit(position, (launch - position) * 4f + lane.Forward * .8f, .27f, .15f + .16f * t);
            }
            Move(lane, 0f, 0f, 0f);
        }

        internal void Move(DragonKingWaveLane lane, float front, float distanceMoved, float elapsed)
        {
            transform.SetPositionAndRotation(lane.Origin + lane.Forward * front, lane.Rotation);
            float depth = Mathf.Min(DragonKingWaveLane.CrestDepth, front);
            for (int x = 0; x <= Segments; x++)
            {
                float across = (x / (float)Segments - .5f) * lane.Width;
                float ripple = .64f + .13f * Mathf.Sin(x * 1.7f + elapsed * 13f)
                    + .08f * Mathf.Cos(x * .8f - elapsed * 18f);
                if (x == 0 || x == Segments) ripple *= .38f;
                points[x] = new Vector3(across, .018f, -depth);
                points[Segments + 1 + x] = new Vector3(across, ripple * .37f, -depth * .72f);
                points[2 * (Segments + 1) + x] = new Vector3(across, ripple * .80f, -depth * .39f);
                points[3 * (Segments + 1) + x] = new Vector3(across, ripple, -depth * .12f);
                points[4 * (Segments + 1) + x] = new Vector3(across, .024f, 0f);
            }
            mesh.vertices = points;
            mesh.RecalculateBounds();
            water.enabled = front > .001f;
            sprayCarry += distanceMoved * 21f;
            int drops = Mathf.Min(36, (int)sprayCarry);
            sprayCarry -= drops;
            for (int i = 0; i < drops; i++)
            {
                float spread = Mathf.Repeat(sprayIndex++ * .618034f, 1f);
                Vector3 position = transform.position + lane.Right * ((spread - .5f) * lane.Width)
                    - lane.Forward * Mathf.Min(depth, distanceMoved * (i / (float)drops)) + Vector3.up * .42f;
                Emit(position, -lane.Forward * .65f + Vector3.up * (.85f + spread * 1.5f), .24f + spread * .15f, .12f + spread * .15f);
            }
        }

        void Emit(Vector3 position, Vector3 velocity, float life, float size)
        {
            foam.Emit(new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = life,
                startSize = size,
                startColor = new Color(.65f, .98f, .92f, .84f)
            }, 1);
        }

        internal void Clear()
        {
            water.enabled = false;
            foam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        void OnDestroy() => Destroy(mesh);
    }
}
