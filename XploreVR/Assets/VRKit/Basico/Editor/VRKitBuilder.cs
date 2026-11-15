#if UNITY_EDITOR
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System;
using TMPro;
using UnityEditor.Events;
using UnityEditor;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine;

namespace VRKit.EditorTools
{
    /// <summary>Menú GameObject › VRKit › Básico: el núcleo: piedra, botón, sistema de partida, menús, consola, subtítulos, objetivos, tutorial, escena VR y ayudas comunes.</summary>
    public static partial class VRKitBuilder
    {
        const string kBasic = kRoot + "Básico/";

        const string kRoot = "GameObject/VRKit/";

        const string kGenerated = "Assets/VRKit/Generated";


        // ------------------------------------------------------------------ Objetos

        [MenuItem(kBasic + "Piedra lanzable", false, 10)]
        static void CreateRock()
        {
            var go = Prim(PrimitiveType.Sphere, "Piedra", null, Vector3.zero, Vector3.one * 0.1f, Mat("Piedra", new Color(0.45f, 0.43f, 0.4f)), true);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<XRGrabInteractable>();
            go.AddComponent<Throwable>();
            go.AddComponent<DamageOnImpact>();
            go.AddComponent<ReturnToSpawn>();
            Finish(go);
        }


        [MenuItem(kBasic + "Botón 3D", false, 16)]
        static void CreateButton()
        {
            var root = new GameObject("Botón");
            Prim(PrimitiveType.Cube, "Base", root.transform, Vector3.zero, new Vector3(0.12f, 0.03f, 0.12f), Mat("Metal oscuro", new Color(0.15f, 0.15f, 0.17f)), true);
            var cap = Prim(PrimitiveType.Cylinder, "Pulsador", root.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.08f, 0.012f, 0.08f), Mat("Rojo botón", new Color(0.85f, 0.15f, 0.15f)), true);
            root.AddComponent<XRSimpleInteractable>();
            // Para pulsarlo con el dedo hace falta XRPokeFilter. Se añade por nombre para no depender de la versión.
            var pokeType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter, Unity.XR.Interaction.Toolkit");
            if (pokeType != null) root.AddComponent(pokeType);
            var b = root.AddComponent<VRButton>();
            SetRef(b, "pressVisual", cap.transform);
            Finish(root, 0.6f, 0.9f);
        }


        // ------------------------------------------------------------------ Sistemas

