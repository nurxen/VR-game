using UnityEngine;

namespace StylizedWorldsSampler.Demo
{
    /// <summary>
    /// A small panel in the demo: the controls, and what the full packs add. Press H to hide it. Not needed in your own
    /// scenes.
    /// </summary>
    public class SamplerInfo : MonoBehaviour
    {
        [TextArea] public string title = "Stylized Worlds - Free Sampler";
        [TextArea] public string body =
            "Right mouse: look   WASD: fly   Shift: faster   Q/E: down/up   H: hide\n\n" +
            "The full packs:\n" +
            "Stylized Biomes - 12 open-world environments, 204 models, seasons, weather, day & night, water, lava, footprints\n" +
            "Stylized Worlds: Structures - 12 villages and 12 castles, 323 pieces, walk-in interiors, every door opens";
        [Tooltip("Opened by the button (the store page or the publisher's site).")]
        public string moreUrl = "https://hadystudios.com";

        bool hidden;
        GUIStyle titleStyle, bodyStyle;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current?.hKey.wasPressedThisFrame == true) hidden = !hidden;
#else
            if (Input.GetKeyDown(KeyCode.H)) hidden = !hidden;
#endif
        }

        void OnGUI()
        {
            if (hidden) return;
            titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            float w = Mathf.Min(460f, Screen.width - 24f);
            GUILayout.BeginArea(new Rect(12f, 12f, w, 230f), GUI.skin.box);
            GUILayout.Label(title, titleStyle);
            GUILayout.Label(body, bodyStyle);
            if (!string.IsNullOrEmpty(moreUrl) && GUILayout.Button("See the full packs", GUILayout.Width(180f)))
                Application.OpenURL(moreUrl);
            GUILayout.EndArea();
        }
    }
}
