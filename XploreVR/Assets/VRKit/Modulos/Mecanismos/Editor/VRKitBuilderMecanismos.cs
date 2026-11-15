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
    /// <summary>Menú GameObject › VRKit › Mecanismos: palanca, rueda, cajón, puerta y caja fuerte.</summary>
    public static partial class VRKitBuilder
    {
        const string kMec = kRoot + "Mecanismos/";


        // ================================================================== Menús

        [MenuItem(kMec + "Palanca", false, 40)] static void MenuLever() { Finish(BuildLever(), 0.6f, 0.8f); }

        [MenuItem(kMec + "Rueda o dial", false, 41)] static void MenuDial() { Finish(BuildDial(), 0.5f, 0.9f); }

        [MenuItem(kMec + "Cajón", false, 42)] static void MenuDrawer() { Finish(BuildDrawer(), 0.6f, 0f); }

        [MenuItem(kMec + "Puerta", false, 43)] static void MenuDoor() { Finish(BuildDoor(), 2.5f, 0f); }

        [MenuItem(kMec + "Caja fuerte con combinación", false, 44)] static void MenuSafe() { Finish(BuildSafe(new[] { 3, 7, 1 }), 0.7f, 0f); }


        // ================================================================== Interacción

        static GameObject BuildLever()
        {
            var root = new GameObject("Palanca");
            var dark = Mat("Metal oscuro", new Color(0.15f, 0.15f, 0.17f));
            Prim(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.03f, 0f), new Vector3(0.18f, 0.06f, 0.22f), dark, true);
            var handle = Empty("Handle", root.transform, new Vector3(0f, 0.06f, 0f));
            Prim(PrimitiveType.Cylinder, "Barra", handle, new Vector3(0f, 0.13f, 0f), new Vector3(0.025f, 0.13f, 0.025f), Mat("Metal claro", new Color(0.7f, 0.7f, 0.72f)), false);
            var knob = Prim(PrimitiveType.Sphere, "Pomo", handle, new Vector3(0f, 0.27f, 0f), Vector3.one * 0.06f, Mat("Rojo botón", new Color(0.85f, 0.15f, 0.15f)), true);
            var si = knob.AddComponent<XRSimpleInteractable>();
            knob.AddComponent<GrabHighlight>();
            var lever = root.AddComponent<VRLever>();
            SetRef(lever, "handle", handle);
            SetRef(lever, "interactable", si);
            return root;
        }


        static GameObject BuildDial()
        {
            var root = new GameObject("Rueda");
            var knobT = Empty("Knob", root.transform, Vector3.zero);
            var body = Prim(PrimitiveType.Cylinder, "Pomo", knobT, Vector3.zero, new Vector3(0.1f, 0.02f, 0.1f), Mat("Metal claro", new Color(0.7f, 0.7f, 0.72f)), true);
            Prim(PrimitiveType.Cube, "Marca", knobT, new Vector3(0f, 0.022f, 0.035f), new Vector3(0.01f, 0.006f, 0.03f), UnlitMat("Marca", new Color(1f, 0.85f, 0.3f)), false);
            var si = knobT.gameObject.AddComponent<XRSimpleInteractable>();
            body.AddComponent<GrabHighlight>();
            var dial = root.AddComponent<VRDial>();
            SetRef(dial, "knob", knobT);
            SetRef(dial, "interactable", si);
            return root;
        }


        static GameObject BuildDrawer()
        {
            var root = new GameObject("Mueble con cajón");
            var wood = Mat("Madera", new Color(0.45f, 0.28f, 0.14f));
            Prim(PrimitiveType.Cube, "Mueble", root.transform, new Vector3(0f, 0.45f, 0.25f), new Vector3(0.6f, 0.9f, 0.5f), wood, true);
            var slide = Empty("Cajón", root.transform, new Vector3(0f, 0.7f, -0.02f));
            Prim(PrimitiveType.Cube, "Frente", slide, Vector3.zero, new Vector3(0.5f, 0.18f, 0.03f), Mat("Madera clara", new Color(0.6f, 0.42f, 0.25f)), true);
            var handle = Prim(PrimitiveType.Cube, "Tirador", slide, new Vector3(0f, 0f, -0.035f), new Vector3(0.14f, 0.03f, 0.03f), Mat("Metal claro", new Color(0.7f, 0.7f, 0.72f)), true);
            var si = handle.AddComponent<XRSimpleInteractable>();
            handle.AddComponent<GrabHighlight>();
            var s = root.AddComponent<VRSlider>();
            SetRef(s, "slide", slide);
            SetRef(s, "interactable", si);
            SetFloat(s, "length", -0.35f); // hacia el jugador (eje -Z)
            return root;
        }


        static GameObject BuildDoor()
        {
            var root = new GameObject("Puerta");
            var stone = Mat("Piedra", new Color(0.5f, 0.5f, 0.52f));
            Prim(PrimitiveType.Cube, "Marco izquierdo", root.transform, new Vector3(-0.65f, 1.15f, 0f), new Vector3(0.2f, 2.3f, 0.25f), stone, true);
            Prim(PrimitiveType.Cube, "Marco derecho", root.transform, new Vector3(0.65f, 1.15f, 0f), new Vector3(0.2f, 2.3f, 0.25f), stone, true);
            Prim(PrimitiveType.Cube, "Dintel", root.transform, new Vector3(0f, 2.4f, 0f), new Vector3(1.5f, 0.2f, 0.25f), stone, true);
            var pivot = Empty("Bisagra", root.transform, new Vector3(-0.55f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Hoja", pivot, new Vector3(0.55f, 1.1f, 0f), new Vector3(1.08f, 2.2f, 0.08f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), true);
            var door = pivot.gameObject.AddComponent<VRDoor>();
            SetBool(door, "locked", false);
            return root;
        }


        static GameObject BuildSafe(int[] solution)
        {
            var root = new GameObject("Caja fuerte");
            var steel = Mat("Acero", new Color(0.35f, 0.38f, 0.42f));
            Prim(PrimitiveType.Cube, "Fondo", root.transform, new Vector3(0f, 0.45f, 0.22f), new Vector3(0.7f, 0.9f, 0.05f), steel, true);
            Prim(PrimitiveType.Cube, "Lado izq.", root.transform, new Vector3(-0.33f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f), steel, true);
            Prim(PrimitiveType.Cube, "Lado der.", root.transform, new Vector3(0.33f, 0.45f, 0f), new Vector3(0.05f, 0.9f, 0.45f), steel, true);
            Prim(PrimitiveType.Cube, "Techo", root.transform, new Vector3(0f, 0.88f, 0f), new Vector3(0.7f, 0.05f, 0.45f), steel, true);
            Prim(PrimitiveType.Cube, "Suelo", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.05f, 0.45f), steel, true);
            var pivot = Empty("Bisagra", root.transform, new Vector3(-0.3f, 0f, -0.24f));
            Prim(PrimitiveType.Cube, "Puerta", pivot, new Vector3(0.3f, 0.45f, 0f), new Vector3(0.6f, 0.82f, 0.04f), Mat("Acero oscuro", new Color(0.22f, 0.24f, 0.28f)), true);
            var door = pivot.gameObject.AddComponent<VRDoor>();
            SetFloat(door, "openAngle", 110f); // gira hacia el jugador (eje -Z)
            SetBool(door, "locked", true);

            var dials = new List<VRDial>();
            for (int i = 0; i < solution.Length; i++)
            {
                var d = BuildDial();
                d.name = "Dial " + (i + 1);
                d.transform.SetParent(pivot, false);
                d.transform.localPosition = new Vector3(0.12f + i * 0.18f, 0.55f, -0.03f);
                d.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // el eje del dial mira al jugador
                var dial = d.GetComponent<VRDial>();
                SetBool(dial, "limited", false);
                SetInt(dial, "steps", 10);
                dials.Add(dial);
                var num = Label3D(pivot, "0-9", new Vector3(0.12f + i * 0.18f, 0.43f, -0.03f), 0.35f, Color.white);
                num.transform.localRotation = Quaternion.identity;
            }
            var lockC = root.AddComponent<CombinationLock>();
            lockC.Setup(dials.ToArray(), solution);
            UnityEventTools.AddPersistentListener(lockC.onSolved, new UnityAction(door.UnlockAndOpen));
            return root;
        }
    }
}
#endif
