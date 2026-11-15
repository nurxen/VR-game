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
    /// <summary>Menú GameObject › VRKit › Objetos e inventario: moneda, poción, caldero con recetas, mochila y linterna.</summary>
    public static partial class VRKitBuilder
    {
        const string kObj = kRoot + "Objetos e inventario/";


        [MenuItem(kObj + "Linterna", false, 15)]
        static void CreateFlashlight()
        {
            var fl = new GameObject("Linterna");
            Prim(PrimitiveType.Cylinder, "Cuerpo", fl.transform, new Vector3(0f, 0f, 0.06f), new Vector3(0.035f, 0.09f, 0.035f), Mat("Metal oscuro", new Color(0.15f, 0.15f, 0.17f)), true)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(fl.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.16f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.range = 15f;
            light.spotAngle = 40f;
            light.intensity = 3f;
            light.shadows = LightShadows.None; // las sombras en tiempo real son caras en Quest 2
            fl.AddComponent<Rigidbody>();
            fl.AddComponent<XRGrabInteractable>();
            var f = fl.AddComponent<Flashlight>();
            SetRef(f, "lightSource", light);
            SetBool(f, "startOn", true);
            Finish(fl, 0.5f, 1.1f, -0.15f);
        }

        [MenuItem(kObj + "Moneda coleccionable", false, 45)] static void MenuCoin() { Finish(BuildCoin(), 0.8f, 1.2f); }

        [MenuItem(kObj + "Poción para beber", false, 46)] static void MenuPotion() { Finish((GameObject)PrefabUtility.InstantiatePrefab(PotionPrefab()), 0.5f, 1.1f); }

        [MenuItem(kObj + "Caldero con recetas e ingredientes", false, 47)] static void MenuCauldron() { Finish(BuildAlchemy(), 1.2f, 0f); }

        [MenuItem(kObj + "Mochila al hombro", false, 48)] static void MenuBackpack() { var go = BuildBackpack(); if (go != null) { Undo.RegisterCreatedObjectUndo(go, "VRKit"); Selection.activeGameObject = go; } }


        static GameObject BuildCoin()
        {
            var coin = Prim(PrimitiveType.Cylinder, "Moneda", null, Vector3.zero, new Vector3(0.12f, 0.01f, 0.12f), Mat("Oro", new Color(1f, 0.78f, 0.2f)), false);
            coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var holder = new GameObject("Moneda");
            coin.transform.SetParent(holder.transform, false);
            coin.name = "Disco";
            holder.AddComponent<Spinner>();
            var c = holder.AddComponent<Collectible>();
            SetInt(c, "score", 5);
            SetString(c, "objectiveId", "monedas");
            SetString(c, "savedCounter", "monedas");
            return holder;
        }


        static GameObject PotionPrefab()
        {
            string path = kGenerated + "/Pocion de vida.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Poción de vida");
            Prim(PrimitiveType.Cylinder, "Frasco", root.transform, Vector3.zero, new Vector3(0.06f, 0.05f, 0.06f), Mat("Poción roja", new Color(0.9f, 0.15f, 0.25f)), true);
            Prim(PrimitiveType.Cylinder, "Cuello", root.transform, new Vector3(0f, 0.07f, 0f), new Vector3(0.025f, 0.025f, 0.025f), Mat("Cristal", new Color(0.8f, 0.9f, 1f)), false);
            Prim(PrimitiveType.Cylinder, "Corcho", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.022f, 0.012f, 0.022f), Mat("Corcho", new Color(0.6f, 0.45f, 0.3f)), false);
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 0.3f;
            root.AddComponent<XRGrabInteractable>();
            root.AddComponent<GrabHighlight>();
            var c = root.AddComponent<Consumable>();
            SetFloat(c, "heal", 40f);
            var item = root.AddComponent<VRItem>();
            item.id = "pocion_vida"; item.displayName = "Poción de vida"; item.category = "pocion";
            return SavePrefab(root, path);
        }


        static GameObject IngredientPrefab(string id, string name, PrimitiveType shape, Color color, Vector3 scale)
        {
            string path = kGenerated + "/Ingrediente " + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = Prim(shape, name, null, Vector3.zero, scale, Mat(name, color), true);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.2f;
            go.AddComponent<XRGrabInteractable>();
            go.AddComponent<GrabHighlight>();
            var item = go.AddComponent<VRItem>();
            item.id = id; item.displayName = name; item.category = "ingrediente";
            return SavePrefab(go, path);
        }


        static CraftingRecipe Recipe(string file, string name, GameObject result, string objective, params string[] ingredients)
        {
            EnsureFolder();
            string path = kGenerated + "/" + file + ".asset";
            var r = AssetDatabase.LoadAssetAtPath<CraftingRecipe>(path);
            if (r == null) { r = ScriptableObject.CreateInstance<CraftingRecipe>(); AssetDatabase.CreateAsset(r, path); }
            r.recipeName = name; r.result = result; r.resultCount = 1; r.objectiveId = objective;
            var list = new List<CraftingRecipe.Ingredient>();
            foreach (var s in ingredients)
            {
                var parts = s.Split('x');
                list.Add(new CraftingRecipe.Ingredient { itemId = parts[0], count = parts.Length > 1 ? int.Parse(parts[1]) : 1 });
            }
            r.ingredients = list.ToArray();
            EditorUtility.SetDirty(r);
            return r;
        }


        static GameObject BuildAlchemy()
        {
            var root = new GameObject("Rincón de alquimia");
            var seta = IngredientPrefab("seta_roja", "Seta roja", PrimitiveType.Sphere, new Color(0.85f, 0.2f, 0.2f), new Vector3(0.07f, 0.05f, 0.07f));
            var cristal = IngredientPrefab("cristal_azul", "Cristal azul", PrimitiveType.Cube, new Color(0.3f, 0.6f, 1f), new Vector3(0.04f, 0.08f, 0.04f));
            var potion = PotionPrefab();
            var rPotion = Recipe("Receta Pocion de vida", "Poción de vida", potion, "pocion", "seta_rojax2", "cristal_azulx1");
            var rDouble = Recipe("Receta Pocion doble", "Dos pociones", potion, "pocion", "cristal_azulx2", "seta_rojax1");
            rDouble.resultCount = 2;

            // Caldero: la zona Trigger absorbe lo que cae dentro
            var cauldron = new GameObject("Caldero");
            cauldron.transform.SetParent(root.transform, false);
            var iron = Mat("Hierro negro", new Color(0.12f, 0.12f, 0.13f));
            Prim(PrimitiveType.Cylinder, "Olla", cauldron.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.3f, 0.6f), iron, false);
            Prim(PrimitiveType.Cylinder, "Líquido", cauldron.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.52f, 0.01f, 0.52f), UnlitMat("Líquido", new Color(0.3f, 0.9f, 0.4f)), false);
            var zone = cauldron.AddComponent<BoxCollider>();
            zone.center = new Vector3(0f, 0.55f, 0f); zone.size = new Vector3(0.6f, 0.5f, 0.6f);
            var station = cauldron.AddComponent<CraftingStation>();
            station.SetRecipes(new[] { rPotion, rDouble });
            var output = Empty("Salida", cauldron.transform, new Vector3(0f, 1.0f, -0.35f));
            SetRef(station, "output", output);
            var contents = Label3D(cauldron.transform, "(vacío)", new Vector3(0f, 1.15f, 0f), 0.5f, new Color(0.7f, 1f, 0.75f));
            SetRef(station, "contentsLabel", contents);

            // Cestas de ingredientes (dan uno nuevo cada vez)
            var b1 = Basket("Cesta de setas", seta, new Color(0.85f, 0.2f, 0.2f));
            b1.transform.SetParent(root.transform, false); b1.transform.localPosition = new Vector3(-0.8f, 0.8f, 0f);
            var b2 = Basket("Cesta de cristales", cristal, new Color(0.3f, 0.6f, 1f));
            b2.transform.SetParent(root.transform, false); b2.transform.localPosition = new Vector3(0.8f, 0.8f, 0f);
            Prim(PrimitiveType.Cube, "Mesa izq.", root.transform, new Vector3(-0.8f, 0.38f, 0f), new Vector3(0.5f, 0.76f, 0.5f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), true);
            Prim(PrimitiveType.Cube, "Mesa der.", root.transform, new Vector3(0.8f, 0.38f, 0f), new Vector3(0.5f, 0.76f, 0.5f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), true);

            var board = Label3D(root.transform, "<b>RECETAS</b>\nPoción de vida = 2 setas rojas + 1 cristal azul\nDos pociones = 2 cristales azules + 1 seta roja\n<size=70%>Echa los ingredientes al caldero. Bébete la poción acercándola a la boca.</size>",
                new Vector3(0f, 1.9f, 0.5f), 0.6f, Color.white);
            board.rectTransform.sizeDelta = new Vector2(2.2f, 0.8f);
            return root;
        }


        static GameObject Basket(string name, GameObject prefab, Color color)
        {
            var b = Prim(PrimitiveType.Cylinder, name, null, Vector3.zero, new Vector3(0.3f, 0.06f, 0.3f), Mat("Mimbre", new Color(0.7f, 0.55f, 0.3f)), true);
            Prim(PrimitiveType.Sphere, "Montón", b.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 0.8f, 0.7f), Mat(name, color), false);
            b.AddComponent<XRSimpleInteractable>();
            b.AddComponent<GrabHighlight>();
            var s = b.AddComponent<SpawnOnGrab>();
            SetRef(s, "prefab", prefab.GetComponent<XRGrabInteractable>());
            return b;
        }


        static GameObject BuildBackpack()
        {
            var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if (origin == null) { Debug.LogWarning("[VRKit] La mochila va en el jugador: añade antes el XR Origin."); return null; }
            var body = new GameObject("Cuerpo (cinturón y mochila)");
            body.transform.SetParent(origin.transform, false);
            body.AddComponent<BodyFollower>();
            var zone = new GameObject("Mochila (detrás del hombro derecho)");
            zone.transform.SetParent(body.transform, false);
            zone.transform.localPosition = new Vector3(0.2f, 0.45f, -0.22f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = Vector3.one * 0.35f;
            zone.AddComponent<XRSimpleInteractable>();
            zone.AddComponent<BackpackStorage>();
            Debug.Log("[VRKit] Mochila creada: suelta un objeto por encima del hombro derecho para guardarlo; mete la mano ahí y aprieta el grip para sacarlo.");
            return body;
        }
    }
}
#endif
