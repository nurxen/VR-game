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
    /// <summary>Menú GameObject › VRKit › Poderes: hechizos, telequinesis, gancho, tiempo bala y portales.</summary>
    public static partial class VRKitBuilder
    {
        const string kPod = kRoot + "Poderes/";

        [MenuItem(kPod + "Pareja de portales", false, 49)] static void MenuPortals() { Finish(BuildPortals(8f), 3f, 0f); }


        [MenuItem(kPod + "Hechizos en la mano derecha", false, 70)] static void MenuCaster() { AddCaster(Hand.Right); }

        [MenuItem(kPod + "Telequinesis en la mano izquierda", false, 71)] static void MenuTelekinesis() { AddTelekinesis(Hand.Left); }

        [MenuItem(kPod + "Gancho en la mano derecha", false, 72)] static void MenuGrapple() { AddGrapple(Hand.Right); }

        [MenuItem(kPod + "Tiempo bala", false, 73)] static void MenuTime() { AddTimeController(); }


        static GameObject BuildPortals(float separation)
        {
            var root = new GameObject("Portales");
            var a = Portal("Portal A", new Color(0.3f, 0.6f, 1f));
            var b = Portal("Portal B", new Color(1f, 0.55f, 0.2f));
            a.transform.SetParent(root.transform, false);
            b.transform.SetParent(root.transform, false);
            a.transform.localPosition = Vector3.zero;
            b.transform.localPosition = new Vector3(separation, 0f, 0f);
            b.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetRef(a.GetComponent<PortalTeleporter>(), "destination", b.transform);
            SetRef(b.GetComponent<PortalTeleporter>(), "destination", a.transform);
            return root;
        }


        static GameObject Portal(string name, Color color)
        {
            var p = new GameObject(name);
            var ring = Prim(PrimitiveType.Cylinder, "Aro", p.transform, new Vector3(0f, 1.2f, 0f), new Vector3(1.6f, 0.04f, 2.3f), Mat("Marco portal", new Color(0.2f, 0.2f, 0.25f)), false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var surf = Prim(PrimitiveType.Cylinder, "Superficie", p.transform, new Vector3(0f, 1.2f, 0f), new Vector3(1.4f, 0.01f, 2.1f), UnlitMat(name, color), false);
            surf.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var spin = new GameObject("Remolino");
            spin.transform.SetParent(p.transform, false);
            spin.transform.localPosition = new Vector3(0f, 1.2f, -0.02f);
            Prim(PrimitiveType.Cube, "Brillo", spin.transform, Vector3.zero, new Vector3(1.2f, 0.06f, 0.01f), UnlitMat("Brillo portal", Color.white), false);
            var sp = spin.AddComponent<Spinner>();
            SetVec(sp, "degreesPerSecond", new Vector3(0f, 0f, 120f));
            SetFloat(sp, "bobHeight", 0f);
            var col = p.AddComponent<BoxCollider>();
            col.isTrigger = true; col.center = new Vector3(0f, 1.2f, 0f); col.size = new Vector3(1.3f, 2.1f, 0.4f);
            p.AddComponent<PortalTeleporter>();
            return p;
        }


        static GameObject SpellPrefab(string name, Color color, float radius)
        {
            string path = kGenerated + "/Hechizo " + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var orb = Prim(PrimitiveType.Sphere, name, null, Vector3.zero, Vector3.one * 0.1f, UnlitMat("Hechizo " + name, color), false);
            var rb = orb.AddComponent<Rigidbody>(); rb.useGravity = false;
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(orb.transform, false);
            light.type = LightType.Point; light.range = 2f; light.color = color;
            var p = orb.AddComponent<Projectile>();
            SetFloat(p, "explosionRadius", radius);
            return SavePrefab(orb, path);
        }


        static void AddCaster(Hand hand)
        {
            var origin = OriginOrWarn();
            if (origin == null) return;
            var go = new GameObject("Hechizos (" + (hand == Hand.Left ? "izquierda" : "derecha") + ")");
            go.transform.SetParent(origin.transform, false);
            var c = go.AddComponent<HandCaster>();
            c.Setup(hand, new[]
            {
                new HandCaster.Spell { name = "Bola de fuego", projectile = SpellPrefab("Fuego", new Color(1f, 0.45f, 0.1f), 1.2f).GetComponent<Projectile>(), color = new Color(1f, 0.45f, 0.1f), damage = 20f },
                new HandCaster.Spell { name = "Esquirla de hielo", projectile = SpellPrefab("Hielo", new Color(0.5f, 0.85f, 1f), 0f).GetComponent<Projectile>(), color = new Color(0.5f, 0.85f, 1f), damage = 30f, minSpeed = 15f, maxSpeed = 35f, chargeTime = 0.6f, fullChargeMultiplier = 1.8f },
            });
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            Selection.activeGameObject = go;
        }


        static void AddTelekinesis(Hand hand)
        {
            var origin = OriginOrWarn();
            if (origin == null) return;
            var go = new GameObject("Telequinesis (" + (hand == Hand.Left ? "izquierda" : "derecha") + ")");
            go.transform.SetParent(origin.transform, false);
            var t = go.AddComponent<Telekinesis>();
            SetEnum(t, "hand", (int)hand);
            var beam = go.AddComponent<LineRenderer>();
            beam.widthMultiplier = 0.01f; beam.positionCount = 2; beam.enabled = false; beam.useWorldSpace = true;
            beam.sharedMaterial = UnlitMat("Rayo telequinesis", new Color(0.6f, 0.4f, 1f, 0.6f));
            SetRef(t, "beam", beam);
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            Selection.activeGameObject = go;
        }


        static void AddGrapple(Hand hand)
        {
            var origin = OriginOrWarn();
            if (origin == null) return;
            var go = new GameObject("Gancho (" + (hand == Hand.Left ? "izquierda" : "derecha") + ")");
            go.transform.SetParent(origin.transform, false);
            var g = go.AddComponent<GrappleHook>();
            SetEnum(g, "hand", (int)hand);
            var rope = go.AddComponent<LineRenderer>();
            rope.widthMultiplier = 0.015f; rope.positionCount = 2; rope.enabled = false; rope.useWorldSpace = true;
            rope.sharedMaterial = UnlitMat("Cuerda gancho", new Color(0.95f, 0.95f, 0.9f));
            SetRef(g, "rope", rope);
            var marker = Prim(PrimitiveType.Sphere, "Punto de enganche", go.transform, Vector3.zero, Vector3.one * 0.12f, UnlitMat("Marcador", new Color(0.4f, 1f, 0.6f)), false);
            SetRef(g, "aimMarker", marker.transform);
            if (Object.FindFirstObjectByType<HandCaster>() != null)
                Debug.LogWarning("[VRKit] Ojo: ya hay hechizos en una mano. El gancho y los hechizos usan el gatillo: ponlos en manos distintas.");
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            Selection.activeGameObject = go;
        }


        static void AddTimeController()
        {
            if (Object.FindFirstObjectByType<TimeController>() != null) return;
            var go = new GameObject("Tiempo bala");
            go.AddComponent<TimeController>();
            Undo.RegisterCreatedObjectUndo(go, "VRKit");
            Selection.activeGameObject = go;
        }
    }
}
#endif
