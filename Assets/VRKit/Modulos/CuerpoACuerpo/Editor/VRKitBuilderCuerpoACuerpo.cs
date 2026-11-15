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
    /// <summary>Menú GameObject › VRKit › Cuerpo a cuerpo: espada, escudo, granadas y muñeco de entrenamiento.</summary>
    public static partial class VRKitBuilder
    {
        const string kMelee = kRoot + "Cuerpo a cuerpo/";


        [MenuItem(kMelee + "Espada", false, 60)] static void MenuSword() { Finish(BuildSword(), 0.5f, 1.1f, 0.2f); }

        [MenuItem(kMelee + "Escudo", false, 61)] static void MenuShield() { Finish(BuildShield(), 0.5f, 1.1f, -0.3f); }

        [MenuItem(kMelee + "Granada", false, 62)] static void MenuGrenade() { Finish((GameObject)PrefabUtility.InstantiatePrefab(GrenadePrefab()), 0.5f, 1.1f); }

        [MenuItem(kMelee + "Caja de granadas", false, 63)] static void MenuGrenadeCrate() { Finish(BuildGrenadeCrate(), 0.8f, 0f); }

        [MenuItem(kMelee + "Muñeco de entrenamiento", false, 64)] static void MenuDummy() { Finish(BuildDummy(), 2f, 0f); }


        // ================================================================== Combate

        static GameObject BuildSword()
        {
            var root = new GameObject("Espada");
            var metal = Mat("Hoja", new Color(0.82f, 0.85f, 0.9f));
            Prim(PrimitiveType.Cylinder, "Empuñadura", root.transform, Vector3.zero, new Vector3(0.035f, 0.09f, 0.035f), Mat("Cuero", new Color(0.35f, 0.2f, 0.1f)), true);
            Prim(PrimitiveType.Cube, "Guarda", root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(0.18f, 0.025f, 0.04f), Mat("Oro", new Color(1f, 0.78f, 0.2f)), false);
            var blade = Prim(PrimitiveType.Cube, "Hoja", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.05f, 0.78f, 0.012f), metal, true);
            blade.GetComponent<Collider>().isTrigger = true; // corta sin rebotar
            var tip = Empty("Tip", root.transform, new Vector3(0f, 0.88f, 0f));
            var attach = Empty("Grip Attach", root.transform, Vector3.zero);
            attach.localRotation = Quaternion.Euler(90f, 0f, 0f); // la hoja hacia delante en la mano
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 1.2f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            root.AddComponent<GrabHighlight>();
            var trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.15f; trail.widthMultiplier = 0.04f; trail.emitting = false;
            trail.sharedMaterial = UnlitMat("Estela", new Color(1f, 1f, 1f, 0.5f));
            var m = root.AddComponent<MeleeWeapon>();
            SetRef(m, "tip", tip);
            SetRef(m, "trail", trail);
            root.AddComponent<ReturnToSpawn>();
            return root;
        }


        static GameObject BuildShield()
        {
            var root = new GameObject("Escudo");
            var disc = Prim(PrimitiveType.Cylinder, "Escudo", root.transform, Vector3.zero, new Vector3(0.5f, 0.02f, 0.5f), Mat("Escudo", new Color(0.2f, 0.35f, 0.7f)), true);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // la cara mira al eje Z
            Prim(PrimitiveType.Cylinder, "Umbo", root.transform, new Vector3(0f, 0f, 0.02f), new Vector3(0.12f, 0.02f, 0.12f), Mat("Oro", new Color(1f, 0.78f, 0.2f)), false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Cube, "Asa", root.transform, new Vector3(0f, 0f, -0.05f), new Vector3(0.03f, 0.14f, 0.03f), Mat("Cuero", new Color(0.35f, 0.2f, 0.1f)), true);
            var attach = Empty("Grip Attach", root.transform, new Vector3(0f, 0f, -0.05f));
            var rb = root.AddComponent<Rigidbody>(); rb.mass = 2f;
            var grab = root.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;
            root.AddComponent<GrabHighlight>();
            root.AddComponent<Shield>();
            root.AddComponent<ReturnToSpawn>();
            return root;
        }


        static GameObject GrenadePrefab()
        {
            string path = kGenerated + "/Granada.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var g = Prim(PrimitiveType.Sphere, "Granada", null, Vector3.zero, Vector3.one * 0.08f, Mat("Granada", new Color(0.25f, 0.35f, 0.2f)), true);
            Prim(PrimitiveType.Cylinder, "Anilla", g.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.3f, 0.15f, 0.3f), Mat("Metal claro", new Color(0.7f, 0.7f, 0.72f)), false);
            var led = Prim(PrimitiveType.Sphere, "Luz armada", g.transform, new Vector3(0f, 0.35f, 0.4f), Vector3.one * 0.25f, UnlitMat("Luz roja", new Color(1f, 0.1f, 0.1f)), false);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = 0.4f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            g.AddComponent<XRGrabInteractable>();
            g.AddComponent<Throwable>();
            g.AddComponent<GrabHighlight>();
            var gr = g.AddComponent<Grenade>();
            SetRef(gr, "armedIndicator", led);
            var item = g.AddComponent<VRItem>();
            item.id = "bomba"; item.displayName = "Bomba"; item.category = "arma";
            return SavePrefab(g, path);
        }


        static GameObject BuildGrenadeCrate()
        {
            var crate = Prim(PrimitiveType.Cube, "Caja de granadas", null, Vector3.zero, new Vector3(0.5f, 0.3f, 0.35f), Mat("Caja militar", new Color(0.3f, 0.38f, 0.22f)), true);
            crate.transform.position = new Vector3(0f, 0.15f, 0f);
            var holder = new GameObject("Caja de granadas");
            crate.transform.SetParent(holder.transform, true);
            crate.name = "Caja";
            crate.AddComponent<XRSimpleInteractable>();
            crate.AddComponent<GrabHighlight>();
            var s = crate.AddComponent<SpawnOnGrab>();
            SetRef(s, "prefab", GrenadePrefab().GetComponent<XRGrabInteractable>());
            Label3D(holder.transform, "GRANADAS\n<size=60%>gatillo = quitar anilla</size>", new Vector3(0f, 0.45f, 0f), 0.5f, new Color(1f, 0.9f, 0.5f));
            return holder;
        }


        static GameObject BuildDummy()
        {
            var root = new GameObject("Muñeco de entrenamiento");
            var straw = Mat("Paja", new Color(0.8f, 0.68f, 0.38f));
            Prim(PrimitiveType.Cylinder, "Poste", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.08f, 0.5f, 0.08f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), false);
            Prim(PrimitiveType.Capsule, "Cuerpo", root.transform, new Vector3(0f, 1.3f, 0f), new Vector3(0.45f, 0.45f, 0.35f), straw, false);
            Prim(PrimitiveType.Sphere, "Cabeza", root.transform, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.3f, straw, false);
            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1.4f, 0f); col.height = 1.2f; col.radius = 0.25f;
            var h = root.AddComponent<Health>();
            SetFloat(h, "maxHealth", 150f);
            UnityEventTools.AddPersistentListener(h.onDeath, new UnityAction(h.ResetHealth)); // nunca se rompe
            var bar = root.AddComponent<HealthBar>();
            SetFloat(bar, "height", 2.2f);
            return root;
        }
    }
}
#endif
