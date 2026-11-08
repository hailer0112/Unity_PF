using UnityEngine;
using UnityEngine.Rendering;

namespace Storybook
{
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(60)]
    public sealed class HongGildongBladeVisual : MonoBehaviour
    {
        // The previous blade was 1.30 times the source length. Extend that result by 30%.
        const float Extension = .69f;
        static readonly int MainTex = Shader.PropertyToID("_MainTex");
        static readonly int Tint = Shader.PropertyToID("_Color");
        static readonly int[] Triangles = { 0, 1, 2, 2, 1, 3 };

        // Pixel landmarks measured from the top left of each original frame: blade base and tip.
        static readonly Vector4[] WalkFront =
        {
            new Vector4(68, 67, 90, 80), new Vector4(66, 66, 89, 80),
            new Vector4(70, 68, 85, 77), new Vector4(62, 65, 84, 79)
        };
        static readonly Vector4[] WalkBack =
        {
            new Vector4(68, 66, 91, 80), new Vector4(66, 64, 88, 79),
            new Vector4(67, 64, 90, 76), new Vector4(67, 65, 89, 78)
        };
        static readonly Vector4[] RunFront =
        {
            new Vector4(66, 61, 90, 76), new Vector4(57, 62, 81, 76),
            default, new Vector4(64, 61, 87, 74)
        };
        static readonly Vector4[] RunBack =
        {
            new Vector4(71, 60, 94, 74), new Vector4(60, 60, 84, 74),
            default, new Vector4(65, 59, 89, 73)
        };
        static readonly Vector4[] SwingFront =
        {
            new Vector4(46, 48, 25, 41), new Vector4(72, 16, 98, 6),
            new Vector4(106, 48, 135, 38), new Vector4(90, 75, 116, 86)
        };
        static readonly Vector4[] SwingBack =
        {
            new Vector4(45, 47, 25, 38), new Vector4(75, 16, 104, 6),
            new Vector4(108, 45, 136, 38), new Vector4(98, 75, 123, 86)
        };

        SpriteRenderer body;
        Sprite frontRunSource, backRunSource, previous;
        MeshRenderer blade;
        Mesh mesh;
        MaterialPropertyBlock properties;
        readonly Vector3[] vertices = new Vector3[4];
        readonly Vector2[] uvs = new Vector2[4];
        bool previousFlip;

        public void Bind(Sprite runFront, Sprite runBack)
        {
            frontRunSource = runFront;
            backRunSource = runBack;
        }

        void Awake()
        {
            body = GetComponent<SpriteRenderer>();
            var go = new GameObject("Longer Blade", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.back * .006f;
            blade = go.GetComponent<MeshRenderer>();
            blade.sharedMaterial = body.sharedMaterial;
            blade.shadowCastingMode = ShadowCastingMode.Off;
            blade.receiveShadows = false;
            blade.enabled = false;
            mesh = new Mesh { name = "Hong Gildong Blade Extension" };
            mesh.MarkDynamic();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            properties = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            Sprite frame = body.sprite;
            if (frame != previous || body.flipX != previousFlip)
            {
                previous = frame;
                previousFlip = body.flipX;
                blade.enabled = Build(frame);
            }
            if (!blade.enabled) return;

            blade.sortingLayerID = body.sortingLayerID;
            blade.sortingOrder = body.sortingOrder + 1;
            properties.SetColor(Tint, body.color);
            blade.SetPropertyBlock(properties);
        }

        bool Build(Sprite frame)
        {
            if (frame == null || !TryBlade(frame.name, out Vector4 guide)) return false;

            Sprite source = frame;
            Vector2 restoredGrip = default;
            if (guide == default)
            {
                bool front = frame.name == "runFront2";
                source = front ? frontRunSource : backRunSource;
                if (source == null) return false;
                guide = front ? RunFront[1] : RunBack[1];
                restoredGrip = front ? new Vector2(76, 47) : new Vector2(76, 42);
            }

            Vector2 root = new Vector2(guide.x, guide.y);
            Vector2 tip = new Vector2(guide.z, guide.w);
            Vector2 direction = (tip - root).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * 5f;
            bool restore = source != frame;
            Vector2 start = Vector2.Lerp(root, tip, restore ? .07f : .52f);
            Vector2 end = tip + direction * 1.2f;
            Vector2 offset = restore ? restoredGrip - start : Vector2.zero;
            Vector2 extended = end + (tip - root) * Extension;

            Corner(0, frame, source, start - side, start + offset - side);
            Corner(1, frame, source, start + side, start + offset + side);
            Corner(2, frame, source, end - side, extended + offset - side);
            Corner(3, frame, source, end + side, extended + offset + side);
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = Triangles;
            mesh.RecalculateBounds();
            properties.SetTexture(MainTex, source.texture);
            return true;
        }

        void Corner(int index, Sprite frame, Sprite source, Vector2 sample, Vector2 draw)
        {
            Rect rect = source.rect;
            uvs[index] = new Vector2((rect.x + sample.x) / source.texture.width,
                (rect.yMax - sample.y) / source.texture.height);
            float x = (draw.x - frame.pivot.x) / frame.pixelsPerUnit;
            vertices[index] = new Vector3(body.flipX ? -x : x,
                (frame.rect.height - draw.y - frame.pivot.y) / frame.pixelsPerUnit, 0f);
        }

        static bool TryBlade(string name, out Vector4 guide)
        {
            switch (name)
            {
                case "IdleFront": case "IdleFront_0": guide = new Vector4(53, 65, 76, 78); return true;
                case "IdleBack": case "IdleBack_0": guide = new Vector4(52, 65, 74, 78); return true;
                case "StepFront": case "StepFront_0": guide = new Vector4(26, 45, 3, 63); return true;
                case "StepBack": case "StepBack_0": guide = new Vector4(24, 48, 3, 65); return true;
                case "SlashFront": case "SlashFront_0": guide = new Vector4(89, 44, 125, 39); return true;
                case "SlashBack": case "SlashBack_0": guide = new Vector4(91, 44, 124, 39); return true;
            }

            Vector4[] frames = name.StartsWith("walkFront", System.StringComparison.Ordinal) ? WalkFront :
                name.StartsWith("walkBack", System.StringComparison.Ordinal) ? WalkBack :
                name.StartsWith("runFront", System.StringComparison.Ordinal) ? RunFront :
                name.StartsWith("runBack", System.StringComparison.Ordinal) ? RunBack :
                name.StartsWith("swingFront", System.StringComparison.Ordinal) ? SwingFront :
                name.StartsWith("swingBack", System.StringComparison.Ordinal) ? SwingBack : null;
            int index = name.Length > 0 ? name[name.Length - 1] - '0' : -1;
            if (frames != null && index >= 0 && index < frames.Length)
            {
                guide = frames[index];
                return true;
            }
            guide = default;
            return false;
        }

        void OnDestroy()
        {
            if (blade != null) Destroy(blade.gameObject);
            if (mesh != null) Destroy(mesh);
        }
    }
}
