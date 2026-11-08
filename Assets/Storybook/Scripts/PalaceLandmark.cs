using UnityEngine;

namespace Storybook
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class PalaceLandmark : MonoBehaviour
    {
        [SerializeField] string heading;
        [SerializeField] string description;

        bool playerPresent;
        Font font;
        GUIStyle title, detail, panel;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<HongGildong>() != null) playerPresent = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<HongGildong>() != null) playerPresent = false;
        }

        void OnGUI()
        {
            if (!playerPresent) return;
            if (title == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 26);
                title = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = 24, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                title.normal.textColor = new Color(.8f, 1f, .94f);
                detail = new GUIStyle(title) { fontSize = 15, fontStyle = FontStyle.Normal, wordWrap = true };
                detail.normal.textColor = new Color(.76f, .89f, .88f);
                panel = new GUIStyle(GUI.skin.box);
            }

            float width = Mathf.Min(440f, Screen.width - 24f);
            var rect = new Rect((Screen.width - width) * .5f, 94f, width, 78f);
            GUI.Box(rect, GUIContent.none, panel);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, width - 24f, 28f), heading, title);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 34f, width - 24f, 37f), description, detail);
        }

        void OnDestroy()
        {
            if (font != null) Destroy(font);
        }
    }
}
