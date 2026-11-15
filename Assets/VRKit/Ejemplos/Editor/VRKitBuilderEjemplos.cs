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
    /// <summary>
    /// Menú GameObject › VRKit › Ejemplos › Montar campo de entrenamiento completo: una escena jugable que usa todos los módulos.
    ///
    ///                         ARENA (oleadas)
    ///      TIRO (arco, pistola)                 COMBATE (espada, escudo, granadas)
    ///                    · inicio · tablón de objetivos ·
    ///      PUZLES (palanca, puerta, caja fuerte)   ALQUIMIA (caldero)
    ///                         MAGIA (telequinesis, portales)
    ///
    /// Necesita TODOS los módulos. Si quitáis alguno, borrad también la carpeta Ejemplos.
    /// </summary>
    public static partial class VRKitBuilder
    {
        const string kEj = kRoot + "Ejemplos/";

        [MenuItem(kEj + "Montar campo de entrenamiento completo", false, 1)]
        static void BuildTrainingGround()
        {
            EnsureFolder();
            var root = new GameObject("Campo de entrenamiento VRKit");

            // ------------------------------------------------------------ Suelo, muros y luz
            var geo = new GameObject("Geometría");
            geo.transform.SetParent(root.transform, false);
            var floorMat = Mat("Suelo campo", new Color(0.36f, 0.42f, 0.36f));
            var wallMat = Mat("Muro campo", new Color(0.55f, 0.52f, 0.48f));
            var floor = Prim(PrimitiveType.Cube, "Suelo", geo.transform, new Vector3(0f, -0.05f, 0f), new Vector3(44f, 0.1f, 44f), floorMat, true);
            AddByName(floor, "UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit",
                             "UnityEngine.XR.Interaction.Toolkit.TeleportationArea, Unity.XR.Interaction.Toolkit");
            Prim(PrimitiveType.Cube, "Muro N", geo.transform, new Vector3(0f, 0.6f, 22f), new Vector3(44f, 1.2f, 0.4f), wallMat, true);
            Prim(PrimitiveType.Cube, "Muro S", geo.transform, new Vector3(0f, 0.6f, -22f), new Vector3(44f, 1.2f, 0.4f), wallMat, true);
            Prim(PrimitiveType.Cube, "Muro E", geo.transform, new Vector3(22f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 44f), wallMat, true);
            Prim(PrimitiveType.Cube, "Muro O", geo.transform, new Vector3(-22f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 44f), wallMat, true);
            Zone(geo, "Zona tiro", new Vector3(-12f, 0f, 6f), new Vector2(9f, 12f), new Color(0.35f, 0.45f, 0.6f));
            Zone(geo, "Zona combate", new Vector3(12f, 0f, 5f), new Vector2(9f, 9f), new Color(0.6f, 0.38f, 0.35f));
            Zone(geo, "Zona puzles", new Vector3(-12f, 0f, -9f), new Vector2(9f, 9f), new Color(0.5f, 0.45f, 0.3f));
            Zone(geo, "Zona alquimia", new Vector3(12f, 0f, -9f), new Vector2(9f, 9f), new Color(0.35f, 0.55f, 0.4f));
            Zone(geo, "Zona magia", new Vector3(0f, 0f, -15f), new Vector2(10f, 7f), new Color(0.45f, 0.38f, 0.6f));
            Zone(geo, "Zona arena", new Vector3(0f, 0f, 15f), new Vector2(14f, 11f), new Color(0.5f, 0.3f, 0.3f));
            BakeNavMeshOn(geo);
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sol").AddComponent<Light>();
                sun.type = LightType.Directional; sun.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.transform.SetParent(root.transform, true);
            }

            // ------------------------------------------------------------ Jugador
            var origin = EnsureXROrigin();
            Empty("Punto de inicio", root.transform, Vector3.zero);

            CreateGameSystems();
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null && gm.GetComponent<LevelProgress>() == null) gm.gameObject.AddComponent<LevelProgress>();
            BuildMenu(false);
            AddTimeController();
            if (origin != null)
            {
                AddCaster(Hand.Right);
                AddTelekinesis(Hand.Left);
                BuildBackpack();
            }

            // ------------------------------------------------------------ Objetivos, subtítulos, tutorial
            var tracker = BuildObjectiveBoard(new[]
            {
                "diana:Acierta 3 veces en las dianas:3",
                "monedas:Encuentra 5 monedas:5",
                "caja_fuerte:Abre la caja fuerte:1",
                "pocion:Prepara una poción en el caldero:1",
                "arena:Supera las oleadas de la arena:1",
            });
            tracker.transform.SetParent(root.transform, false);
            tracker.transform.localPosition = new Vector3(0f, 0f, 3f);

            var subs = EnsureSubtitles();
            subs.transform.SetParent(root.transform, false);
            var intro = new GameObject("Intro (secuencia)");
            intro.transform.SetParent(root.transform, false);
            var seq = intro.AddComponent<EventSequence>();
            SetBool(seq, "playOnStart", true);
            Say(seq, subs, 1.5f, "Instructora: Bienvenido al campo de entrenamiento.");
            Say(seq, subs, 3.5f, "Instructora: Cumple los objetivos del tablón que tienes delante.");
            Say(seq, subs, 4f, "Instructora: Tu mano izquierda mueve objetos a distancia. La derecha, vacía, lanza hechizos.");
            Say(seq, subs, 5f, "Instructora: Y si te agobias... el botón Y ralentiza el tiempo.");
            BuildTutorial().transform.SetParent(root.transform, false);

            // ------------------------------------------------------------ TIRO
            var tiro = Group(root, "Estación de tiro", new Vector3(-12f, 0f, 2f));
            Table(tiro, Vector3.zero, new Vector3(2.4f, 0.8f, 0.8f));
            Sign(tiro, "TIRO", "Coge el arco por el mango y la cuerda con la otra mano.\nO prueba la pistola.", new Vector3(0f, 2f, 0.6f));
            CreateBowSet();
            Place(Recent("Arco"), tiro, new Vector3(-0.6f, 0.95f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Place(Recent("Carcaj"), tiro, new Vector3(0.1f, 1.05f, 0f), Quaternion.identity);
            CreateGun();
            Place(Recent("Pistola"), tiro, new Vector3(0.8f, 0.9f, 0f), Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                CreateTarget();
                var t = Recent("Diana");
                Place(t, tiro, new Vector3(-2f + i * 2f, 1.2f + (i % 2) * 0.6f, 9f), Quaternion.identity);
                SetString(t.GetComponent<Target>(), "objectiveId", "diana");
            }

            // ------------------------------------------------------------ COMBATE
            var combate = Group(root, "Estación de combate", new Vector3(12f, 0f, 2f));
            Table(combate, Vector3.zero, new Vector3(2.4f, 0.8f, 0.8f));
            Sign(combate, "COMBATE", "Espada: golpea con ganas (la fuerza importa).\nEscudo: tápate de los disparos.\nGranadas: gatillo para quitar la anilla.", new Vector3(0f, 2f, 0.6f));
            Place(BuildSword(), combate, new Vector3(-0.6f, 0.95f, 0f), Quaternion.Euler(0f, 0f, 90f));
            Place(BuildShield(), combate, new Vector3(0.3f, 1.05f, 0f), Quaternion.Euler(-60f, 0f, 0f));
            Place(BuildGrenadeCrate(), combate, new Vector3(1.8f, 0f, 0f), Quaternion.identity);
            Place(BuildDummy(), combate, new Vector3(-1.2f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            Place(BuildDummy(), combate, new Vector3(1.2f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));

            // ------------------------------------------------------------ PUZLES
            var puzles = Group(root, "Estación de puzles", new Vector3(-12f, 0f, -8f), 180f);
            Sign(puzles, "PUZLES", "La palanca abre la puerta.\nDetrás está la combinación de la caja fuerte.", new Vector3(0f, 2.3f, -2.5f));
            var door = BuildDoor();
            Place(door, puzles, new Vector3(-1.5f, 0f, 1f), Quaternion.identity);
            var doorC = door.GetComponentInChildren<VRDoor>();
            SetBool(doorC, "locked", false);
            var lever = BuildLever();
            Place(lever, puzles, new Vector3(-3f, 0.9f, -0.5f), Quaternion.identity);
            Prim(PrimitiveType.Cube, "Pedestal palanca", puzles.transform, new Vector3(-3f, 0.45f, -0.5f), new Vector3(0.3f, 0.9f, 0.3f), Mat("Piedra", new Color(0.5f, 0.5f, 0.52f)), true);
            var lv = lever.GetComponent<VRLever>();
            UnityEventTools.AddPersistentListener(lv.onTurnedOn, new UnityAction(doorC.Open));
            UnityEventTools.AddPersistentListener(lv.onTurnedOff, new UnityAction(doorC.Close));
            var hint = new GameObject("Pista");
            hint.transform.SetParent(puzles.transform, false);
            hint.transform.localPosition = new Vector3(-1.5f, 0f, 3f);
            Prim(PrimitiveType.Cube, "Pizarra", hint.transform, new Vector3(0f, 1.4f, 0.05f), new Vector3(1.2f, 0.7f, 0.05f), Mat("Pizarra", new Color(0.12f, 0.18f, 0.15f)), true);
            Label3D(hint.transform, "Combinación:\n<size=150%><b>3 · 7 · 1</b></size>", new Vector3(0f, 1.4f, 0f), 0.8f, Color.white);
            var safe = BuildSafe(new[] { 3, 7, 1 });
            Place(safe, puzles, new Vector3(2.5f, 0f, 0f), Quaternion.identity);
            var lockC = safe.GetComponent<CombinationLock>();
            UnityEventTools.AddStringPersistentListener(lockC.onSolved, new UnityAction<string>(tracker.Report), "caja_fuerte");
            var safeCoin = BuildCoin();
            Place(safeCoin, puzles, new Vector3(2.5f, 0.35f, 0f), Quaternion.identity);
            var dc = BuildCoin(); Place(dc, puzles, new Vector3(-1.5f, 1.2f, 2.2f), Quaternion.identity);
            Place(BuildDrawer(), puzles, new Vector3(1f, 0f, -2.2f), Quaternion.identity);

            // ------------------------------------------------------------ ALQUIMIA
            var alq = Group(root, "Estación de alquimia", new Vector3(12f, 0f, -8f), 180f);
            Place(BuildAlchemy(), alq, Vector3.zero, Quaternion.identity);

            // ------------------------------------------------------------ MAGIA
            var magia = Group(root, "Estación de magia", new Vector3(0f, 0f, -14f), 180f);
            Sign(magia, "MAGIA", "Mano izquierda vacía: apunta a una caja.\nGrip + tirón hacia ti = atraerla. Gatillo = levitarla.\nMano derecha vacía: mantén el gatillo para cargar un hechizo. A/X lo cambia.", new Vector3(0f, 2.4f, -3.2f));
            for (int i = 0; i < 5; i++)
            {
                var box = Prim(PrimitiveType.Cube, "Caja mágica", magia.transform, new Vector3(-4f + i * 2f, 1.3f, 0f), Vector3.one * 0.35f, Mat("Caja mágica", new Color(0.55f, 0.4f, 0.8f)), true);
                box.AddComponent<Rigidbody>().mass = 2f;
                box.AddComponent<XRGrabInteractable>();
                box.AddComponent<GrabHighlight>();
                Prim(PrimitiveType.Cylinder, "Pedestal", magia.transform, new Vector3(-4f + i * 2f, 0.55f, 0f), new Vector3(0.4f, 0.55f, 0.4f), Mat("Piedra", new Color(0.5f, 0.5f, 0.52f)), true);
            }
            var portals = BuildPortals(14f);
            Place(portals, magia, new Vector3(-7f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
            var mc = BuildCoin(); Place(mc, magia, new Vector3(3f, 1.3f, 2f), Quaternion.identity);

            // ------------------------------------------------------------ ARENA
            var arena = BuildArena();
            Place(arena, root, new Vector3(0f, 0f, 13f), Quaternion.identity);
            var spawner = arena.GetComponent<WaveSpawner>();
            UnityEventTools.AddStringPersistentListener(spawner.onAllWavesCleared, new UnityAction<string>(tracker.Report), "arena");
            Sign(arena, "ARENA", "Pulsa el botón y sobrevive a 3 oleadas.", new Vector3(0f, 2.2f, -4.2f));

            // ------------------------------------------------------------ Monedas sueltas
            Place(BuildCoin(), root, new Vector3(-4f, 1.3f, 5f), Quaternion.identity);
            Place(BuildCoin(), root, new Vector3(18f, 1.1f, 14f), Quaternion.identity);
            Place(BuildCoin(), root, new Vector3(-18f, 1.1f, -18f), Quaternion.identity);

            Undo.RegisterCreatedObjectUndo(root, "VRKit");
            Selection.activeGameObject = root;
            Debug.Log("[VRKit] Campo de entrenamiento montado. Guarda la escena y pulsa Play con las gafas conectadas (Quest Link) o con el XR Device Simulator.");
        }
    }
}
#endif
