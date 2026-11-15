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
    /// <summary>Menú GameObject › VRKit › Enemigos: enemigo a pie, dron volador y arena de oleadas.</summary>
    public static partial class VRKitBuilder
    {
        const string kEne = kRoot + "Enemigos/";


        [MenuItem(kEne + "Enemigo básico", false, 17)]
        static void CreateEnemy()
        {
            var root = new GameObject("Enemigo");
            var red = Mat("Enemigo", new Color(0.7f, 0.15f, 0.2f));
            Prim(PrimitiveType.Capsule, "Cuerpo", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1f, 0.6f), red, true);
            Prim(PrimitiveType.Cube, "Ojos", root.transform, new Vector3(0f, 1.5f, 0.27f), new Vector3(0.35f, 0.08f, 0.05f), UnlitMat("Ojos", new Color(1f, 0.9f, 0.2f)), false);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 2.2f;
            agent.stoppingDistance = 1.2f;
            agent.radius = 0.35f;
            agent.height = 2f;
            var h = root.AddComponent<Health>();
            SetFloat(h, "maxHealth", 50f);
            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;
            var e = root.AddComponent<SimpleEnemy>();
            SetRef(e, "audioSource", audio);
            Finish(root, 6f, 0f);
            Debug.Log("[VRKit] Enemigo creado. Para que se mueva, el suelo necesita un NavMesh: añade 'NavMeshSurface' al suelo (paquete AI Navigation) y pulsa Bake.");
        }


        [MenuItem(kEne + "Dron volador", false, 80)] static void MenuDrone() { Finish((GameObject)PrefabUtility.InstantiatePrefab(DronePrefab()), 6f, 2.5f); }

        [MenuItem(kEne + "Arena de oleadas", false, 81)] static void MenuArena() { Finish(BuildArena(), 6f, 0f); }


        // ================================================================== Enemigos

        static GameObject EnemyBoltPrefab()
        {
            string path = kGenerated + "/Disparo enemigo.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var bolt = Prim(PrimitiveType.Sphere, "Disparo enemigo", null, Vector3.zero, Vector3.one * 0.08f, UnlitMat("Disparo enemigo", new Color(1f, 0.25f, 0.3f)), false);
            var rb = bolt.AddComponent<Rigidbody>(); rb.useGravity = false;
            var p = bolt.AddComponent<Projectile>();
            SetBool(p, "ignorePlayer", false);
            return SavePrefab(bolt, path);
        }


        static GameObject DronePrefab()
        {
            string path = kGenerated + "/Dron.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var d = Prim(PrimitiveType.Sphere, "Dron", null, Vector3.zero, Vector3.one * 0.4f, Mat("Dron", new Color(0.3f, 0.32f, 0.36f)), true);
            var eye = Prim(PrimitiveType.Sphere, "Ojo", d.transform, new Vector3(0f, 0f, 0.42f), Vector3.one * 0.35f, UnlitMat("Ojo dron", new Color(1f, 0.3f, 0.2f)), false);
            for (int i = 0; i < 2; i++)
                Prim(PrimitiveType.Cylinder, "Hélice", d.transform, new Vector3(i == 0 ? -0.7f : 0.7f, 0.3f, 0f), new Vector3(0.6f, 0.02f, 0.6f), Mat("Metal oscuro", new Color(0.15f, 0.15f, 0.17f)), false);
            var muzzle = Empty("Muzzle", d.transform, new Vector3(0f, 0f, 0.6f));
            var h = d.AddComponent<Health>();
            SetFloat(h, "maxHealth", 30f);
            var f = d.AddComponent<FlyingEnemy>();
            SetRef(f, "projectile", EnemyBoltPrefab().GetComponent<Projectile>());
            SetRef(f, "muzzle", muzzle);
            SetRef(f, "warningRenderer", eye.GetComponent<Renderer>());
            var bar = d.AddComponent<HealthBar>();
            SetFloat(bar, "height", 1.2f);
            return SavePrefab(d, path);
        }


        static GameObject WalkerPrefab()
        {
            string path = kGenerated + "/Enemigo de oleada.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Enemigo de oleada");
            Prim(PrimitiveType.Capsule, "Cuerpo", root.transform, new Vector3(0f, 1f, 0f), new Vector3(0.6f, 1f, 0.6f), Mat("Enemigo", new Color(0.7f, 0.15f, 0.2f)), true);
            Prim(PrimitiveType.Cube, "Ojos", root.transform, new Vector3(0f, 1.5f, 0.27f), new Vector3(0.35f, 0.08f, 0.05f), UnlitMat("Ojos", new Color(1f, 0.9f, 0.2f)), false);
            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 2.2f; agent.stoppingDistance = 1.2f; agent.radius = 0.35f; agent.height = 2f;
            var h = root.AddComponent<Health>();
            SetFloat(h, "maxHealth", 40f);
            var e = root.AddComponent<SimpleEnemy>();
            SetFloat(e, "detectRange", 30f);
            SetFloat(e, "loseRange", 40f);
            SetBool(e, "requireLineOfSight", false);
            root.AddComponent<HealthBar>();
            return SavePrefab(root, path);
        }


        static GameObject BuildArena()
        {
            var root = new GameObject("Arena de oleadas");
            var spawnPoints = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f) * Mathf.PI * 2f + 0.4f;
                var sp = Empty("Punto de aparición " + (i + 1), root.transform, new Vector3(Mathf.Cos(a) * 6f, 0f, Mathf.Sin(a) * 6f + 2f));
                Prim(PrimitiveType.Cylinder, "Marca", sp, new Vector3(0f, 0.01f, 0f), new Vector3(0.8f, 0.01f, 0.8f), UnlitMat("Marca aparición", new Color(0.6f, 0.3f, 1f)), false);
                spawnPoints.Add(sp);
            }
            var spawner = root.AddComponent<WaveSpawner>();
            var walker = WalkerPrefab();
            var drone = DronePrefab();
            spawner.SetWaves(new[]
            {
                new WaveSpawner.Wave { name = "Oleada", spawnInterval = 1.2f, enemies = new[] { new WaveSpawner.Entry { prefab = walker, count = 3 } } },
                new WaveSpawner.Wave { name = "Oleada", spawnInterval = 1.5f, enemies = new[] { new WaveSpawner.Entry { prefab = drone, count = 3 } } },
                new WaveSpawner.Wave { name = "Oleada", spawnInterval = 1f, enemies = new[] { new WaveSpawner.Entry { prefab = walker, count = 4 }, new WaveSpawner.Entry { prefab = drone, count = 2 } } },
            });
            SetObjArray(spawner, "spawnPoints", spawnPoints.ToArray());
            SetBool(spawner, "victoryWhenDone", false);
            var status = Label3D(root.transform, "Pulsa el botón para empezar", new Vector3(0f, 2.6f, -3f), 0.9f, new Color(1f, 0.7f, 0.5f));
            SetRef(spawner, "statusLabel", status);

            // Botón de inicio
            var button = new GameObject("Botón de inicio");
            button.transform.SetParent(root.transform, false);
            button.transform.localPosition = new Vector3(0f, 0f, -3f);
            Prim(PrimitiveType.Cylinder, "Pedestal", button.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.3f, 0.45f, 0.3f), Mat("Piedra", new Color(0.5f, 0.5f, 0.52f)), true);
            var cap = Prim(PrimitiveType.Cylinder, "Pulsador", button.transform, new Vector3(0f, 0.93f, 0f), new Vector3(0.18f, 0.03f, 0.18f), Mat("Rojo botón", new Color(0.85f, 0.15f, 0.15f)), true);
            var top = Empty("Botón", button.transform, new Vector3(0f, 0.93f, 0f)).gameObject;
            var col = top.AddComponent<BoxCollider>(); col.size = new Vector3(0.2f, 0.08f, 0.2f);
            top.AddComponent<XRSimpleInteractable>();
            var pokeType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Filtering.XRPokeFilter, Unity.XR.Interaction.Toolkit");
            if (pokeType != null) top.AddComponent(pokeType);
            var vb = top.AddComponent<VRButton>();
            SetRef(vb, "pressVisual", cap.transform);
            UnityEventTools.AddPersistentListener(vb.onPressed, new UnityAction(spawner.Begin));
            return root;
        }
    }
}
#endif
