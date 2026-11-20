using UnityEngine;
using UnityEngine.SceneManagement;

namespace Storybook
{
    internal enum DefeatBurstStyle { Crab, Fish, Turtle, Puffer }

    [RequireComponent(typeof(SpriteRenderer))]
    internal sealed class DefeatPixelBurst : MonoBehaviour
    {
        const int Size = 40;
        const int FramesPerSecond = 60;
        const int PulseFrames = 8;
        const int FrameCount = 16;
        static readonly Color32[] Palette =
        {
            new Color32(0x17, 0x69, 0xd2, 255),
            new Color32(0x35, 0xb9, 0xff, 255),
            new Color32(0x72, 0xe5, 0xf2, 255),
            new Color32(0xe0, 0xf8, 0xff, 255),
            new Color32(255, 255, 255, 255)
        };
        static readonly Sprite[][] CachedFrames = new Sprite[4][];

        SpriteRenderer display;
        Sprite[] frames;
        double startedAt;

        internal static void Spawn(SpriteRenderer body, DefeatBurstStyle style, Vector3 away)
        {
            var root = new GameObject("Defeat Pixel Burst");
            SceneManager.MoveGameObjectToScene(root, body.gameObject.scene);
            var burst = root.AddComponent<DefeatPixelBurst>();

            Camera view = Camera.main;
            Quaternion rotation = view != null ? view.transform.rotation : body.transform.rotation;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Bounds bounds = body.bounds;
            float side = Vector3.Dot(away, right) < 0f ? 1f : -1f;
            // Put the pulse near the contact side and below the face.
            Vector3 center = bounds.center + right * (bounds.size.x * .33f * side) - up * (bounds.size.y * .13f);
            root.transform.SetPositionAndRotation(center, rotation);
            root.transform.localScale = Vector3.one * Mathf.Clamp(bounds.size.y * .029f, .019f, .033f);

            burst.display = root.GetComponent<SpriteRenderer>();
            burst.display.sortingLayerID = body.sortingLayerID;
            burst.display.sortingOrder = body.sortingOrder + 1;
            burst.display.flipX = side > 0f;
            burst.frames = GetFrames(style);
            burst.display.sprite = burst.frames[0];
            burst.startedAt = Time.unscaledTimeAsDouble;
        }

        void Update()
        {
            int frame = (int)((Time.unscaledTimeAsDouble - startedAt) * FramesPerSecond);
            if (frame >= FrameCount)
            {
                Destroy(gameObject);
                return;
            }
            display.sprite = frames[frame];
        }

        static Sprite[] GetFrames(DefeatBurstStyle style)
        {
            int index = (int)style;
            if (CachedFrames[index] is { } cached && cached[0] != null) return cached;

            var (count, spread) = style switch
            {
                DefeatBurstStyle.Fish => (12, 15f),
                DefeatBurstStyle.Turtle => (8, 9f),
                DefeatBurstStyle.Puffer => (11, 13f),
                _ => (10, 12f)
            };
            var pixels = new Color32[Size * Size * FrameCount];
            for (int frame = 0; frame < FrameCount; frame++)
            {
                if (frame < PulseFrames) DrawPulse(pixels, frame);
                for (int fragment = 0; fragment < count; fragment++)
                {
                    int lifetime = 10 + fragment * 7 % 11 % 6;
                    if (frame >= lifetime) continue;

                    float angle = fragment * (2f * Mathf.PI / count) + .33f;
                    float progress = frame / (float)lifetime;
                    float distance = 3f + spread * (1f - (1f - progress) * (1f - progress)) * (.73f + .09f * (fragment % 4));
                    int x = Mathf.RoundToInt(Mathf.Cos(angle) * distance);
                    int y = Mathf.RoundToInt(Mathf.Sin(angle) * distance * .69f - 2f * progress * progress);
                    Color32 color = Palette[1 + fragment % 3];
                    color.a = (byte)Mathf.Lerp(245f, 75f, progress);
                    Put(pixels, frame, x, y, color);
                    if (fragment % 3 == 0 && frame < 5) Put(pixels, frame, x + 1, y, color);
                }
            }

            var atlas = new Texture2D(Size, Size * FrameCount, TextureFormat.RGBA32, false)
            {
                name = $"{style} Defeat Pixels",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            atlas.SetPixels32(pixels);
            atlas.Apply(false, true);

            var result = new Sprite[FrameCount];
            for (int frame = 0; frame < result.Length; frame++)
                result[frame] = Sprite.Create(atlas, new Rect(0, frame * Size, Size, Size),
                    new Vector2(.5f, .5f), 1f, 0, SpriteMeshType.FullRect);
            CachedFrames[index] = result;
            return result;
        }

        static void DrawPulse(Color32[] pixels, int frame)
        {
            int radius = 3 + Mathf.Min(frame, 4);
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                int vertical = (Mathf.Abs(y) * 3 + 1) / 2;
                int distance = Mathf.Max(Mathf.Abs(x), vertical) + Mathf.Min(Mathf.Abs(x), vertical) / 2;
                if (distance > radius || (frame >= 4 && distance < radius - 2)) continue;

                int colorIndex = Mathf.Clamp(radius - distance, 0, frame < 3 ? 4 : 3);
                Color32 color = Palette[colorIndex];
                color.a = (byte)(frame < 3 ? 255 : 245 - (frame - 3) * 42);
                Put(pixels, frame, x, y, color);
            }
        }

        static void Put(Color32[] pixels, int frame, int x, int y, Color32 color)
        {
            int index = (frame * Size + Size / 2 + y) * Size + Size / 2 + x;
            if (pixels[index].a <= color.a) pixels[index] = color;
        }
    }
}
