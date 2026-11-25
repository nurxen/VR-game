#if UNITY_EDITOR
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit.EditorTools
{
    /// <summary>
    /// Menú GameObject › VRKit › Mundo de bloques: un mundo tipo Minecraft (supervivencia o creativo), una maqueta
    /// sobre la mesa, y los assets de ejemplo (bloques, atlas, recetas, herramientas y criaturas) en Assets/_Juego/MundoDeBloques.
    /// </summary>
    public static partial class VRKitBuilder
    {
        const string kVox = kRoot + "Mundo de bloques/";
        const string kVoxAssets = "Assets/_Juego/MundoDeBloques";

        [MenuItem(kVox + "Mundo completo · supervivencia", false, 100)] static void MenuVoxelSurvival() { BuildVoxelWorld(VoxelGameMode.Supervivencia); }
        [MenuItem(kVox + "Mundo completo · creativo", false, 101)] static void MenuVoxelCreative() { BuildVoxelWorld(VoxelGameMode.Creativo); }
        [MenuItem(kVox + "Maqueta sobre una mesa", false, 102)] static void MenuVoxelTabletop() { BuildVoxelTabletop(); }
        [MenuItem(kVox + "Crear o completar bloques, atlas y recetas", false, 120)]
        static void MenuVoxelAssets()
        {
            var a = EnsureVoxelAssets();
            Selection.activeObject = a.registry;
            EditorGUIUtility.PingObject(a.registry);
        }
        [MenuItem(kVox + "Volver a pintar el atlas de ejemplo", false, 121)]
        static void MenuVoxelRepaint()
        {
            if (!EditorUtility.DisplayDialog("VRKit", "Se sobrescribe " + kVoxAssets + "/Atlas.png con el atlas de ejemplo. Si lo habéis pintado vosotros, se pierde (salvo en GitHub).", "Pintar", "Cancelar")) return;
            WriteAtlas(kVoxAssets + "/Atlas.png", true);
        }

        // ================================================================== Assets

        class VoxelAssets
        {
            public BlockRegistry registry;
            public Material material, outline;
            public readonly Dictionary<string, BlockType> blocks = new Dictionary<string, BlockType>();
            public readonly List<VoxelRecipe> recipes = new List<VoxelRecipe>();
            public VoxelMob zombie, pig, sheep;
        }

        struct BlockDef
        {
            public string name; public int top, side, bottom; public float hardness; public VoxelToolType tool; public bool needsTool;
            public Color color; public string drop; public int dropCount; public bool transparent, cross, opens, creative;
        }

        static BlockDef B(string name, int top, int side, int bottom, float hardness, VoxelToolType tool, Color color,
                          bool needsTool = false, string drop = null, int dropCount = 1, bool transparent = false, bool cross = false, bool opens = false, bool creative = true)
        {
            return new BlockDef { name = name, top = top, side = side, bottom = bottom, hardness = hardness, tool = tool, needsTool = needsTool, color = color,
                                  drop = drop, dropCount = dropCount, transparent = transparent, cross = cross, opens = opens, creative = creative };
        }

        static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        static Texture2D WriteAtlas(string path, bool overwrite)
        {
            if (overwrite || AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null)
            {
                int w, h;
                var px = VoxelAtlasPainter.Paint(16, out w, out h);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Application.dataPath), path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.filterMode = FilterMode.Point;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static VoxelAssets EnsureVoxelAssets()
        {
            var a = new VoxelAssets();
            EnsureAssetFolder(kVoxAssets + "/Bloques");
            EnsureAssetFolder(kVoxAssets + "/Recetas");
            EnsureAssetFolder(kVoxAssets + "/Objetos");
            EnsureAssetFolder(kVoxAssets + "/Criaturas");

            // Atlas y material
            var atlas = WriteAtlas(kVoxAssets + "/Atlas.png", false);
            string matPath = kVoxAssets + "/Bloques.mat";
            a.material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (a.material == null)
            {
                var sh = Shader.Find("VRKit/Voxel");
                if (sh == null) { Debug.LogWarning("[VRKit] No encuentro el shader VRKit/Voxel (¿no está URP?). Uso URP/Unlit: sin sombras por cara ni noche."); sh = Shader.Find("Universal Render Pipeline/Unlit"); }
                a.material = new Material(sh);
                AssetDatabase.CreateAsset(a.material, matPath);
            }
            a.material.mainTexture = atlas;
            if (a.material.HasProperty("_BaseMap")) a.material.SetTexture("_BaseMap", atlas);
            if (a.material.HasProperty("_AlphaClip")) { a.material.SetFloat("_AlphaClip", 1f); a.material.EnableKeyword("_ALPHATEST_ON"); }
            EditorUtility.SetDirty(a.material);
            a.outline = UnlitMat("Contorno de bloque", Color.white);

            // Bloques (solo se crean los que faltan: vuestros cambios se respetan)
            const VoxelToolType N = VoxelToolType.Ninguna, Pico = VoxelToolType.Pico, Pala = VoxelToolType.Pala, Hacha = VoxelToolType.Hacha;
            var P = new { gt = VoxelAtlasPainter.GrassTop, gs = VoxelAtlasPainter.GrassSide };
            var defs = new[]
            {
                B("Hierba", P.gt, P.gs, VoxelAtlasPainter.Dirt, 0.9f, Pala, new Color(0.38f, 0.62f, 0.22f), drop: "Tierra"),
                B("Tierra", VoxelAtlasPainter.Dirt, VoxelAtlasPainter.Dirt, VoxelAtlasPainter.Dirt, 0.75f, Pala, new Color(0.53f, 0.38f, 0.27f)),
                B("Piedra", VoxelAtlasPainter.Stone, VoxelAtlasPainter.Stone, VoxelAtlasPainter.Stone, 2.5f, Pico, new Color(0.5f, 0.5f, 0.5f), needsTool: true, drop: "Roca"),
                B("Roca", VoxelAtlasPainter.Cobble, VoxelAtlasPainter.Cobble, VoxelAtlasPainter.Cobble, 2.8f, Pico, new Color(0.45f, 0.45f, 0.45f), needsTool: true),
                B("Tronco", VoxelAtlasPainter.LogTop, VoxelAtlasPainter.LogSide, VoxelAtlasPainter.LogTop, 2f, Hacha, new Color(0.4f, 0.32f, 0.2f)),
                B("Tablones", VoxelAtlasPainter.Planks, VoxelAtlasPainter.Planks, VoxelAtlasPainter.Planks, 1.5f, Hacha, new Color(0.65f, 0.52f, 0.31f)),
                B("Hojas", VoxelAtlasPainter.Leaves, VoxelAtlasPainter.Leaves, VoxelAtlasPainter.Leaves, 0.3f, N, new Color(0.23f, 0.48f, 0.15f), dropCount: 0, transparent: true),
                B("Arena", VoxelAtlasPainter.Sand, VoxelAtlasPainter.Sand, VoxelAtlasPainter.Sand, 0.75f, Pala, new Color(0.87f, 0.82f, 0.63f)),
                B("Cristal", VoxelAtlasPainter.Glass, VoxelAtlasPainter.Glass, VoxelAtlasPainter.Glass, 0.5f, N, new Color(0.8f, 0.9f, 0.95f), dropCount: 0, transparent: true),
                B("Ladrillos", VoxelAtlasPainter.Bricks, VoxelAtlasPainter.Bricks, VoxelAtlasPainter.Bricks, 3f, Pico, new Color(0.6f, 0.28f, 0.22f), needsTool: true),
                B("Mineral de carbón", VoxelAtlasPainter.CoalOre, VoxelAtlasPainter.CoalOre, VoxelAtlasPainter.CoalOre, 3f, Pico, new Color(0.2f, 0.2f, 0.2f), needsTool: true),
                B("Mineral de hierro", VoxelAtlasPainter.IronOre, VoxelAtlasPainter.IronOre, VoxelAtlasPainter.IronOre, 3.5f, Pico, new Color(0.85f, 0.7f, 0.58f), needsTool: true),
                B("Lecho de roca", VoxelAtlasPainter.Bedrock, VoxelAtlasPainter.Bedrock, VoxelAtlasPainter.Bedrock, -1f, N, new Color(0.25f, 0.25f, 0.25f)),
                B("Mesa de trabajo", VoxelAtlasPainter.TableTop, VoxelAtlasPainter.TableSide, VoxelAtlasPainter.Planks, 1.5f, Hacha, new Color(0.55f, 0.4f, 0.24f), opens: true),
                B("Flor", 0, VoxelAtlasPainter.Flower, 0, 0f, N, new Color(0.85f, 0.2f, 0.25f), cross: true),
                B("Hierba alta", 0, VoxelAtlasPainter.TallGrass, 0, 0f, N, new Color(0.3f, 0.6f, 0.2f), dropCount: 0, cross: true),
                B("Lana", VoxelAtlasPainter.Wool, VoxelAtlasPainter.Wool, VoxelAtlasPainter.Wool, 0.8f, N, new Color(0.92f, 0.92f, 0.92f)),
                B("Nieve", VoxelAtlasPainter.Snow, VoxelAtlasPainter.SnowSide, VoxelAtlasPainter.Dirt, 0.6f, Pala, new Color(0.95f, 0.97f, 1f)),
                B("Grava", VoxelAtlasPainter.Gravel, VoxelAtlasPainter.Gravel, VoxelAtlasPainter.Gravel, 0.8f, Pala, new Color(0.5f, 0.47f, 0.44f)),
                B("Ladrillos de piedra", VoxelAtlasPainter.StoneBricks, VoxelAtlasPainter.StoneBricks, VoxelAtlasPainter.StoneBricks, 2.5f, Pico, new Color(0.5f, 0.5f, 0.5f), needsTool: true),
            };
            for (int i = 0; i < defs.Length; i++)
            {
                var d = defs[i];
                string path = kVoxAssets + "/Bloques/" + (i + 1).ToString("00") + " " + d.name + ".asset";
                var b = AssetDatabase.LoadAssetAtPath<BlockType>(path);
                if (b == null)
                {
                    b = ScriptableObject.CreateInstance<BlockType>();
                    b.displayName = d.name;
                    b.tileTop = d.top; b.tileSide = d.side; b.tileBottom = d.bottom;
                    b.hardness = d.hardness; b.bestTool = d.tool; b.needsTool = d.needsTool;
                    b.particleColor = d.color; b.dropCount = d.dropCount;
                    b.transparent = d.transparent || d.cross;
                    b.shape = d.cross ? BlockShape.Cruz : BlockShape.Cubo;
                    b.solid = !d.cross; b.replaceable = d.cross;
                    b.opensCrafting = d.opens; b.inCreativeMenu = d.creative;
                    AssetDatabase.CreateAsset(b, path);
                }
                a.blocks[d.name] = b;
            }
            foreach (var d in defs)
                if (d.drop != null && a.blocks[d.name].drop == null) { a.blocks[d.name].drop = a.blocks[d.drop]; EditorUtility.SetDirty(a.blocks[d.name]); }

            // Registro: se añaden al final los que falten, sin reordenar
            string regPath = kVoxAssets + "/Registro de bloques.asset";
            a.registry = AssetDatabase.LoadAssetAtPath<BlockRegistry>(regPath);
            if (a.registry == null) { a.registry = ScriptableObject.CreateInstance<BlockRegistry>(); AssetDatabase.CreateAsset(a.registry, regPath); }
            foreach (var d in defs) if (!a.registry.blocks.Contains(a.blocks[d.name])) a.registry.blocks.Add(a.blocks[d.name]);
            a.registry.atlas = atlas;
            a.registry.material = a.material;
            a.registry.atlasColumns = VoxelAtlasPainter.Columns;
            a.registry.atlasRows = VoxelAtlasPainter.Rows;
            EditorUtility.SetDirty(a.registry);

            // Herramientas (objetos que se cogen)
            var woodPick = ToolPrefab("Pico de madera", VoxelToolType.Pico, 4f, 3f, 60, "madera", new Color(0.65f, 0.52f, 0.31f));
            var woodAxe = ToolPrefab("Hacha de madera", VoxelToolType.Hacha, 4f, 4f, 60, "madera", new Color(0.65f, 0.52f, 0.31f));
            var woodShovel = ToolPrefab("Pala de madera", VoxelToolType.Pala, 4f, 2f, 60, "madera", new Color(0.65f, 0.52f, 0.31f));
            var stonePick = ToolPrefab("Pico de piedra", VoxelToolType.Pico, 6f, 4f, 130, "piedra", new Color(0.5f, 0.5f, 0.5f));
            var ironPick = ToolPrefab("Pico de hierro", VoxelToolType.Pico, 9f, 5f, 250, "hierro", new Color(0.85f, 0.85f, 0.88f));
            var sword = ToolPrefab("Espada de piedra", VoxelToolType.Espada, 1f, 8f, 130, "piedra", new Color(0.5f, 0.5f, 0.5f));

            // Recetas
            Recipe(a, "Tablones", new[] { S(a, "Tronco", 1) }, S(a, "Tablones", 4), null, false);
            Recipe(a, "Mesa de trabajo", new[] { S(a, "Tablones", 4) }, S(a, "Mesa de trabajo", 1), null, false);
            Recipe(a, "Pico de madera", new[] { S(a, "Tablones", 3) }, default(BlockStack), woodPick, true);
            Recipe(a, "Hacha de madera", new[] { S(a, "Tablones", 3) }, default(BlockStack), woodAxe, true);
            Recipe(a, "Pala de madera", new[] { S(a, "Tablones", 1) }, default(BlockStack), woodShovel, true);
            Recipe(a, "Pico de piedra", new[] { S(a, "Roca", 3), S(a, "Tablones", 2) }, default(BlockStack), stonePick, true);
            Recipe(a, "Espada de piedra", new[] { S(a, "Roca", 2), S(a, "Tablones", 1) }, default(BlockStack), sword, true);
            Recipe(a, "Pico de hierro", new[] { S(a, "Mineral de hierro", 3), S(a, "Tablones", 2) }, default(BlockStack), ironPick, true);
            Recipe(a, "Ladrillos de piedra", new[] { S(a, "Roca", 4) }, S(a, "Ladrillos de piedra", 4), null, true);
            Recipe(a, "Cristal", new[] { S(a, "Arena", 1), S(a, "Mineral de carbón", 1) }, S(a, "Cristal", 2), null, true);
            Recipe(a, "Ladrillos", new[] { S(a, "Tierra", 2), S(a, "Mineral de carbón", 1) }, S(a, "Ladrillos", 4), null, true);

            // Criaturas
            a.zombie = MobPrefab("Zombi", VoxelMobKind.Hostil, a, null);
            a.pig = MobPrefab("Cerdo", VoxelMobKind.Pacifico, a, null);
            a.sheep = MobPrefab("Oveja", VoxelMobKind.Pacifico, a, "Lana");

            AssetDatabase.SaveAssets();
            return a;
        }

        static BlockStack S(VoxelAssets a, string block, int count) { return new BlockStack(a.blocks[block], count); }

        static void Recipe(VoxelAssets a, string name, BlockStack[] inputs, BlockStack output, GameObject prefab, bool table)
        {
            string path = kVoxAssets + "/Recetas/" + name + ".asset";
            var r = AssetDatabase.LoadAssetAtPath<VoxelRecipe>(path);
            if (r == null)
            {
                r = ScriptableObject.CreateInstance<VoxelRecipe>();
                r.inputs = inputs; r.output = output; r.outputPrefab = prefab; r.outputName = prefab != null ? name : ""; r.needsTable = table;
                AssetDatabase.CreateAsset(r, path);
            }
            a.recipes.Add(r);
        }

        static GameObject ToolPrefab(string name, VoxelToolType type, float speed, float damage, int durability, string material, Color headColor)
        {
            string path = kVoxAssets + "/Objetos/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject(name);
            var wood = Mat("Mango de madera", new Color(0.55f, 0.4f, 0.24f));
            var head = Mat("Herramienta de " + material, headColor);
            Prim(PrimitiveType.Cube, "Mango", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.035f, 0.46f, 0.035f), wood, true);
            switch (type)
            {
                case VoxelToolType.Pico:
                    Prim(PrimitiveType.Cube, "Cabeza", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.05f, 0.05f), head, true);
                    Prim(PrimitiveType.Cube, "Punta izq.", root.transform, new Vector3(-0.17f, 0.4f, 0f), new Vector3(0.06f, 0.07f, 0.045f), head, false);
                    Prim(PrimitiveType.Cube, "Punta der.", root.transform, new Vector3(0.17f, 0.4f, 0f), new Vector3(0.06f, 0.07f, 0.045f), head, false);
                    break;
                case VoxelToolType.Hacha:
                    Prim(PrimitiveType.Cube, "Hoja", root.transform, new Vector3(0.07f, 0.38f, 0f), new Vector3(0.13f, 0.14f, 0.03f), head, true);
                    break;
                case VoxelToolType.Pala:
                    Prim(PrimitiveType.Cube, "Pala", root.transform, new Vector3(0f, 0.48f, 0f), new Vector3(0.11f, 0.14f, 0.02f), head, true);
                    break;
                default:
                    Prim(PrimitiveType.Cube, "Guarda", root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.14f, 0.025f, 0.04f), wood, false);
                    Prim(PrimitiveType.Cube, "Hoja", root.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.06f, 0.6f, 0.015f), head, true);
                    break;
            }
            var attach = Empty("Agarre", root.transform, new Vector3(0f, 0.05f, 0f));
            attach.localRotation = Quaternion.Euler(90f, 0f, 0f); // la herramienta apunta hacia delante en la mano
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 0.8f;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            root.AddComponent<GrabHighlight>();
            var t = root.AddComponent<VoxelToolItem>();
            t.toolType = type; t.speed = speed; t.damage = damage; t.durability = durability;
            return SavePrefab(root, path);
        }

        static VoxelMob MobPrefab(string name, VoxelMobKind kind, VoxelAssets a, string dropBlock)
        {
            string path = kVoxAssets + "/Criaturas/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<VoxelMob>();

            var root = new GameObject(name);
            var legs = new List<Transform>();
            Transform head;
            var black = Mat("Ojos", new Color(0.05f, 0.05f, 0.05f));
            float w, h, hp;
            if (kind == VoxelMobKind.Hostil)
            {
                var skin = Mat("Zombi piel", new Color(0.35f, 0.55f, 0.3f));
                var shirt = Mat("Zombi camisa", new Color(0.2f, 0.55f, 0.6f));
                var pants = Mat("Zombi pantalón", new Color(0.25f, 0.25f, 0.55f));
                for (int i = 0; i < 2; i++)
                {
                    var hip = Empty("Pierna", root.transform, new Vector3(i == 0 ? -0.12f : 0.12f, 0.75f, 0f));
                    Prim(PrimitiveType.Cube, "Pierna", hip, new Vector3(0f, -0.375f, 0f), new Vector3(0.24f, 0.75f, 0.24f), pants, false);
                    legs.Add(hip);
                }
                Prim(PrimitiveType.Cube, "Cuerpo", root.transform, new Vector3(0f, 1.12f, 0f), new Vector3(0.48f, 0.74f, 0.26f), shirt, false);
                for (int i = 0; i < 2; i++)
                    Prim(PrimitiveType.Cube, "Brazo", root.transform, new Vector3(i == 0 ? -0.36f : 0.36f, 1.38f, 0.3f), new Vector3(0.22f, 0.22f, 0.7f), skin, false);
                head = Empty("Cabeza", root.transform, new Vector3(0f, 1.5f, 0f));
                Prim(PrimitiveType.Cube, "Cabeza", head, new Vector3(0f, 0.24f, 0f), Vector3.one * 0.48f, skin, false);
                Prim(PrimitiveType.Cube, "Ojo", head, new Vector3(-0.1f, 0.27f, 0.245f), new Vector3(0.09f, 0.06f, 0.02f), black, false);
                Prim(PrimitiveType.Cube, "Ojo", head, new Vector3(0.1f, 0.27f, 0.245f), new Vector3(0.09f, 0.06f, 0.02f), black, false);
                w = 0.6f; h = 1.9f; hp = 20f;
            }
            else
            {
                bool sheep = name == "Oveja";
                var body = Mat(sheep ? "Oveja lana" : "Cerdo piel", sheep ? new Color(0.93f, 0.93f, 0.93f) : new Color(0.95f, 0.6f, 0.62f));
                var face = Mat(sheep ? "Oveja cara" : "Cerdo hocico", sheep ? new Color(0.55f, 0.45f, 0.4f) : new Color(0.9f, 0.5f, 0.55f));
                for (int i = 0; i < 4; i++)
                {
                    var hip = Empty("Pata", root.transform, new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.36f, i < 2 ? 0.3f : -0.3f));
                    Prim(PrimitiveType.Cube, "Pata", hip, new Vector3(0f, -0.18f, 0f), new Vector3(0.18f, 0.36f, 0.18f), face, false);
                    legs.Add(hip);
                }
                Prim(PrimitiveType.Cube, "Cuerpo", root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 0.5f, 0.95f), body, false);
                head = Empty("Cabeza", root.transform, new Vector3(0f, 0.7f, 0.5f));
                Prim(PrimitiveType.Cube, "Cabeza", head, new Vector3(0f, 0.05f, 0.15f), Vector3.one * 0.42f, sheep ? face : body, false);
                Prim(PrimitiveType.Cube, "Ojo", head, new Vector3(-0.12f, 0.12f, 0.365f), new Vector3(0.06f, 0.06f, 0.02f), black, false);
                Prim(PrimitiveType.Cube, "Ojo", head, new Vector3(0.12f, 0.12f, 0.365f), new Vector3(0.06f, 0.06f, 0.02f), black, false);
                if (!sheep) Prim(PrimitiveType.Cube, "Hocico", head, new Vector3(0f, -0.02f, 0.37f), new Vector3(0.18f, 0.12f, 0.04f), face, false);
                w = 0.8f; h = 0.95f; hp = 10f;
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, kind == VoxelMobKind.Hostil ? w : 1.1f);
            var health = root.AddComponent<Health>();
            SetFloat(health, "maxHealth", hp);
            SetFloat(health, "invulnerableTime", 0.3f);
            var mob = root.AddComponent<VoxelMob>();
            SetEnum(mob, "kind", (int)kind);
            SetFloat(mob, "width", w);
            SetFloat(mob, "height", h);
            SetObjArray(mob, "legs", legs.ConvertAll(t => (Object)t).ToArray());
            SetRef(mob, "head", head);
            if (kind == VoxelMobKind.Pacifico) { SetFloat(mob, "walkSpeed", 0.9f); SetFloat(mob, "chaseSpeed", 2.4f); SetInt(mob, "score", 0); }
            if (dropBlock != null)
            {
                var so = new SerializedObject(mob);
                var p = so.FindProperty("drops");
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).FindPropertyRelative("block").objectReferenceValue = a.blocks[dropBlock];
                p.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return SavePrefab(root, path).GetComponent<VoxelMob>();
        }

        // ================================================================== Escenas

        static void BuildVoxelWorld(VoxelGameMode mode)
        {
            var a = EnsureVoxelAssets();
            var origin = EnsureXROrigin();
            CreateGameSystems();
            if (Object.FindFirstObjectByType<VRMenuManager>() == null) BuildMenu(false);
            EnsureSubtitles();
            if (origin != null && origin.GetComponent<VoxelPlayer>() == null) Undo.AddComponent<VoxelPlayer>(origin.gameObject);

            var root = new GameObject("Mundo de bloques");
            var worldGo = new GameObject("Mundo");
            worldGo.transform.SetParent(root.transform, false);
            var world = worldGo.AddComponent<VoxelWorld>();
            WireWorld(world, a);
            SetBool(world, "initializeOnStart", false);

            var inv = root.AddComponent<BlockInventory>();
            if (mode == VoxelGameMode.Supervivencia)
            {
                // Para no empezar con las manos vacías: unos tablones y una mesa
                var so = new SerializedObject(inv);
                var p = so.FindProperty("startingItems");
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).FindPropertyRelative("block").objectReferenceValue = a.blocks["Tablones"];
                p.GetArrayElementAtIndex(0).FindPropertyRelative("count").intValue = 8;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            var game = root.AddComponent<VoxelGame>();
            SetRef(game, "world", world);
            SetRef(game, "inventory", inv);
            SetEnum(game, "mode", (int)mode);
            SetString(game, "worldName", mode == VoxelGameMode.Creativo ? "Creativo" : "Supervivencia");

            var crafting = root.AddComponent<VoxelCrafting>();
            SetObjArray(crafting, "recipes", a.recipes.ConvertAll(r => (Object)r).ToArray());

            AddHands(root, a, VoxelAimMode.Rayo, 5f);

            // Sol y ciclo de día
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.transform.SetParent(root.transform, false);
            }
            sun.shadows = LightShadows.None; // los bloques no usan sombras en tiempo real: en Quest se notaría mucho
            var day = root.AddComponent<VoxelDayNight>();
            SetRef(day, "sun", sun);

            var spawner = root.AddComponent<VoxelMobSpawner>();
            SetObjArray(spawner, "passivePrefabs", new Object[] { a.pig, a.sheep });
            SetObjArray(spawner, "hostilePrefabs", new Object[] { a.zombie });

            Undo.RegisterCreatedObjectUndo(root, "VRKit");
            Selection.activeGameObject = root;
            Debug.Log("[VRKit] Mundo de bloques (" + mode + ") montado. Play: aparecerás en el centro del mundo. Gatillo derecho rompe, gatillo izquierdo pone, X/Y cambian de bloque, B abre la fabricación, A salta. " +
                      "Los bloques, recetas, herramientas y criaturas están en " + kVoxAssets + ".");
        }

        static void WireWorld(VoxelWorld world, VoxelAssets a)
        {
            SetRef(world, "registry", a.registry);
            string[,] map =
            {
                { "grass", "Hierba" }, { "dirt", "Tierra" }, { "stone", "Piedra" }, { "sand", "Arena" }, { "snow", "Nieve" }, { "log", "Tronco" },
                { "leaves", "Hojas" }, { "coalOre", "Mineral de carbón" }, { "ironOre", "Mineral de hierro" }, { "bedrock", "Lecho de roca" },
                { "flower", "Flor" }, { "tallGrass", "Hierba alta" },
            };
            for (int i = 0; i < map.GetLength(0); i++) SetRef(world, "terrainBlocks." + map[i, 0], a.blocks[map[i, 1]]);
        }

        static void AddHands(GameObject root, VoxelAssets a, VoxelAimMode aim, float reach)
        {
            var hands = new GameObject("Manos");
            hands.transform.SetParent(root.transform, false);
            var right = hands.AddComponent<VoxelHandTool>();
            SetEnum(right, "hand", (int)Hand.Right);
            SetEnum(right, "role", (int)VoxelHandRole.Romper);
            SetEnum(right, "aim", (int)aim);
            SetFloat(right, "reach", reach);
            SetRef(right, "outlineMaterial", a.outline);
            var left = hands.AddComponent<VoxelHandTool>();
            SetEnum(left, "hand", (int)Hand.Left);
            SetEnum(left, "role", (int)VoxelHandRole.Poner);
            SetEnum(left, "aim", (int)aim);
            SetFloat(left, "reach", reach);
            SetRef(left, "outlineMaterial", a.outline);
            var bar = hands.AddComponent<BlockHotbar>();
            SetEnum(bar, "hand", (int)Hand.Left);
        }

        static void BuildVoxelTabletop()
        {
            var a = EnsureVoxelAssets();
            if (Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>() == null) PrepareVRScene();

            var root = new GameObject("Maqueta de bloques");
            const float scale = 0.03f;          // 3 cm por bloque: 32 bloques ≈ 1 metro
            var table = Prim(PrimitiveType.Cube, "Mesa", root.transform, new Vector3(0f, 0.375f, 0f), new Vector3(1.15f, 0.75f, 1.15f), Mat("Mesa", new Color(0.42f, 0.3f, 0.2f)), true);
            table.isStatic = true;

            var worldGo = new GameObject("Mundo");
            worldGo.transform.SetParent(root.transform, false);
            worldGo.transform.localPosition = new Vector3(-16f * scale, 0.75f, -16f * scale);
            worldGo.transform.localScale = Vector3.one * scale;
            var world = worldGo.AddComponent<VoxelWorld>();
            WireWorld(world, a);
            SetBool(world, "initializeOnStart", false);
            SetInt(world, "worldHeight", 24);
            var so = new SerializedObject(world);
            so.FindProperty("sizeInChunks").vector2IntValue = new Vector2Int(2, 2);
            so.FindProperty("viewDistance").intValue = 3;
            so.FindProperty("teleportOnBlocks").boolValue = false;
            so.FindProperty("fogHidesEdges").boolValue = false;
            so.FindProperty("terrain.baseHeight").intValue = 8;
            so.FindProperty("terrain.hillHeight").floatValue = 6f;
            so.FindProperty("terrain.mountainHeight").floatValue = 6f;
            so.FindProperty("terrain.sandLevel").intValue = 6;
            so.FindProperty("terrain.snowLevel").intValue = 13;
            so.FindProperty("terrain.hillScale").floatValue = 0.06f;
            so.ApplyModifiedPropertiesWithoutUndo();

            var inv = root.AddComponent<BlockInventory>();
            var game = root.AddComponent<VoxelGame>();
            SetRef(game, "world", world);
            SetRef(game, "inventory", inv);
            SetEnum(game, "mode", (int)VoxelGameMode.Creativo);
            SetString(game, "worldName", "Maqueta");
            SetBool(game, "placePlayer", false);
            SetBool(game, "respawnOnDeath", false);
            AddHands(root, a, VoxelAimMode.RayoYTocar, 14f);

            Finish(root, 0.9f, 0f);
            Debug.Log("[VRKit] Maqueta montada: un mundo de 32 × 32 bloques de 3 cm sobre una mesa. Toca los bloques con el mando o apunta: gatillo derecho quita, gatillo izquierdo pone.");
        }
    }
}
#endif
