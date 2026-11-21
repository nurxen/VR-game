#if UNITY_EDITOR
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace VRKit.EditorTools
{
    /// <summary>
    /// Menú GameObject › VRKit › Mundo abierto: la escena completa (terreno infinito, biomas, cielo con estrellas,
    /// cuevas, criaturas, música) y los assets de ejemplo en Assets/_Juego/MundoAbierto (biomas, vegetación low poly,
    /// criaturas y materiales), todos editables.
    /// </summary>
    public static partial class VRKitBuilder
    {
        const string kOW = kRoot + "Mundo abierto/";
        const string kOWAssets = "Assets/_Juego/MundoAbierto";

        [MenuItem(kOW + "Mundo abierto completo", false, 200)] static void MenuOpenWorld() { BuildOpenWorldScene(); }
        [MenuItem(kOW + "Crear o completar biomas, vegetación y materiales", false, 220)]
        static void MenuOpenWorldAssets()
        {
            var a = EnsureOpenWorldAssets();
            if (a.biomes.Count > 0) { Selection.activeObject = a.biomes[0]; EditorGUIUtility.PingObject(a.biomes[0]); }
        }

        class OWAssets
        {
            public Material terrain, props, water, stars, crystal, skybox;
            public readonly List<BiomeProfile> biomes = new List<BiomeProfile>();
            public GameObject cave, deer, shadow;
        }

        static void OWFolder(string path)
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

        static Material OWMaterial(string name, string shader, string fallback)
        {
            string path = kOWAssets + "/Materiales/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            var sh = Shader.Find(shader);
            if (sh == null) { Debug.LogWarning("[VRKit] No encuentro el shader " + shader + " (¿proyecto sin URP?). Uso " + fallback + "."); sh = Shader.Find(fallback); }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Mesh OWMesh(string name, LowPolyMeshData d)
        {
            string path = kOWAssets + "/Vegetacion/Mallas/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = new Mesh { name = name };
            mesh.SetVertices(d.vertices);
            mesh.SetNormals(d.normals);
            mesh.SetColors(d.colors);
            mesh.SetTriangles(d.triangles, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Color32 C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }

        /// <summary>Prefab de vegetación: una malla low poly y, si hace falta, una colisión sencilla.</summary>
        static GameObject PropPrefab(string name, System.Action<LowPolyMeshData> build, Material mat, string collider, Vector3 colliderSize)
        {
            string path = kOWAssets + "/Vegetacion/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var d = new LowPolyMeshData();
            build(d);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = OWMesh(name, d);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider == "capsula")
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = colliderSize.x; c.height = colliderSize.y; c.center = new Vector3(0f, colliderSize.y * 0.5f, 0f);
            }
            else if (collider == "esfera")
            {
                var c = go.AddComponent<SphereCollider>();
                c.radius = colliderSize.x; c.center = new Vector3(0f, colliderSize.y, 0f);
            }
            return SavePrefab(go, path);
        }

        static OWAssets EnsureOpenWorldAssets()
        {
            var a = new OWAssets();
            OWFolder(kOWAssets + "/Materiales");
            OWFolder(kOWAssets + "/Biomas");
            OWFolder(kOWAssets + "/Vegetacion/Mallas");
            OWFolder(kOWAssets + "/Criaturas");

            a.terrain = OWMaterial("Terreno", "VRKit/TerrenoLowPoly", "Universal Render Pipeline/Simple Lit");
            a.props = OWMaterial("Vegetación", "VRKit/TerrenoLowPoly", "Universal Render Pipeline/Simple Lit");
            a.water = OWMaterial("Agua", "VRKit/Agua", "Universal Render Pipeline/Unlit");
            a.stars = OWMaterial("Estrellas", "VRKit/Estrellas", "Sprites/Default");
            a.crystal = OWMaterial("Cristal", "VRKit/Cristal", "Universal Render Pipeline/Unlit");
            a.skybox = OWMaterial("Cielo", "Skybox/Procedural", "Skybox/Procedural");
            if (a.skybox.HasProperty("_SunSize")) a.skybox.SetFloat("_SunSize", 0.035f);
            if (a.skybox.HasProperty("_AtmosphereThickness")) a.skybox.SetFloat("_AtmosphereThickness", 0.85f);

            var trunk = C(110, 80, 52); var leaf = C(78, 140, 62); var leaf2 = C(96, 160, 70); var pine = C(46, 102, 62);
            var snow = C(236, 240, 247); var rock = C(130, 126, 120); var grass = C(104, 166, 72); var birch = C(226, 222, 210);
            var m = a.props;

            var tree = PropPrefab("Árbol", d =>
            {
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.24f, 0.16f, 2.6f, 6, trunk);
                LowPolyShapes.Blob(d, new Vector3(0f, 3.4f, 0f), new Vector3(1.7f, 1.4f, 1.7f), 0.18f, 11, leaf);
                LowPolyShapes.Blob(d, new Vector3(0.5f, 4.3f, 0.2f), new Vector3(1.1f, 1f, 1.1f), 0.2f, 12, leaf2);
            }, m, "capsula", new Vector3(0.3f, 3f, 0f));
            var pineTree = PropPrefab("Pino", d =>
            {
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.2f, 0.12f, 1.4f, 6, trunk);
                LowPolyShapes.Cone(d, new Vector3(0f, 1.1f, 0f), 1.5f, 2.2f, 7, pine);
                LowPolyShapes.Cone(d, new Vector3(0f, 2.4f, 0f), 1.15f, 1.9f, 7, pine);
                LowPolyShapes.Cone(d, new Vector3(0f, 3.6f, 0f), 0.75f, 1.6f, 7, pine);
            }, m, "capsula", new Vector3(0.25f, 3f, 0f));
            var snowPine = PropPrefab("Pino nevado", d =>
            {
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.2f, 0.12f, 1.4f, 6, trunk);
                LowPolyShapes.Cone(d, new Vector3(0f, 1.1f, 0f), 1.5f, 2.2f, 7, pine);
                LowPolyShapes.Cone(d, new Vector3(0f, 2.4f, 0f), 1.15f, 1.9f, 7, snow);
                LowPolyShapes.Cone(d, new Vector3(0f, 3.6f, 0f), 0.75f, 1.6f, 7, snow);
            }, m, "capsula", new Vector3(0.25f, 3f, 0f));
            var birchTree = PropPrefab("Abedul de ribera", d =>
            {
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.17f, 0.11f, 3.2f, 6, birch);
                LowPolyShapes.Blob(d, new Vector3(0f, 3.8f, 0f), new Vector3(1.2f, 1.6f, 1.2f), 0.2f, 13, C(130, 182, 84));
            }, m, "capsula", new Vector3(0.22f, 3.4f, 0f));
            var bush = PropPrefab("Arbusto", d => LowPolyShapes.Blob(d, new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.5f, 0.7f), 0.25f, 14, leaf), m, null, Vector3.zero);
            var grassTuft = PropPrefab("Hierba", d =>
            {
                for (int i = 0; i < 4; i++)
                {
                    float an = i * 1.7f;
                    LowPolyShapes.Cone(d, new Vector3(Mathf.Cos(an) * 0.12f, 0f, Mathf.Sin(an) * 0.12f), 0.05f, 0.35f + i * 0.06f, 4, grass);
                }
            }, m, null, Vector3.zero);
            var flowers = PropPrefab("Flores", d =>
            {
                Color32[] petals = { C(236, 92, 104), C(250, 210, 80), C(170, 120, 230) };
                for (int i = 0; i < 3; i++)
                {
                    float an = i * 2.1f;
                    var p = new Vector3(Mathf.Cos(an) * 0.15f, 0f, Mathf.Sin(an) * 0.15f);
                    LowPolyShapes.Cylinder(d, p, 0.015f, 0.015f, 0.3f, 4, grass, false);
                    LowPolyShapes.Blob(d, p + new Vector3(0f, 0.33f, 0f), new Vector3(0.07f, 0.05f, 0.07f), 0f, 15 + i, petals[i], 0f);
                }
            }, m, null, Vector3.zero);
            var smallRock = PropPrefab("Roca pequeña", d => LowPolyShapes.Blob(d, new Vector3(0f, 0.15f, 0f), new Vector3(0.45f, 0.3f, 0.4f), 0.3f, 16, rock), m, null, Vector3.zero);
            var bigRock = PropPrefab("Roca grande", d => LowPolyShapes.Blob(d, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.3f, 1.5f), 0.3f, 17, rock), m, "esfera", new Vector3(1.4f, 0.7f, 0f));
            var cactus = PropPrefab("Cactus", d =>
            {
                var g = C(84, 150, 78);
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.22f, 0.2f, 2.2f, 7, g);
                LowPolyShapes.Cylinder(d, new Vector3(0.38f, 0.9f, 0f), 0.13f, 0.12f, 0.8f, 6, g);
                LowPolyShapes.Cylinder(d, new Vector3(-0.34f, 1.2f, 0f), 0.12f, 0.11f, 0.6f, 6, g);
                LowPolyShapes.Cylinder(d, new Vector3(0.38f, 0.85f, 0f) + new Vector3(-0.2f, 0f, 0f), 0.08f, 0.08f, 0.12f, 5, g);
            }, m, "capsula", new Vector3(0.25f, 2.2f, 0f));
            var dryBush = PropPrefab("Arbusto seco", d =>
            {
                var dry = C(150, 128, 86);
                for (int i = 0; i < 5; i++)
                {
                    float an = i * 1.25f;
                    LowPolyShapes.Cone(d, new Vector3(Mathf.Cos(an) * 0.15f, 0f, Mathf.Sin(an) * 0.15f), 0.04f, 0.5f + (i % 2) * 0.2f, 4, dry);
                }
            }, m, null, Vector3.zero);
            var mushroom = PropPrefab("Seta", d =>
            {
                LowPolyShapes.Cylinder(d, Vector3.zero, 0.05f, 0.04f, 0.18f, 6, C(236, 228, 214));
                LowPolyShapes.Cone(d, new Vector3(0f, 0.16f, 0f), 0.14f, 0.12f, 7, C(200, 60, 50));
            }, m, null, Vector3.zero);

            // Biomas: color del suelo y qué sale (densidad = por cada 100 m²)
            a.biomes.Add(Biome_(Biome.Pradera, "Pradera", new Color(0.46f, 0.67f, 0.31f), new[]
            {
                R(grassTuft, 6f, 0f, 35f, false, false), R(flowers, 1.2f, 0f, 25f, false, false), R(tree, 0.12f, 0f, 25f, false, true),
                R(bush, 0.25f, 0f, 30f, false, false), R(smallRock, 0.15f, 0f, 40f, true, false), R(bigRock, 0.03f, 0f, 40f, true, true),
            }));
            a.biomes.Add(Biome_(Biome.Bosque, "Bosque", new Color(0.28f, 0.5f, 0.23f), new[]
            {
                R(tree, 1.4f, 0f, 30f, false, true), R(pineTree, 0.5f, 0f, 35f, false, true), R(bush, 0.7f, 0f, 35f, false, false),
                R(mushroom, 0.35f, 0f, 25f, false, false), R(grassTuft, 2f, 0f, 35f, false, false), R(smallRock, 0.2f, 0f, 45f, true, false),
            }));
            a.biomes.Add(Biome_(Biome.Ribera, "Orilla del río", new Color(0.34f, 0.59f, 0.29f), new[]
            {
                R(birchTree, 1.1f, 0f, 25f, false, true, 2f), R(tree, 0.5f, 0f, 25f, false, true, 1.5f), R(grassTuft, 5f, 0f, 35f, false, false),
                R(flowers, 1f, 0f, 25f, false, false), R(bush, 0.5f, 0f, 30f, false, false),
            }));
            a.biomes.Add(Biome_(Biome.Desierto, "Desierto", new Color(0.87f, 0.75f, 0.49f), new[]
            {
                R(cactus, 0.12f, 0f, 20f, false, true), R(dryBush, 0.35f, 0f, 30f, false, false), R(bigRock, 0.05f, 0f, 40f, true, true),
                R(smallRock, 0.3f, 0f, 45f, true, false),
            }));
            a.biomes.Add(Biome_(Biome.Montana, "Montaña", new Color(0.5f, 0.47f, 0.44f), new[]
            {
                R(pineTree, 0.35f, 0f, 35f, false, true), R(bigRock, 0.15f, 0f, 60f, true, true), R(smallRock, 0.5f, 0f, 60f, true, false),
                R(grassTuft, 0.6f, 0f, 35f, false, false),
            }));
            a.biomes.Add(Biome_(Biome.Nevado, "Cumbres nevadas", new Color(0.93f, 0.94f, 0.97f), new[]
            {
                R(snowPine, 0.2f, 0f, 30f, false, true), R(bigRock, 0.1f, 0f, 60f, true, true),
            }));
            a.biomes.Add(Biome_(Biome.Playa, "Playa", new Color(0.89f, 0.82f, 0.6f), new[]
            {
                R(smallRock, 0.1f, 0f, 30f, true, false), R(grassTuft, 0.4f, 0f, 25f, false, false),
            }));
            a.biomes.Add(Biome_(Biome.Fondo, "Fondo del agua", new Color(0.46f, 0.42f, 0.33f), new BiomePropRule[0]));

            a.cave = CaveEntrancePrefab(m, rock);
            a.deer = CreaturePrefab("Ciervo", CreatureMood.Pacifica, m, a.crystal);
            a.shadow = CreaturePrefab("Sombra nocturna", CreatureMood.Hostil, m, a.crystal);
            AssetDatabase.SaveAssets();
            return a;
        }

        static BiomePropRule R(GameObject prefab, float density, float minSlope, float maxSlope, bool align, bool collide, float riverBoost = 1f)
        {
            return new BiomePropRule
            {
                prefab = prefab, density = density, slope = new Vector2(minSlope, maxSlope), altitude = new Vector2(0.4f, 500f),
                scale = collide ? new Vector2(0.8f, 1.3f) : new Vector2(0.7f, 1.3f), alignToSlope = align, collide = collide,
                riverBoost = riverBoost, sink = align ? 0.2f : 0.1f,
            };
        }

        static BiomeProfile Biome_(Biome biome, string display, Color ground, BiomePropRule[] rules)
        {
            string path = kOWAssets + "/Biomas/" + display + ".asset";
            var b = AssetDatabase.LoadAssetAtPath<BiomeProfile>(path);
            if (b != null) return b;
            b = ScriptableObject.CreateInstance<BiomeProfile>();
            b.biome = biome; b.displayName = display; b.groundColor = ground;
            b.props = new List<BiomePropRule>(rules);
            AssetDatabase.CreateAsset(b, path);
            return b;
        }

        static GameObject CaveEntrancePrefab(Material m, Color32 rock)
        {
            string path = kOWAssets + "/Entrada de cueva.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var d = new LowPolyMeshData();
            // Arco de rocas alrededor de un hueco oscuro, mirando a +Z
            for (int i = 0; i <= 6; i++)
            {
                float an = Mathf.PI * i / 6f;
                var p = new Vector3(Mathf.Cos(an) * 1.9f, Mathf.Sin(an) * 2.1f, 0f);
                LowPolyShapes.Blob(d, p, new Vector3(0.85f, 0.8f, 1f), 0.3f, 40 + i, rock);
            }
            LowPolyShapes.Blob(d, new Vector3(0f, 1f, -0.4f), new Vector3(1.6f, 1.7f, 0.6f), 0.1f, 50, C(12, 10, 14), 0f);
            LowPolyShapes.Blob(d, new Vector3(0f, 3f, -1f), new Vector3(3.2f, 1.8f, 2.4f), 0.3f, 51, rock);
            var go = new GameObject("Entrada de cueva");
            go.AddComponent<MeshFilter>().sharedMesh = OWMesh("Entrada de cueva", d);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.AddComponent<CaveEntrance>();
            var back = go.AddComponent<BoxCollider>();
            back.center = new Vector3(0f, 2.2f, -1.4f); back.size = new Vector3(4.5f, 4f, 1.5f);
            return SavePrefab(go, path);
        }

        static GameObject CreaturePrefab(string name, CreatureMood mood, Material m, Material glow)
        {
            string path = kOWAssets + "/Criaturas/" + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var d = new LowPolyMeshData();
            var go = new GameObject(name);
            var agent = go.AddComponent<NavMeshAgent>();
            if (mood == CreatureMood.Pacifica)
            {
                var fur = C(150, 104, 66);
                LowPolyShapes.Blob(d, new Vector3(0f, 1f, 0f), new Vector3(0.38f, 0.38f, 0.75f), 0.1f, 60, fur);
                for (int i = 0; i < 4; i++)
                    LowPolyShapes.Cylinder(d, new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0f, i < 2 ? 0.45f : -0.45f), 0.05f, 0.06f, 0.85f, 5, C(110, 76, 50), false);
                LowPolyShapes.Blob(d, new Vector3(0f, 1.45f, 0.75f), new Vector3(0.18f, 0.2f, 0.28f), 0.1f, 61, fur);
                LowPolyShapes.Cone(d, new Vector3(-0.1f, 1.6f, 0.7f), 0.03f, 0.35f, 4, C(220, 205, 180));
                LowPolyShapes.Cone(d, new Vector3(0.1f, 1.6f, 0.7f), 0.03f, 0.35f, 4, C(220, 205, 180));
                agent.speed = 2.2f; agent.radius = 0.4f; agent.height = 1.6f;
            }
            else
            {
                var dark = C(34, 30, 52);
                LowPolyShapes.Blob(d, new Vector3(0f, 0.7f, 0f), new Vector3(0.55f, 0.6f, 0.55f), 0.35f, 62, dark);
                LowPolyShapes.Blob(d, new Vector3(0f, 1.25f, 0.1f), new Vector3(0.35f, 0.32f, 0.35f), 0.3f, 63, dark);
                agent.speed = 2.6f; agent.radius = 0.45f; agent.height = 1.5f;
                // Ojos que brillan
                var eyes = new LowPolyMeshData();
                LowPolyShapes.Blob(eyes, new Vector3(-0.12f, 1.3f, 0.4f), new Vector3(0.06f, 0.05f, 0.03f), 0f, 64, C(255, 220, 120), 0f);
                LowPolyShapes.Blob(eyes, new Vector3(0.12f, 1.3f, 0.4f), new Vector3(0.06f, 0.05f, 0.03f), 0f, 65, C(255, 220, 120), 0f);
                var eg = new GameObject("Ojos");
                eg.transform.SetParent(go.transform, false);
                eg.AddComponent<MeshFilter>().sharedMesh = OWMesh(name + " ojos", eyes);
                eg.AddComponent<MeshRenderer>().sharedMaterial = glow;
                var h = go.AddComponent<Health>();
                SetFloat(h, "maxHealth", 20f);
            }
            var body = new GameObject("Cuerpo");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = OWMesh(name, d);
            body.AddComponent<MeshRenderer>().sharedMaterial = m;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.4f; col.height = 1.5f; col.center = new Vector3(0f, 0.8f, 0f);
            var c = go.AddComponent<WorldCreature>();
            SetEnum(c, "mood", (int)mood);
            if (mood == CreatureMood.Hostil) SetFloat(c, "damage", 6f);
            return SavePrefab(go, path);
        }

        static void BuildOpenWorldScene()
        {
            var a = EnsureOpenWorldAssets();
            var origin = EnsureXROrigin();
            CreateGameSystems();
            if (Object.FindFirstObjectByType<VRMenuManager>() == null) BuildMenu(false);
            EnsureSubtitles();
            if (origin != null)
            {
                // Modo desarrollador (los dos joysticks hundidos 1 s, o F1) y andar/girar con los gatillos
                if (origin.GetComponent<VRDevMode>() == null) Undo.AddComponent<VRDevMode>(origin.gameObject);
                if (origin.GetComponent<TriggerLocomotion>() == null) Undo.AddComponent<TriggerLocomotion>(origin.gameObject);
            }

            var root = new GameObject("Mundo abierto");
            var world = root.AddComponent<OpenWorld>();
            SetRef(world, "terrainMaterial", a.terrain);
            SetRef(world, "waterMaterial", a.water);
            SetRef(world, "propMaterial", a.props);
            SetObjArray(world, "biomes", a.biomes.ConvertAll(b => (Object)b).ToArray());
            SetRef(world, "caveEntrancePrefab", a.cave);

            // Cielo: sol, luna y estrellas
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) { sun = new GameObject("Sol").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.SetParent(root.transform, false); }
            sun.shadows = LightShadows.None;
            var moon = new GameObject("Luna").AddComponent<Light>();
            moon.type = LightType.Directional; moon.color = new Color(0.65f, 0.72f, 1f); moon.shadows = LightShadows.None;
            moon.transform.SetParent(root.transform, false);
            var sky = root.AddComponent<SkyCycle>();
            SetRef(sky, "sun", sun);
            SetRef(sky, "moon", moon);
            SetRef(sky, "starMaterial", a.stars);
            RenderSettings.skybox = a.skybox;
            RenderSettings.sun = sun;

            var caves = root.AddComponent<CaveSystem>();
            SetRef(caves, "wallMaterial", a.terrain);
            SetRef(caves, "crystalMaterial", a.crystal);

            root.AddComponent<WorldNavMesh>();
            var spawner = root.AddComponent<CreatureSpawner>();
            SetObjArray(spawner, "dayCreatures", new Object[] { a.deer });
            SetObjArray(spawner, "nightCreatures", new Object[] { a.shadow });
            root.AddComponent<BiomeAudio>();
            root.AddComponent<WorldDiscovery>();

            Undo.RegisterCreatedObjectUndo(root, "VRKit");
            Selection.activeGameObject = root;
            Debug.Log("[VRKit] Mundo abierto montado. Play: apareces en tierra firme y el terreno se genera alrededor. " +
                      "Biomas, vegetación y criaturas en " + kOWAssets + ". Para otro mundo: Seed en el OpenWorld (0 = al azar).");
        }
    }
}
#endif