        [MenuItem(kBasic + "Sistema de partida (GameManager, fundidos, vida del jugador)", false, 30)]
        static void CreateGameSystems()
        {
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm == null)
            {
                var go = new GameObject("GameManager");
                gm = go.AddComponent<GameManager>();
                go.AddComponent<ScreenFader>();
                Undo.RegisterCreatedObjectUndo(go, "VRKit");
            }
            else if (gm.GetComponent<ScreenFader>() == null && Object.FindFirstObjectByType<ScreenFader>() == null)
            {
                Undo.AddComponent<ScreenFader>(gm.gameObject);
            }

            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
            {
                if (origin.GetComponent<PlayerHealth>() == null) Undo.AddComponent<PlayerHealth>(origin.gameObject);
                EnsurePlayerComponents(origin.gameObject);
                if (origin.gameObject.tag == "Untagged") origin.gameObject.tag = "Player";
                Debug.Log("[VRKit] PlayerHealth añadido al XR Origin y marcado con el tag Player.");
            }
            else
            {
                Debug.LogWarning("[VRKit] No hay XR Origin en la escena. Añade el 'XR Origin (XR Rig)' de Starter Assets y vuelve a ejecutar esto.");
            }
            Selection.activeGameObject = gm.gameObject;
        }


        [MenuItem(kBasic + "Menú de pausa (pausa, opciones, fin de partida)", false, 31)]
        static void CreatePauseMenu() { BuildMenu(false); }


        [MenuItem(kBasic + "Menú principal (escena de inicio)", false, 32)]
        static void CreateMainMenu() { BuildMenu(true); }


        [MenuItem(kBasic + "Consola de depuración en el visor", false, 33)]
        static void CreateDebugConsole()
        {
            var canvas = WorldCanvas("Consola VR", new Vector2(700f, 500f));
            var bg = Panel(canvas.transform, "Fondo", new Color(0f, 0f, 0f, 0.75f));
            var text = MakeText(bg.transform, "", 24, TextAlignmentOptions.TopLeft);
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(20f, 20f); rt.offsetMax = new Vector2(-20f, -20f);
            var console = canvas.gameObject.AddComponent<VRDebugConsole>();
            SetRef(console, "output", text);
            Finish(canvas.gameObject, 1.5f, 1.4f, -0.8f);
            canvas.transform.rotation = Quaternion.Euler(0f, -25f, 0f);
        }


        static void BuildMenu(bool isMainMenu)
        {
            EnsureEventSystem();
            var gm = Object.FindFirstObjectByType<GameManager>();
            var canvas = WorldCanvas(isMainMenu ? "Menú principal" : "Menú de pausa", new Vector2(820f, 1350f));
            var menuGo = new GameObject(isMainMenu ? "Main Menu Manager" : "Menu Manager");
            var menu = menuGo.AddComponent<VRMenuManager>();
            var loader = menuGo.AddComponent<SceneLoader>();
            var settings = menuGo.AddComponent<SettingsManager>();
            if (isMainMenu && Object.FindFirstObjectByType<ScreenFader>() == null) menuGo.AddComponent<ScreenFader>();

            var bgColor = new Color(0.08f, 0.09f, 0.14f, 0.92f);

            // ---- Pantalla principal (Pausa o Inicio) ----
            var main = Panel(canvas.transform, isMainMenu ? "Panel Inicio" : "Panel Pausa", bgColor);
            MakeText(main.transform, isMainMenu ? "Nombre del juego" : "Pausa", 64, TextAlignmentOptions.Center, 130f);

            // ---- Opciones (3 pantallas: movimiento · sonido y accesibilidad) ----
            var options = Panel(canvas.transform, "Panel Opciones", bgColor);
            MakeText(options.transform, "Opciones · Movimiento", 50, TextAlignmentOptions.Center, 90f);
            var tTurn = MakeToggle(options.transform, "Giro suave (si no, por saltos)");
            var lSnap = MakeText(options.transform, "Ángulo de giro: 45°", 32, TextAlignmentOptions.Left, 44f);
            var sSnap = MakeSlider(options.transform, 1f, 6f, true);
            var tMove = MakeToggle(options.transform, "Andar con joystick (si no, teletransporte)");
            MakeText(options.transform, "Velocidad al andar", 32, TextAlignmentOptions.Left, 44f);
            var sMove = MakeSlider(options.transform, 0.5f, 4f, false);
            var tVig = MakeToggle(options.transform, "Viñeta de confort al moverse");
            var tSeat = MakeToggle(options.transform, "Jugar sentado (sube la vista)");
            var tLeft = MakeToggle(options.transform, "Zurdo (mano dominante izquierda)");
            var bMore = MakeButton(options.transform, "Sonido y accesibilidad");
            var bBack = MakeButton(options.transform, "Volver");

            var options2 = Panel(canvas.transform, "Panel Opciones 2", bgColor);
            MakeText(options2.transform, "Sonido y accesibilidad", 50, TextAlignmentOptions.Center, 90f);
            var lVol = MakeText(options2.transform, "Volumen general", 32, TextAlignmentOptions.Left, 44f);
            var vol = MakeSlider(options2.transform, 0f, 1f, false);
            MakeText(options2.transform, "Música", 32, TextAlignmentOptions.Left, 44f);
            var sMusic = MakeSlider(options2.transform, 0f, 1f, false);
            MakeText(options2.transform, "Efectos", 32, TextAlignmentOptions.Left, 44f);
            var sSfx = MakeSlider(options2.transform, 0f, 1f, false);
            var tSubs = MakeToggle(options2.transform, "Subtítulos");
            MakeText(options2.transform, "Tamaño de los subtítulos", 32, TextAlignmentOptions.Left, 44f);
            var sSubSize = MakeSlider(options2.transform, 0.7f, 1.6f, false);
            MakeText(options2.transform, "Intensidad de la vibración", 32, TextAlignmentOptions.Left, 44f);
            var sHap = MakeSlider(options2.transform, 0f, 1f, false);
            var lDiff = MakeText(options2.transform, "Dificultad: Normal", 32, TextAlignmentOptions.Left, 44f);
            var sDiff = MakeSlider(options2.transform, 0f, 3f, true);
            var tFloat = MakeToggle(options2.transform, "Textos flotantes (+10, avisos)");
            var bBack2 = MakeButton(options2.transform, "Volver a Movimiento");

            UnityEventTools.AddPersistentListener(tTurn.onValueChanged, new UnityAction<bool>(settings.SetSmoothTurn));
            UnityEventTools.AddPersistentListener(sSnap.onValueChanged, new UnityAction<float>(settings.SetSnapAngle));
            UnityEventTools.AddPersistentListener(tMove.onValueChanged, new UnityAction<bool>(settings.SetSmoothMove));
            UnityEventTools.AddPersistentListener(sMove.onValueChanged, new UnityAction<float>(settings.SetMoveSpeed));
            UnityEventTools.AddPersistentListener(tVig.onValueChanged, new UnityAction<bool>(settings.SetVignette));
            UnityEventTools.AddPersistentListener(tSeat.onValueChanged, new UnityAction<bool>(settings.SetSeated));
            UnityEventTools.AddPersistentListener(tLeft.onValueChanged, new UnityAction<bool>(settings.SetLeftHanded));
            UnityEventTools.AddPersistentListener(vol.onValueChanged, new UnityAction<float>(settings.SetVolume));
            UnityEventTools.AddPersistentListener(sMusic.onValueChanged, new UnityAction<float>(settings.SetMusicVolume));
            UnityEventTools.AddPersistentListener(sSfx.onValueChanged, new UnityAction<float>(settings.SetSfxVolume));
            UnityEventTools.AddPersistentListener(tSubs.onValueChanged, new UnityAction<bool>(settings.SetSubtitles));
            UnityEventTools.AddPersistentListener(sSubSize.onValueChanged, new UnityAction<float>(settings.SetSubtitleSize));
            UnityEventTools.AddPersistentListener(sHap.onValueChanged, new UnityAction<float>(settings.SetHaptics));
            UnityEventTools.AddPersistentListener(sDiff.onValueChanged, new UnityAction<float>(settings.SetDifficulty));
            UnityEventTools.AddPersistentListener(tFloat.onValueChanged, new UnityAction<bool>(settings.SetFloatingTexts));
            UnityEventTools.AddObjectPersistentListener(bBack.onClick, new UnityAction<GameObject>(menu.ShowPanel), main);
            UnityEventTools.AddObjectPersistentListener(bMore.onClick, new UnityAction<GameObject>(menu.ShowPanel), options2);
            UnityEventTools.AddObjectPersistentListener(bBack2.onClick, new UnityAction<GameObject>(menu.ShowPanel), options);
            SetRef(settings, "smoothTurnToggle", tTurn);
            SetRef(settings, "snapAngleSlider", sSnap);
            SetRef(settings, "snapAngleLabel", lSnap);
            SetRef(settings, "smoothMoveToggle", tMove);
            SetRef(settings, "moveSpeedSlider", sMove);
            SetRef(settings, "vignetteToggle", tVig);
            SetRef(settings, "seatedToggle", tSeat);
            SetRef(settings, "leftHandedToggle", tLeft);
            SetRef(settings, "volumeSlider", vol);
            SetRef(settings, "volumeLabel", lVol);
            SetRef(settings, "musicSlider", sMusic);
            SetRef(settings, "sfxSlider", sSfx);
            SetRef(settings, "subtitlesToggle", tSubs);
            SetRef(settings, "subtitleSizeSlider", sSubSize);
            SetRef(settings, "hapticsSlider", sHap);
            SetRef(settings, "difficultySlider", sDiff);
            SetRef(settings, "difficultyLabel", lDiff);
            SetRef(settings, "floatingTextToggle", tFloat);
            AutoWireSettings(settings);

            if (isMainMenu)
            {
                var bPlay = MakeButton(main.transform, "Jugar");
                var bOpt = MakeButton(main.transform, "Opciones");
                var bQuit = MakeButton(main.transform, "Salir");
                UnityEventTools.AddStringPersistentListener(bPlay.onClick, new UnityAction<string>(loader.LoadScene), "Nivel1");
                UnityEventTools.AddObjectPersistentListener(bOpt.onClick, new UnityAction<GameObject>(menu.ShowPanel), options);
                UnityEventTools.AddPersistentListener(bQuit.onClick, new UnityAction(loader.QuitGame));
                SetRefs(menu, main, main, options, options2);
                SetBool(menu, "openOnStart", true);
                SetBool(menu, "allowToggle", false);
                SetBool(menu, "pauseGame", false);
                Debug.Log("[VRKit] Menú principal creado. El botón Jugar carga la escena 'Nivel1': cambia el nombre en su OnClick si tu escena se llama distinto.");
            }
            else
            {
                var bResume = MakeButton(main.transform, "Continuar");
                var bRestart = MakeButton(main.transform, "Reiniciar nivel");
                var bOpt = MakeButton(main.transform, "Opciones");
                var bMenu = MakeButton(main.transform, "Menú principal");
                UnityEventTools.AddPersistentListener(bResume.onClick, new UnityAction(menu.Close));
                UnityEventTools.AddPersistentListener(bRestart.onClick, new UnityAction(loader.ReloadScene));
                UnityEventTools.AddObjectPersistentListener(bOpt.onClick, new UnityAction<GameObject>(menu.ShowPanel), options);
                UnityEventTools.AddStringPersistentListener(bMenu.onClick, new UnityAction<string>(loader.LoadScene), "MainMenu");

                var over = Panel(canvas.transform, "Panel Game Over", bgColor);
                MakeText(over.transform, "Has perdido", 64, TextAlignmentOptions.Center, 130f);
                var bRetry = MakeButton(over.transform, "Reintentar");
                var bMenu2 = MakeButton(over.transform, "Menú principal");
                UnityEventTools.AddPersistentListener(bRetry.onClick, new UnityAction(loader.ReloadScene));
                UnityEventTools.AddStringPersistentListener(bMenu2.onClick, new UnityAction<string>(loader.LoadScene), "MainMenu");

                var win = Panel(canvas.transform, "Panel Victoria", bgColor);
                MakeText(win.transform, "¡Victoria!", 64, TextAlignmentOptions.Center, 130f);
                var bAgain = MakeButton(win.transform, "Jugar otra vez");
                var bMenu3 = MakeButton(win.transform, "Menú principal");
                UnityEventTools.AddPersistentListener(bAgain.onClick, new UnityAction(loader.ReloadScene));
                UnityEventTools.AddStringPersistentListener(bMenu3.onClick, new UnityAction<string>(loader.LoadScene), "MainMenu");

                SetRefs(menu, main, main, options, options2, over, win);
                if (gm != null)
                {
                    UnityEventTools.AddObjectPersistentListener(gm.onGameOver, new UnityAction<GameObject>(menu.OpenWithPanel), over);
                    UnityEventTools.AddObjectPersistentListener(gm.onVictory, new UnityAction<GameObject>(menu.OpenWithPanel), win);
                    EditorUtility.SetDirty(gm);
                }
                else
                {
                    Debug.LogWarning("[VRKit] No hay GameManager: crea antes el 'Sistema de partida' para que el menú de Game Over y Victoria se abra solo.");
                }
            }

            SetRef(menu, "menuRoot", canvas.gameObject);
            options.SetActive(false);
            options2.SetActive(false);
            Undo.RegisterCreatedObjectUndo(canvas.gameObject, "VRKit");
            Undo.RegisterCreatedObjectUndo(menuGo, "VRKit");
            Selection.activeGameObject = menuGo;
        }


        // ------------------------------------------------------------------ UI helpers

        static TMP_DefaultControls.Resources TmpRes()
        {
            var r = new TMP_DefaultControls.Resources();
            r.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            r.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            r.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            r.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            return r;
        }


        static DefaultControls.Resources UiRes()
        {
            var r = new DefaultControls.Resources();
            r.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            r.background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            r.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            r.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
            return r;
        }


        static Canvas WorldCanvas(string name, Vector2 sizePx)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>(); // para poder apuntar y pulsar con los mandos
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * 0.001f; // 1000 px = 1 metro
            var cam = Camera.main;
            if (cam != null) canvas.worldCamera = cam;
            return canvas;
        }


        static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(60, 60, 40, 40);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go;
        }


        static TMP_Text MakeText(Transform parent, string content, float size, TextAlignmentOptions align, float height = 0f)
        {
            var go = TMP_DefaultControls.CreateText(TmpRes());
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TMP_Text>();
            t.text = content;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            if (height > 0f) go.AddComponent<LayoutElement>().preferredHeight = height;
            return t;
        }


        static Button MakeButton(Transform parent, string label)
        {
            var go = TMP_DefaultControls.CreateButton(TmpRes());
            go.name = "Botón " + label;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 90f;
            var t = go.GetComponentInChildren<TMP_Text>();
            t.text = label;
            t.fontSize = 40;
            t.color = new Color(0.1f, 0.1f, 0.12f);
            return go.GetComponent<Button>();
        }


        static Toggle MakeToggle(Transform parent, string label)
        {
            var go = DefaultControls.CreateToggle(UiRes());
            go.name = "Toggle " + label;
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 70f;

            var bg = (RectTransform)go.transform.Find("Background");
            if (bg != null) { bg.anchoredPosition = new Vector2(40f, -40f); bg.sizeDelta = new Vector2(60f, 60f); }

            // Cambia la etiqueta de texto clásico por TextMeshPro
            var labelTf = go.transform.Find("Label");
            if (labelTf != null)
            {
                var legacy = labelTf.GetComponent<UnityEngine.UI.Text>();
                if (legacy != null) Object.DestroyImmediate(legacy);
                var tmp = labelTf.gameObject.AddComponent<TextMeshProUGUI>();
                tmp.text = label;
                tmp.fontSize = 34;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var lrt = (RectTransform)labelTf;
                lrt.offsetMin = new Vector2(90f, 0f);
                lrt.offsetMax = Vector2.zero;
            }
            return go.GetComponent<Toggle>();
        }


        static Slider MakeSlider(Transform parent, float min, float max, bool whole)
        {
            var s = MakeSlider(parent);
            s.minValue = min; s.maxValue = max; s.wholeNumbers = whole;
            s.value = whole ? Mathf.Round((min + max) * 0.5f) : (min + max) * 0.5f;
            return s;
        }


        static Slider MakeSlider(Transform parent)
        {
            var go = DefaultControls.CreateSlider(UiRes());
            go.name = "Slider";
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>().preferredHeight = 44f;
            var handle = go.transform.Find("Handle Slide Area/Handle") as RectTransform;
            if (handle != null) handle.sizeDelta = new Vector2(50f, 0f);
            var s = go.GetComponent<Slider>();
            s.minValue = 0f; s.maxValue = 1f; s.value = 0.8f;
            return s;
        }


        static void EnsureEventSystem()
        {
            var es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(XRUIInputModule));
                Undo.RegisterCreatedObjectUndo(go, "VRKit");
                return;
            }
            if (es.GetComponent<XRUIInputModule>() == null)
                Debug.LogWarning("[VRKit] El EventSystem de la escena no tiene 'XR UI Input Module': cámbiale el módulo de entrada para poder usar los menús con los mandos.");
        }


        static void SetRefs(VRMenuManager menu, GameObject defaultPanel, params GameObject[] panels)
        {
            var so = new SerializedObject(menu);
            so.FindProperty("defaultPanel").objectReferenceValue = defaultPanel;
            var arr = so.FindProperty("panels");
            arr.arraySize = panels.Length;
            for (int i = 0; i < panels.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        // ------------------------------------------------------------------ Generic helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }


        static Transform Empty(string name, Transform parent, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }


        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/VRKit")) AssetDatabase.CreateFolder("Assets", "VRKit");
            if (!AssetDatabase.IsValidFolder(kGenerated)) AssetDatabase.CreateFolder("Assets/VRKit", "Generated");
        }


        static Material Mat(string name, Color color)
        {
            return MatWith(name, color, "Universal Render Pipeline/Lit", "Standard");
        }


        static Material UnlitMat(string name, Color color)
        {
            return MatWith(name + " (unlit)", color, "Universal Render Pipeline/Unlit", "Unlit/Color");
        }


        static Material MatWith(string name, Color color, string shader, string fallback)
        {
            EnsureFolder();
            string path = kGenerated + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find(shader);
            if (sh == null) sh = Shader.Find(fallback);
            m = new Material(sh);
            m.color = color;
            if (color.a < 1f && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); // transparente en URP
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = 3000;
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }


        static GameObject SavePrefab(GameObject go, string path)
        {
            EnsureFolder();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            return prefab;
        }


        static void Finish(GameObject go, float forward = 0.6f, float height = 1.1f, float side = 0f)
        {
            Vector3 basePos = Vector3.zero;
            Vector3 fwd = Vector3.forward;
            Vector3 right = Vector3.right;
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin != null)
            {
                basePos = origin.transform.position;
                fwd = origin.transform.forward;
                right = origin.transform.right;
            }
            go.transform.position = basePos + fwd * forward + Vector3.up * height + right * side;
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }


        static void SetRef(Object target, string prop, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetFloat(Object target, string prop, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetBool(Object target, string prop, bool value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetEnum(Object target, string prop, int value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        [MenuItem(kBasic + "Subtítulos", false, 90)] static void MenuSubtitles() { Selection.activeGameObject = EnsureSubtitles().gameObject; }

        [MenuItem(kBasic + "Tablón de objetivos", false, 91)] static void MenuObjectives() { Finish(BuildObjectiveBoard(new[] { "monedas:Recoge 5 monedas:5", "diana:Acierta en 3 dianas:3" }).gameObject, 2f, 0f); }

        [MenuItem(kBasic + "Tutorial pegado a los mandos", false, 92)] static void MenuTutorial() { Selection.activeGameObject = BuildTutorial(); }

        [MenuItem(kBasic + "Secuencia de eventos", false, 93)] static void MenuSequence() { var go = new GameObject("Secuencia"); go.AddComponent<EventSequence>(); Undo.RegisterCreatedObjectUndo(go, "VRKit"); Selection.activeGameObject = go; }


        // ================================================================== Poderes

        static GameObject OriginOrWarn()
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null) { Debug.LogWarning("[VRKit] Los poderes van en el jugador: añade antes el XR Origin (Starter Assets)."); return null; }
            EnsurePlayerComponents(origin.gameObject);
            return origin.gameObject;
        }


        // ================================================================== Narrativa

        static SubtitleSystem EnsureSubtitles()
        {
            var s = Object.FindFirstObjectByType<SubtitleSystem>();
            if (s != null) return s;
            var go = new GameObject("Subtítulos");
            s = go.AddComponent<SubtitleSystem>();
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            return s;
        }


        /// <summary>Objetivos en formato «id:descripción:cantidad».</summary>
        static ObjectiveTracker BuildObjectiveBoard(string[] objectives)
        {
            var root = new GameObject("Tablón de objetivos");
            Prim(PrimitiveType.Cube, "Tablón", root.transform, new Vector3(0f, 1.6f, 0.04f), new Vector3(1.4f, 0.9f, 0.04f), Mat("Madera oscura", new Color(0.25f, 0.16f, 0.1f)), true);
            Prim(PrimitiveType.Cylinder, "Pata", root.transform, new Vector3(0f, 0.6f, 0.06f), new Vector3(0.06f, 0.6f, 0.06f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), false);
            Label3D(root.transform, "<b>OBJETIVOS</b>", new Vector3(0f, 1.95f, 0f), 0.7f, new Color(1f, 0.85f, 0.4f));
            var label = Label3D(root.transform, "", new Vector3(0f, 1.5f, 0f), 0.5f, Color.white);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.rectTransform.sizeDelta = new Vector2(1.25f, 0.6f);
            var t = root.AddComponent<ObjectiveTracker>();
            SetRef(t, "listLabel", label);
            foreach (var o in objectives)
            {
                var parts = o.Split(':');
                t.AddObjective(parts[0], parts[1], parts.Length > 2 ? int.Parse(parts[2]) : 1);
            }
            return t;
        }


        static GameObject BuildTutorial()
        {
            var root = new GameObject("Tutorial");
            var p1 = Prompt(root.transform, "1 · Grip", Hand.Right, "Aprieta el <b>grip</b> cerca de un objeto para cogerlo", "{GripButton}", "tutorial_grip");
            var p2 = Prompt(root.transform, "2 · Gatillo", Hand.Right, "Con algo en la mano, el <b>gatillo</b> lo usa (disparar, encender...)", "{TriggerButton}", "tutorial_gatillo");
            var p3 = Prompt(root.transform, "3 · Menú", Hand.Left, "El botón <b>Menú</b> abre la pausa y las opciones", "{MenuButton}", "tutorial_menu");
            SetRef(p1, "next", p2);
            SetRef(p2, "next", p3);
            p2.gameObject.SetActive(false);
            p3.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(root, "VRKit");
            return root;
        }


        static TutorialPrompt Prompt(Transform parent, string name, Hand hand, string text, string button, string flag)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<TutorialPrompt>();
            SetEnum(p, "hand", (int)hand);
            SetString(p, "message", text);
            SetString(p, "completeOnButton", button);
            SetString(p, "rememberFlag", flag);
            return p;
        }


        // ================================================================== Jugador

        static readonly string[] kLocomotionTypes = { "ContinuousMoveProvider", "DynamicMoveProvider", "SnapTurnProvider", "ContinuousTurnProvider", "TeleportationProvider", "GravityProvider", "ClimbProvider", "JumpProvider" };


        /// <summary>VRPlayer en el XR Origin, con la lista de componentes de locomoción ya rellena.</summary>
        static void EnsurePlayerComponents(GameObject origin)
        {
            var vp = origin.GetComponent<VRPlayer>();
            if (vp == null) vp = Undo.AddComponent<VRPlayer>(origin);
            var loco = new List<Object>();
            foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && Array.IndexOf(kLocomotionTypes, mb.GetType().Name) >= 0) loco.Add(mb);
            SetObjArray(vp, "locomotionComponents", loco.ToArray());
        }


        /// <summary>Rellena el SettingsManager con los componentes del XR Origin (gestores de mando, proveedores, viñeta).</summary>
        static void AutoWireSettings(SettingsManager settings)
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null) return;
            var managers = new List<Object>();
            var providers = new List<Object>();
            foreach (var mb in origin.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n == "ControllerInputActionManager") managers.Add(mb);
                var t = mb.GetType();
                if (t.GetProperty("turnAmount") != null || t.GetProperty("turnSpeed") != null || t.GetProperty("moveSpeed") != null) providers.Add(mb);
            }
            SetObjArray(settings, "controllerInputManagers", managers.ToArray());
            SetObjArray(settings, "locomotionProviders", providers.ToArray());
            foreach (var tr in origin.GetComponentsInChildren<Transform>(true))
                if (tr.name.Replace(" ", "").ToLowerInvariant().Contains("tunnelingvignette")) { SetRef(settings, "comfortVignette", tr.gameObject); break; }
        }


        // ================================================================== Ayudas

        static TextMeshPro Label3D(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Texto");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity; // TextMeshPro se lee desde -Z: de frente para un jugador que mira hacia +Z
            var t = go.AddComponent<TextMeshPro>();
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
            return t;
        }


        static void SetInt(Object target, string prop, int value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.intValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetString(Object target, string prop, string value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetVec(Object target, string prop, Vector3 value)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.vector3Value = value; so.ApplyModifiedPropertiesWithoutUndo();
        }


        static void SetObjArray(Object target, string prop, Object[] values)
        {
            var so = new SerializedObject(target); var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError("[VRKit] No existe el campo '" + prop + "' en " + target.GetType().Name); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }


        // ================================================================== Ayudas de la escena

        [MenuItem(kBasic + "Preparar escena VR (suelo, jugador, partida y menú)", false, 0)]
        static void PrepareVRScene()
        {
            EnsureFolder();
            var root = new GameObject("Escena VR");
            var geo = new GameObject("Geometría");
            geo.transform.SetParent(root.transform, false);
            var floor = Prim(PrimitiveType.Cube, "Suelo", geo.transform, new Vector3(0f, -0.05f, 0f), new Vector3(20f, 0.1f, 20f), Mat("Suelo", new Color(0.4f, 0.44f, 0.42f)), true);
            AddByName(floor, "UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit",
                             "UnityEngine.XR.Interaction.Toolkit.TeleportationArea, Unity.XR.Interaction.Toolkit");
            BakeNavMeshOn(geo);
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.transform.SetParent(root.transform, true);
            }
            EnsureXROrigin();
            CreateGameSystems();
            BuildMenu(false);
            EnsureSubtitles().transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "VRKit");
            Selection.activeGameObject = root;
            Debug.Log("[VRKit] Escena VR preparada: suelo con teletransporte, jugador con vida, GameManager, fundidos, menú de pausa con opciones y subtítulos. Añade lo que quieras desde GameObject › VRKit.");
        }

        /// <summary>Pone el XR Origin de Starter Assets si no hay uno (y quita la cámara normal que sobra).</summary>
        static Unity.XR.CoreUtils.XROrigin EnsureXROrigin()
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null)
            {
                var prefab = FindXROriginPrefab();
                if (prefab == null)
                {
                    Debug.LogWarning("[VRKit] No encuentro el prefab 'XR Origin (XR Rig)'. Importa Starter Assets (Package Manager › XR Interaction Toolkit › Samples) y vuelve a intentarlo, o añádelo a mano.");
                    return null;
                }
                var o = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                origin = o.GetComponent<Unity.XR.CoreUtils.XROrigin>();
                foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    if (!cam.transform.IsChildOf(o.transform)) Object.DestroyImmediate(cam.gameObject);
                Undo.RegisterCreatedObjectUndo(o, "VRKit");
            }
            if (origin != null) origin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return origin;
        }

        static GameObject FindXROriginPrefab()
        {
            foreach (var guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("XR Origin (XR Rig)") || path.EndsWith("XR Origin.prefab"))
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            return null;
        }


        static Component AddByName(GameObject go, params string[] typeNames)
        {
            foreach (var n in typeNames)
            {
                var t = Type.GetType(n);
                if (t != null) return go.AddComponent(t);
            }
            Debug.LogWarning("[VRKit] No se encontró el componente " + typeNames[0] + " (¿otra versión de XRI?). Añádelo a mano.");
            return null;
        }


        static void BakeNavMeshOn(GameObject geo)
        {
            var type = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
            if (type == null) { Debug.LogWarning("[VRKit] Instala el paquete AI Navigation y hornea el NavMesh (Geometría › NavMeshSurface › Bake) para los enemigos de la arena."); return; }
            var surface = geo.AddComponent(type);
            var collect = type.GetProperty("collectObjects");
            if (collect != null) collect.SetValue(surface, Enum.ToObject(collect.PropertyType, 2)); // solo los hijos
            var bake = type.GetMethod("BuildNavMesh");
            if (bake != null) bake.Invoke(surface, null);
        }


        static void Zone(GameObject parent, string name, Vector3 center, Vector2 size, Color color)
        {
            var z = Prim(PrimitiveType.Cube, name, parent.transform, center + Vector3.up * 0.005f, new Vector3(size.x, 0.01f, size.y), Mat(name, color), false);
            z.isStatic = true;
        }


        /// <summary>Un grupo de estación. yaw = 180 para las que quedan detrás del inicio (así miran al jugador).</summary>
        static GameObject Group(GameObject root, string name, Vector3 pos, float yaw = 0f)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g;
        }


        static void Table(GameObject parent, Vector3 pos, Vector3 size)
        {
            Prim(PrimitiveType.Cube, "Mesa", parent.transform, pos + Vector3.up * size.y * 0.5f, size, Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), true);
        }


        static void Sign(GameObject parent, string title, string body, Vector3 pos)
        {
            var s = new GameObject("Cartel " + title);
            s.transform.SetParent(parent.transform, false);
            s.transform.localPosition = pos;
            Prim(PrimitiveType.Cube, "Tabla", s.transform, new Vector3(0f, 0f, 0.04f), new Vector3(2.6f, 1f, 0.05f), Mat("Madera oscura", new Color(0.25f, 0.16f, 0.1f)), false);
            var t = Label3D(s.transform, "<b><size=140%>" + title + "</size></b>\n" + body, Vector3.zero, 0.55f, Color.white);
            t.rectTransform.sizeDelta = new Vector2(2.4f, 0.9f);
        }


        static void Say(EventSequence seq, SubtitleSystem subs, float delay, string line)
        {
            var step = seq.AddStep(line.Length > 30 ? line.Substring(0, 30) + "…" : line, delay);
            UnityEventTools.AddStringPersistentListener(step.action, new UnityAction<string>(subs.Say), line);
        }


        /// <summary>El objeto raíz más reciente con ese nombre (los menús de objetos lo crean al final de la escena).</summary>
        static GameObject Recent(string name)
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = roots.Length - 1; i >= 0; i--) if (roots[i].name == name) return roots[i];
            return GameObject.Find(name);
        }


        static void Place(GameObject go, GameObject parent, Vector3 localPos, Quaternion localRot)
        {
            if (go == null) return;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
        }
    }
}
#endif
