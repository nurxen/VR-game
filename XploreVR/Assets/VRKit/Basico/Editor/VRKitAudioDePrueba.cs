#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VRKit.EditorTools
{
    /// <summary>
    /// Tools › VRKit › Audios de prueba: pone los sonidos de prueba (Assets/_Juego/Audio/Prueba) en todos los huecos
    /// de sonido del kit: los objetos de las escenas abiertas, los prefabs de Assets/_Juego y Assets/VRKit/Generated,
    /// y los biomas del Mundo abierto. Busca los scripts por nombre, así que funciona con los módulos que tengáis.
    /// Por defecto solo rellena huecos vacíos: lo que hayáis puesto vosotros no se toca.
    /// </summary>
    public static class VRKitAudioDePrueba
    {
        const string kFolder = "Assets/_Juego/Audio/Prueba";

        // Script → campo → sonido(s)
        static readonly string[,] kMap =
        {
            { "SpawnOnGrab", "spawnSound", "aparece" },
            { "VRButton", "clickSound", "boton_3d" },
            { "Throwable", "grabSound", "coger" },
            { "Throwable", "impactSound", "impacto" },
            { "TutorialPrompt", "doneSound", "tutorial_hecho" },
            { "Projectile", "impactSound", "impacto_proyectil" },
            { "ObjectiveTracker", "progressSound", "objetivo_progreso" },
            { "ObjectiveTracker", "completeSound", "objetivo_completo" },
            { "VRDoor", "openSound", "puerta_abrir" },
            { "VRDoor", "closeSound", "puerta_cerrar" },
            { "VRDoor", "lockedSound", "puerta_cerrada" },
            { "VRDoor", "unlockSound", "puerta_desbloquear" },
            { "CombinationLock", "solvedSound", "puzle_resuelto" },
            { "VRLever", "clickSound", "palanca_click" },
            { "VRDial", "stepSound", "dial_paso" },
            { "VRSlider", "endSound", "deslizador_tope" },
            { "Breakable", "breakSound", "romper_objeto" },
            { "Flashlight", "clickSound", "linterna_click" },
            { "Consumable", "consumeSound", "consumir" },
            { "CraftingStation", "addSound", "caldero_anadir" },
            { "CraftingStation", "successSound", "caldero_exito" },
            { "CraftingStation", "failSound", "caldero_fallo" },
            { "Collectible", "collectSound", "recoger" },
            { "BackpackStorage", "storeSound", "mochila_guardar" },
            { "BackpackStorage", "takeSound", "mochila_sacar" },
            { "BackpackStorage", "fullSound", "mochila_llena" },
            { "Target", "hitSound", "diana" },
            { "ProjectileLauncher", "fireSound", "disparo" },
            { "ProjectileLauncher", "emptySound", "sin_municion" },
            { "Arrow", "hitSound", "flecha_impacto" },
            { "BowString", "nockSound", "arco_cargar" },
            { "BowString", "releaseSound", "arco_soltar" },
            { "MeleeWeapon", "hitSounds", "espada_golpe,espada_golpe_2" },
            { "MeleeWeapon", "swingSounds", "espada_movimiento" },
            { "Shield", "blockSound", "escudo_bloqueo" },
            { "Grenade", "armSound", "granada_armar" },
            { "Grenade", "explosionSound", "explosion" },
            { "HandCaster", "chargeSound", "hechizo_cargar" },
            { "HandCaster", "switchSound", "hechizo_cambiar" },
            { "HandCaster", "spells[].castSound", "hechizo_lanzar" },
            { "Telekinesis", "pullSound", "telequinesis" },
            { "GrappleHook", "fireSound", "gancho_disparo" },
            { "GrappleHook", "attachSound", "gancho_engancha" },
            { "PortalTeleporter", "sound", "portal" },
            { "FlyingEnemy", "fireSound", "dron_disparo" },
            { "FlyingEnemy", "deathSound", "dron_muerte" },
            { "SimpleEnemy", "alertSound", "enemigo_alerta" },
            { "SimpleEnemy", "attackSound", "enemigo_ataque" },
            { "SimpleEnemy", "hurtSound", "enemigo_herido" },
            { "SimpleEnemy", "deathSound", "enemigo_muerte" },
            { "WaveSpawner", "waveStartSound", "oleada" },
            { "VoxelHandTool", "breakSound", "bloque_romper" },
            { "VoxelHandTool", "placeSound", "bloque_poner" },
            { "VoxelHandTool", "hitSound", "golpe_mano" },
            { "VoxelHandTool", "deniedSound", "denegado" },
            { "VoxelHandTool", "pickupSound", "recoger_bloque" },
            { "VoxelCrafting", "craftSound", "fabricar" },
            { "VoxelMob", "idleSound", "mob_grunido" },
            { "VoxelMob", "hurtSound", "mob_herido" },
            { "VoxelMob", "deathSound", "mob_muerte" },
            { "WorldDiscovery", "discoverSound", "descubrir" },
            { "CaveSystem", "enterSound", "cueva_entrar" },
            { "CaveSystem", "caveAmbience", "amb_cueva" },
            { "WorldMap", "openSound", "ui_click" },
            { "WorldMap", "markSound", "boton_3d" },
            { "WorldMap", "discoverSound", "descubrir" },
        };

        // Bioma (número del enum Biome) → música de día, de noche, ambiente de día y de noche
        static readonly string[][] kBiomes =
        {
            new[] { "musica_pradera_dia", "musica_noche_1,musica_noche_2", "amb_pradera_dia", "amb_noche" },              // Pradera
            new[] { "musica_bosque_dia", "musica_noche_1,musica_noche_2", "amb_bosque_dia", "amb_noche" },                // Bosque
            new[] { "musica_pradera_dia,musica_bosque_dia", "musica_noche_2", "amb_ribera", "amb_ribera" },               // Ribera
            new[] { "musica_desierto_dia", "musica_noche_1", "amb_desierto", "amb_desierto" },                           // Desierto
            new[] { "musica_montana_dia", "musica_noche_2", "amb_montana", "amb_montana" },                              // Montaña
            new[] { "musica_montana_dia", "musica_noche_1", "amb_nevado", "amb_nevado" },                                // Nevado
            new[] { "musica_pradera_dia", "musica_noche_1", "amb_playa", "amb_playa" },                                  // Playa
            new[] { "musica_pradera_dia", "musica_noche_2", "amb_ribera", "amb_ribera" },                                // Fondo
        };

        [MenuItem("Tools/VRKit/Audios de prueba/Poner en los huecos vacíos", false, 40)]
        static void FillEmpty() { Run(false); }

        [MenuItem("Tools/VRKit/Audios de prueba/Poner y reemplazar los que haya", false, 41)]
        static void Replace()
        {
            if (EditorUtility.DisplayDialog("Audios de prueba", "Se cambiarán TODOS los sonidos del kit por los de prueba, también los que hayáis puesto vosotros. ¿Seguro?", "Reemplazar", "Cancelar"))
                Run(true);
        }

        static Dictionary<string, AudioClip> s_Clips;
        static int s_Changes;

        static void Run(bool overwrite)
        {
            s_Clips = new Dictionary<string, AudioClip>();
            if (!AssetDatabase.IsValidFolder(kFolder))
            {
                EditorUtility.DisplayDialog("Audios de prueba", "No encuentro " + kFolder + ". Descomprimid el paquete de audios en la carpeta del proyecto.", "Vale");
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { kFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ConfigureImport(path);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) s_Clips[Path.GetFileNameWithoutExtension(path)] = clip;
            }
            s_Changes = 0;

            // 1. Escenas abiertas
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                int before = s_Changes;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) Apply(mb, overwrite);
                if (s_Changes != before) EditorSceneManager.MarkSceneDirty(scene);
            }

            // 2. Prefabs del juego y del kit
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Juego", "Assets/VRKit" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int before = s_Changes;
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true)) Apply(mb, overwrite);
                if (s_Changes != before) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 3. Biomas del Mundo abierto
            foreach (var guid in AssetDatabase.FindAssets("t:BiomeProfile"))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null) continue;
                var so = new SerializedObject(asset);
                var b = so.FindProperty("biome");
                if (b == null || b.intValue < 0 || b.intValue >= kBiomes.Length) continue;
                var m = kBiomes[b.intValue];
                Set(so.FindProperty("dayMusic"), m[0], overwrite);
                Set(so.FindProperty("nightMusic"), m[1], overwrite);
                Set(so.FindProperty("dayAmbience"), m[2], overwrite);
                Set(so.FindProperty("nightAmbience"), m[3], overwrite);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[VRKit] Audios de prueba: " + s_Clips.Count + " sonidos encontrados, " + s_Changes + " huecos rellenados. Guardad la escena (Ctrl+S).");
            EditorUtility.DisplayDialog("Audios de prueba", s_Changes + " sonidos puestos.\n\nGuardad la escena (Ctrl+S). Lo que creéis desde ahora con los menús de VRKit no los trae: volved a usar esta opción.", "Vale");
        }

        static void Apply(MonoBehaviour mb, bool overwrite)
        {
            if (mb == null) return;
            string type = mb.GetType().Name;
            SerializedObject so = null;
            for (int i = 0; i < kMap.GetLength(0); i++)
            {
                if (kMap[i, 0] != type) continue;
                if (so == null) so = new SerializedObject(mb);
                string field = kMap[i, 1];
                if (field.Contains("[]."))
                {
                    // Un campo dentro de cada elemento de una lista (los hechizos de HandCaster)
                    var parts = field.Split(new[] { "[]." }, System.StringSplitOptions.None);
                    var list = so.FindProperty(parts[0]);
                    if (list == null || !list.isArray) continue;
                    for (int k = 0; k < list.arraySize; k++) Set(list.GetArrayElementAtIndex(k).FindPropertyRelative(parts[1]), kMap[i, 2], overwrite);
                }
                else Set(so.FindProperty(field), kMap[i, 2], overwrite);
            }
            // Criaturas del Mundo abierto: sonido según sean pacíficas u hostiles
            if (type == "WorldCreature")
            {
                so = so ?? new SerializedObject(mb);
                var mood = so.FindProperty("mood");
                Set(so.FindProperty("idleSound"), mood != null && mood.intValue == 1 ? "sombra_nocturna" : "ciervo", overwrite);
            }
            if (so != null) so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(SerializedProperty p, string names, bool overwrite)
        {
            if (p == null) return;
            var list = new List<AudioClip>();
            foreach (var n in names.Split(',')) { AudioClip c; if (s_Clips.TryGetValue(n.Trim(), out c)) list.Add(c); }
            if (list.Count == 0) return;
            if (p.isArray && p.propertyType != SerializedPropertyType.String)
            {
                if (p.arraySize > 0 && !overwrite) return;
                p.arraySize = list.Count;
                for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
                s_Changes++;
            }
            else if (p.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (p.objectReferenceValue != null && !overwrite) return;
                p.objectReferenceValue = list[0];
                s_Changes++;
            }
        }

        /// <summary>Ajustes de importación pensados para Quest: música en streaming, el resto comprimido.</summary>
        static void ConfigureImport(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as AudioImporter;
            if (imp == null || imp.userData == "vrkit") return;
            var s = imp.defaultSampleSettings;
            if (path.Contains("/Musica/")) { s.loadType = AudioClipLoadType.Streaming; s.quality = 0.6f; }
            else if (path.Contains("/Ambiente/")) { s.loadType = AudioClipLoadType.CompressedInMemory; s.quality = 0.6f; }
            else { s.loadType = AudioClipLoadType.DecompressOnLoad; s.quality = 0.7f; }
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.loadInBackground = path.Contains("/Musica/");
            imp.userData = "vrkit";
            imp.SaveAndReimport();
        }
    }
}
#endif
