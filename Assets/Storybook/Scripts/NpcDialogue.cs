using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Storybook
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class NpcDialogue : MonoBehaviour
    {
        [SerializeField] string displayName;
        [SerializeField] string interactionPrompt;
        [SerializeField, TextArea] string[] lines;
        [SerializeField] Transform indicatorAnchor;
        [SerializeField] SortingGroup depthSorting;
        [SerializeField, Min(.1f)] float interactionRange = 1.35f;
        [SerializeField] GroundShadow.Style groundShadow = GroundShadow.Style.Character;

        static NpcDialogue active;
        static int closedFrame = -1;
        HongGildong player;
        PlayerHealth health;
        PalaceDungeon dungeon;
        int line;
        Texture2D pixel;
        Font font;
        GUIStyle title, body, hint;

        public virtual string DisplayName => displayName;
        public Transform IndicatorAnchor => indicatorAnchor;
        public bool IsInteracting => active == this;
        internal static bool BlocksInteraction => active != null || closedFrame == Time.frameCount;

        protected virtual void Awake()
        {
            dungeon = GetComponentInParent<PalaceDungeon>();
            GroundShadow.Attach(transform, groundShadow, -2, depthSorting != null ? depthSorting.sortingLayerID : 0);
            player = FindFirstObjectByType<HongGildong>();
            if (player != null) health = player.GetComponent<PlayerHealth>();
        }

        protected virtual void Update()
        {
            if (RelicInventoryPanel.BlocksGameplay) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            var keyboard = Keyboard.current;
            if (IsInteracting)
            {
                if (health == null || health.IsDead || StorybookTransition.IsBusy ||
                    !PlayerInRange(out _) || keyboard?.escapeKey.wasPressedThisFrame == true)
                    EndInteraction();
                else if (keyboard?.fKey.wasPressedThisFrame == true && ++line >= (lines?.Length ?? 0))
                    EndInteraction();
                return;
            }

            if (keyboard?.fKey.wasPressedThisFrame != true || !CanApproach(out Vector3 offset)) return;
            line = 0;
            active = this;
            player.enabled = false;
            FacePlayer(offset);
        }

        bool CanApproach(out Vector3 offset)
        {
            offset = default;
            return !BlocksInteraction && !StorybookTransition.IsBusy && player != null && player.enabled &&
                health != null && !health.IsDead && PlayerInRange(out offset) &&
                (dungeon == null || !dungeon.ShopClaimsInteraction) && CanStart(player);
        }

        bool PlayerInRange(out Vector3 offset)
        {
            offset = default;
            if (player == null || !player.gameObject.activeInHierarchy) return false;
            offset = transform.InverseTransformDirection(player.transform.position - transform.position);
            offset.y = 0f;
            return offset.sqrMagnitude <= interactionRange * interactionRange;
        }

        protected virtual bool CanStart(HongGildong candidate) => true;
        protected virtual void FacePlayer(Vector3 offset) { }
        protected virtual void FaceFront() { }

        public void EndInteraction()
        {
            if (!IsInteracting) return;
            active = null;
            closedFrame = Time.frameCount;
            FaceFront();
            if (player != null && player.gameObject.activeInHierarchy && health != null && !health.IsDead &&
                !StorybookTransition.IsBusy)
                player.enabled = true;
        }

        protected virtual void OnDisable() => EndInteraction();

        void LateUpdate()
        {
            if (depthSorting != null && player != null)
                depthSorting.sortingOrder = player.transform.position.z > transform.position.z ? 3 : -1;
        }

        protected void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (PalaceDeveloperMenu.BlocksGameplay) return;
#endif
            if (!IsInteracting && !CanApproach(out _)) return;
            PrepareStyles();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1100f, Screen.height / 650f), .5f, 2f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            if (IsInteracting)
            {
                float panelWidth = Mathf.Min(width - 24f, 680f);
                var panel = new Rect((width - panelWidth) * .5f, height - 188f, panelWidth, 135f);
                Frame(panel);
                GUI.Label(new Rect(panel.x + 18, panel.y + 10, panel.width - 36, 28), DisplayName, title);
                if (lines != null && line < lines.Length)
                    GUI.Label(new Rect(panel.x + 18, panel.y + 42, panel.width - 36, 57), lines[line], body);
                string action = line + 1 < (lines?.Length ?? 0) ? "F — 다음" : "F — 닫기";
                GUI.Label(new Rect(panel.x + 14, panel.yMax - 31, panel.width - 28, 24), action + "   Esc — 종료", hint);
            }
            else
            {
                var camera = Camera.main;
                if (camera != null)
                {
                    Vector3 anchor = indicatorAnchor != null ? indicatorAnchor.position : transform.position + Vector3.up;
                    Vector3 point = camera.WorldToScreenPoint(anchor);
                    if (point.z > 0f && camera.pixelRect.Contains(new Vector2(point.x, point.y)))
                    {
                        float promptWidth = string.IsNullOrEmpty(interactionPrompt) ? 184f : 260f;
                        var rect = new Rect(Mathf.Clamp(point.x / scale - promptWidth * .5f, 4f, width - promptWidth - 4f),
                            Mathf.Max(4f, (Screen.height - point.y) / scale - 32f), promptWidth, 30f);
                        Frame(rect);
                        GUI.Label(rect, string.IsNullOrEmpty(interactionPrompt) ? "F — 대화" : interactionPrompt, title);
                    }
                }
            }
            GUI.matrix = previous;
        }

        void PrepareStyles()
        {
            if (pixel != null) return;
            pixel = Texture2D.whiteTexture;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo" }, 20);
            title = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = new Color32(236, 199, 122, 255);
            body = new GUIStyle(title) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
            body.normal.textColor = new Color32(248, 231, 198, 255);
            hint = new GUIStyle(title) { fontSize = 15, alignment = TextAnchor.MiddleRight };
            if (font != null) title.font = body.font = hint.font = font;
        }

        void Frame(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color32(205, 166, 96, 255);
            GUI.DrawTexture(rect, pixel);
            GUI.color = new Color32(20, 17, 39, 255);
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4), pixel);
            GUI.color = previous;
        }

        protected void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
