#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRKit.EditorTools
{
    /// <summary>
    /// Ventana «Tools › VRKit › Módulos y tipos de juego».
    ///  1. Comprueba el proyecto: paquetes de XR, Starter Assets, AI Navigation, TextMeshPro…
    ///  2. Enseña qué módulos tenéis y para qué sirve cada uno.
    ///  3. Deja quedarse solo con los módulos de un tipo de juego (lo demás va a la papelera, se puede recuperar).
    ///
    /// Los módulos solo dependen del Básico, nunca unos de otros, así que se pueden quitar de uno en uno.
    /// La carpeta Ejemplos usa los seis primeros (no el Mundo de bloques): se quita sola si quitáis alguno de ellos.
    /// </summary>
    public class VRKitModulesWindow : EditorWindow
    {
        class Module
        {
            public string folder, name, contents, useFor;
            public Module(string f, string n, string c, string u) { folder = f; name = n; contents = c; useFor = u; }
        }

        class Preset
        {
            public string name, description;
            public string[] modules;
            public Preset(string n, string d, params string[] m) { name = n; description = d; modules = m; }
        }

        static readonly Module[] kModules =
        {
            new Module("Mecanismos", "Mecanismos", "Palanca, rueda o dial, cajón, puerta, caja fuerte de combinación", "Puzles, escape rooms, cabinas de naves, máquinas"),
            new Module("ObjetosEInventario", "Objetos e inventario", "Coleccionables, pociones que se beben, mochila al hombro, caldero con recetas, linterna, objetos rompibles", "Exploración, cozy, alquimia, terror, recolección"),
            new Module("ArmasADistancia", "Armas a distancia", "Arco con flechas, pistola, varita, ballesta, dianas", "Tiro, shooters, galerías de tiro, caza"),
            new Module("CuerpoACuerpo", "Cuerpo a cuerpo", "Espada, hacha, escudo que bloquea, granadas y bombas", "Acción, combate medieval, defensa"),
            new Module("Poderes", "Poderes", "Hechizos con la mano, telequinesis, gancho o telaraña, tiempo bala, portales", "Magia, superpoderes, dioses, sueños"),
            new Module("Enemigos", "Enemigos", "Enemigo a pie (NavMesh), dron volador, oleadas", "Cualquier juego con rivales: arenas, hordas, jefes"),
            new Module("MundoDeBloques", "Mundo de bloques", "Mundo de cubos que se genera solo, romper y poner bloques, inventario en la muñeca, fabricar, herramientas, día y noche, criaturas, guardado. También en maqueta sobre la mesa", "Juegos tipo Minecraft, construir, sandbox, supervivencia"),
            new Module("MundoAbierto", "Mundo abierto", "Terreno infinito con semilla, biomas (pradera, bosque, ríos, desierto, montaña, nieve), vegetación, cuevas, día y noche con estrellas, criaturas con NavMesh, música por zona, guardado por zonas", "Exploración, aventura tranquila, supervivencia, viajes"),
        };

        static readonly Preset[] kPresets =
        {
            new Preset("Puzle / escape room", "Manipular cosas, abrir puertas, encontrar objetos.", "Mecanismos", "ObjetosEInventario"),
            new Preset("Shooter / arena", "Disparar, sobrevivir a oleadas.", "ArmasADistancia", "Enemigos", "CuerpoACuerpo"),
            new Preset("Acción cuerpo a cuerpo", "Espadas, escudos y enemigos cerca.", "CuerpoACuerpo", "Enemigos", "ObjetosEInventario"),
            new Preset("Magia / aventura", "Hechizos, pociones, puzles y criaturas.", "Poderes", "ObjetosEInventario", "Mecanismos", "Enemigos"),
            new Preset("Cozy / gestión", "Recoger, cultivar, fabricar, decorar. Sin combate.", "ObjetosEInventario", "Mecanismos"),
            new Preset("Terror / exploración", "Linterna, notas, puertas y algo que te persigue.", "ObjetosEInventario", "Mecanismos", "Enemigos"),
            new Preset("Mundo de bloques (tipo Minecraft)", "Romper, construir, fabricar y sobrevivir a la noche.", "MundoDeBloques"),
            new Preset("Exploración (mundo abierto)", "Descubrir sitios, construir y algo de tensión de noche.", "MundoAbierto", "ObjetosEInventario", "Mecanismos"),
            new Preset("Experiencia narrativa", "Recorrer, mirar, escuchar. Solo el Básico.", new string[0]),
        };

        /// <summary>Módulos que usa la carpeta Ejemplos.</summary>
        static readonly string[] kExampleModules = { "Mecanismos", "ObjetosEInventario", "ArmasADistancia", "CuerpoACuerpo", "Poderes", "Enemigos" };

        Vector2 m_Scroll;

        [MenuItem("Tools/VRKit/Módulos y tipos de juego", false, 0)]
        static void Open() { GetWindow<VRKitModulesWindow>("VRKit · Módulos"); }

        string Root
        {
            get
            {
                // Esta ventana está en <raíz>/Basico/Editor/VRKitModulesWindow.cs
                string path = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                for (int i = 0; i < 3 && !string.IsNullOrEmpty(path); i++) path = System.IO.Path.GetDirectoryName(path);
                return string.IsNullOrEmpty(path) ? "Assets/VRKit" : path.Replace('\\', '/');
            }
        }

        bool Has(string module) { return AssetDatabase.IsValidFolder(Root + "/Modulos/" + module); }
        bool HasExamples { get { return AssetDatabase.IsValidFolder(Root + "/Ejemplos"); } }

        void OnGUI()
        {
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);

            GUILayout.Label("VRKit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Básico siempre va. Cada módulo es opcional y solo depende del Básico: quedaos con los que necesite vuestro juego. " +
                                    "Lo que se quita va a la papelera del sistema (y lo tenéis en GitHub).", MessageType.Info);

            // ---------------------------------------------------------------- 1. Proyecto
            GUILayout.Space(6);
            GUILayout.Label("1 · Comprobar el proyecto", EditorStyles.boldLabel);
            Check("XR Interaction Toolkit 3.x", Type.GetType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable, Unity.XR.Interaction.Toolkit") != null,
                  "Package Manager › Unity Registry › XR Interaction Toolkit");
            Check("Starter Assets de XRI (prefab XR Origin)", AssetDatabase.FindAssets("XR Origin t:Prefab").Length > 0,
                  "Package Manager › XR Interaction Toolkit › Samples › Starter Assets › Import");
            Check("Input System", Type.GetType("UnityEngine.InputSystem.InputAction, Unity.InputSystem") != null, "Package Manager › Input System");
            Check("OpenXR", Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR") != null, "Package Manager › OpenXR Plugin (y Unity OpenXR: Meta)");
            Check("AI Navigation (solo para enemigos a pie)", Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation") != null, "Package Manager › AI Navigation");
            Check("TextMeshPro: TMP Essentials", AssetDatabase.FindAssets("TMP Settings t:ScriptableObject").Length > 0 || AssetDatabase.FindAssets("TMP Settings").Length > 0,
                  "Window › TextMeshPro › Import TMP Essential Resources");
            Check("Plataforma Android", EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android, "File › Build Profiles › Meta Quest (o Android) › Switch Platform");

            // ---------------------------------------------------------------- 2. Módulos
            GUILayout.Space(10);
            GUILayout.Label("2 · Módulos", EditorStyles.boldLabel);
            Row("Básico", "Siempre", "Vida y daño, partida, jugador (VRPlayer), coger y lanzar, botones, menús y opciones, subtítulos, objetivos, tutorial, guardado.", "Todos los juegos", true, null);
            foreach (var m in kModules)
                Row(m.name, Has(m.folder) ? "Instalado" : "No está", m.contents, m.useFor, Has(m.folder), Has(m.folder) ? m.folder : null);
            Row("Ejemplos", HasExamples ? "Instalado" : "No está", "Campo de entrenamiento completo (GameObject › VRKit › Ejemplos). Usa los seis primeros módulos.", "Probar todo y copiar cómo se conecta", HasExamples, HasExamples ? "@Ejemplos" : null);

            // ---------------------------------------------------------------- 3. Tipos de juego
            GUILayout.Space(10);
            GUILayout.Label("3 · Quedarme solo con lo de un tipo de juego", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("No hace falta hacerlo: tener módulos de más no molesta. Sirve para tener el proyecto limpio y no perderse.", MessageType.None);
            foreach (var p in kPresets)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.BeginVertical();
                GUILayout.Label(p.name, EditorStyles.boldLabel);
                GUILayout.Label(p.description + "\nMódulos: " + (p.modules.Length == 0 ? "solo Básico" : string.Join(", ", NamesOf(p.modules))), EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button("Quedarme con esto", GUILayout.Width(130), GUILayout.Height(34))) ApplyPreset(p);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        static IEnumerable<string> NamesOf(string[] folders)
        {
            foreach (var f in folders) foreach (var m in kModules) if (m.folder == f) yield return m.name;
        }

        static void Check(string label, bool ok, string fix)
        {
            EditorGUILayout.BeginHorizontal();
            var old = GUI.color;
            GUI.color = ok ? new Color(0.5f, 0.9f, 0.55f) : new Color(1f, 0.6f, 0.5f);
            GUILayout.Label(ok ? "OK" : "FALTA", EditorStyles.boldLabel, GUILayout.Width(48));
            GUI.color = old;
            GUILayout.Label(label + (ok ? "" : "   →  " + fix), EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndHorizontal();
        }

        void Row(string name, string status, string contents, string useFor, bool installed, string removable)
        {
            EditorGUILayout.BeginHorizontal("box");
            EditorGUILayout.BeginVertical();
            GUILayout.Label(name + "   ·   " + status, EditorStyles.boldLabel);
            GUILayout.Label(contents + "\nPara: " + useFor, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
            GUI.enabled = removable != null;
            if (GUILayout.Button("Quitar", GUILayout.Width(70), GUILayout.Height(30)))
            {
                if (removable == "@Ejemplos") RemoveFolders(new List<string> { Root + "/Ejemplos" });
                else RemoveFolders(new List<string> { Root + "/Modulos/" + removable });
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        void ApplyPreset(Preset p)
        {
            var remove = new List<string>();
            foreach (var m in kModules)
                if (Has(m.folder) && Array.IndexOf(p.modules, m.folder) < 0) remove.Add(Root + "/Modulos/" + m.folder);
            if (remove.Count == 0) { EditorUtility.DisplayDialog("VRKit", "Ya tenéis justo los módulos de «" + p.name + "».", "Vale"); return; }
            RemoveFolders(remove);
        }

        void RemoveFolders(List<string> folders)
        {
            // Si se quita un módulo que usa el campo de entrenamiento, el ejemplo dejaría de compilar: se quita también
            bool removingModule = false;
            foreach (var f in folders) foreach (var m in kExampleModules) if (f.EndsWith("/Modulos/" + m)) removingModule = true;
            if (removingModule && HasExamples && !folders.Contains(Root + "/Ejemplos")) folders.Add(Root + "/Ejemplos");

            string list = string.Join("\n", folders.ConvertAll(f => "  · " + f.Substring(Root.Length + 1)).ToArray());
            if (!EditorUtility.DisplayDialog("Quitar módulos de VRKit",
                    "Se van a mover a la papelera:\n\n" + list + "\n\nSi algún objeto de vuestras escenas usa sus scripts, aparecerá como «Missing Script». Se puede recuperar de la papelera o de GitHub.",
                    "Quitar", "Cancelar")) return;
            foreach (var f in folders) AssetDatabase.MoveAssetToTrash(f);
            AssetDatabase.Refresh();
        }
    }
}
#endif
