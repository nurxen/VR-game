using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRKit.EditorTools
{
    /// <summary>
    /// Tools › VRKit › Optimizar para Quest: pone de una vez los ajustes del proyecto que más FPS dan en las gafas.
    /// Todo por nombre (SerializedObject y reflexión), así no depende de la versión de URP ni de OpenXR:
    /// lo que no encuentra, lo salta y lo dice.
    /// </summary>
    public static class VRKitOptimizarQuest
    {
        [MenuItem("Tools/VRKit/Optimizar para Quest", false, 30)]
        static void Run()
        {
            if (!EditorUtility.DisplayDialog("Optimizar para Quest",
                "Se cambiarán estos ajustes del proyecto:\n\n" +
                "· URP: sin HDR, sin sombras, sin texturas de profundidad/opacas, MSAA 4x\n" +
                "· Cámaras de las escenas abiertas: sin post-procesado ni sombras\n" +
                "· OpenXR (Android): Single Pass Instanced y Foveated Rendering\n" +
                "· Recolector de basura incremental y texturas ASTC\n\n" +
                "Se puede deshacer a mano. ¿Seguimos?", "Optimizar", "Cancelar")) return;

            var log = new StringBuilder();
            int n = 0;

            // 1. Assets de URP (todos: el de PC también, que es el que se usa al probar con Link desde el editor)
            foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.GetType().Name != "UniversalRenderPipelineAsset") continue;
                var so = new SerializedObject(asset);
                int c = 0;
                c += SetBool(so, "m_SupportsHDR", false);
                c += SetInt(so, "m_MSAA", 4);
                c += SetBool(so, "m_RequireDepthTexture", false);
                c += SetBool(so, "m_RequireOpaqueTexture", false);
                c += SetBool(so, "m_MainLightShadowsSupported", false);
                c += SetBool(so, "m_AdditionalLightShadowsSupported", false);
                c += SetBool(so, "m_SoftShadowsSupported", false);
                c += SetBool(so, "m_SupportsTerrainHoles", false);
                so.ApplyModifiedPropertiesWithoutUndo();
                if (c > 0) { EditorUtility.SetDirty(asset); log.AppendLine("· " + asset.name + ": " + c + " ajustes"); n += c; }
            }

            // 2. Cámaras de las escenas abiertas
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                bool dirty = false;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null || mb.GetType().Name != "UniversalAdditionalCameraData") continue;
                        var so = new SerializedObject(mb);
                        int c = SetBool(so, "m_RenderPostProcessing", false) + SetBool(so, "m_RenderShadows", false) + SetInt(so, "m_Antialiasing", 0);
                        so.ApplyModifiedPropertiesWithoutUndo();
                        if (c > 0) { dirty = true; n += c; log.AppendLine("· Cámara «" + mb.name + "»: sin post-procesado ni sombras"); }
                    }
                if (dirty) EditorSceneManager.MarkSceneDirty(scene);
            }

            // 3. OpenXR para Android
            try { n += OpenXR(log); }
            catch (Exception ex) { log.AppendLine("· OpenXR: no se pudo (" + ex.Message + ")"); }

            // 4. Varios
            if (!PlayerSettings.gcIncremental) { PlayerSettings.gcIncremental = true; n++; log.AppendLine("· Recolector de basura incremental"); }
            try
            {
                var p = typeof(EditorUserBuildSettings).GetProperty("androidBuildSubtarget", BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.PropertyType.IsEnum && p.GetValue(null).ToString() != "ASTC")
                {
                    p.SetValue(null, Enum.Parse(p.PropertyType, "ASTC"));
                    n++; log.AppendLine("· Texturas en ASTC");
                }
            }
            catch { }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Optimizar para Quest",
                (n == 0 ? "Ya estaba todo optimizado." : "Hecho (" + n + " cambios):\n\n" + log) +
                "\n\nGuardad la escena (Ctrl+S). Para medir FPS de verdad, probad en una build (Build And Run), no desde el editor.", "Vale");
        }

        static int OpenXR(StringBuilder log)
        {
            Type settingsType = null;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                settingsType = a.GetType("UnityEngine.XR.OpenXR.OpenXRSettings");
                if (settingsType != null) break;
            }
            if (settingsType == null) { log.AppendLine("· OpenXR no está instalado: se salta"); return 0; }
            var get = settingsType.GetMethod("GetSettingsForBuildTargetGroup", BindingFlags.Public | BindingFlags.Static);
            var settings = get != null ? get.Invoke(null, new object[] { BuildTargetGroup.Android }) as UnityEngine.Object : null;
            if (settings == null) { log.AppendLine("· OpenXR para Android no está configurado: se salta"); return 0; }

            int n = 0;
            var mode = settingsType.GetProperty("renderMode");
            if (mode != null && mode.PropertyType.IsEnum && mode.GetValue(settings).ToString() != "SinglePassInstanced")
            {
                mode.SetValue(settings, Enum.Parse(mode.PropertyType, "SinglePassInstanced"));
                n++; log.AppendLine("· OpenXR: Single Pass Instanced (dibuja los dos ojos de una vez)");
            }
            var features = settingsType.GetMethod("GetFeatures", Type.EmptyTypes);
            if (features != null)
            {
                var list = features.Invoke(settings, null) as Array;
                if (list != null)
                    foreach (var f in list)
                    {
                        if (f == null || f.GetType().Name.IndexOf("Foveat", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        var en = f.GetType().GetProperty("enabled");
                        if (en != null && !(bool)en.GetValue(f)) { en.SetValue(f, true); n++; log.AppendLine("· OpenXR: Foveated Rendering activado"); }
                        var fo = f as UnityEngine.Object;
                        if (fo != null) EditorUtility.SetDirty(fo);
                    }
            }
            EditorUtility.SetDirty(settings);
            return n;
        }

        static int SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p == null || p.propertyType != SerializedPropertyType.Boolean || p.boolValue == value) return 0;
            p.boolValue = value; return 1;
        }

        static int SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p == null) return 0;
            if (p.propertyType == SerializedPropertyType.Enum) { if (p.intValue == value) return 0; p.intValue = value; return 1; }
            if (p.propertyType != SerializedPropertyType.Integer || p.intValue == value) return 0;
            p.intValue = value; return 1;
        }
    }
}
