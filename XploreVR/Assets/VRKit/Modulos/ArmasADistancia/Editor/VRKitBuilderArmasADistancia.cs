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
    /// <summary>Menú GameObject › VRKit › Armas a distancia: diana, arco con flechas y carcaj, pistola y varita.</summary>
    public static partial class VRKitBuilder
    {
        const string kArmas = kRoot + "Armas a distancia/";


        [MenuItem(kArmas + "Diana", false, 11)]
        static void CreateTarget()
        {
            var root = new GameObject("Diana");
            var face = Prim(PrimitiveType.Cylinder, "Cara", root.transform, Vector3.zero, new Vector3(1f, 0.03f, 1f), Mat("Diana Blanco", Color.white), true);
            face.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var ring = Prim(PrimitiveType.Cylinder, "Anillo", root.transform, new Vector3(0f, 0f, -0.02f), new Vector3(0.6f, 0.01f, 0.6f), Mat("Diana Rojo", new Color(0.85f, 0.15f, 0.15f)), false);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var bull = Prim(PrimitiveType.Cylinder, "Centro", root.transform, new Vector3(0f, 0f, -0.03f), new Vector3(0.2f, 0.01f, 0.2f), Mat("Diana Amarillo", new Color(1f, 0.8f, 0.1f)), false);
            bull.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var target = root.AddComponent<Target>();
            SetFloat(target, "radius", 0.5f);
            Finish(root, 4f, 1.5f);
        }


        [MenuItem(kArmas + "Arco con flechas y carcaj", false, 12)]
        static void CreateBowSet()
        {
            var arrowPrefab = CreateArrowPrefab();

            // ---- Arco ----
            var bow = new GameObject("Arco");
            var wood = Mat("Madera", new Color(0.45f, 0.28f, 0.14f));
            var handle = Prim(PrimitiveType.Capsule, "Mango", bow.transform, Vector3.zero, new Vector3(0.04f, 0.08f, 0.04f), wood, true);
            var top = Prim(PrimitiveType.Cube, "Pala superior", bow.transform, new Vector3(0f, 0.3f, -0.05f), new Vector3(0.025f, 0.45f, 0.02f), wood, false);
            top.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            var bottom = Prim(PrimitiveType.Cube, "Pala inferior", bow.transform, new Vector3(0f, -0.3f, -0.05f), new Vector3(0.025f, 0.45f, 0.02f), wood, false);
            bottom.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);

            var stringTop = Empty("String Top", bow.transform, new Vector3(0f, 0.5f, -0.12f));
            var stringBottom = Empty("String Bottom", bow.transform, new Vector3(0f, -0.5f, -0.12f));
            var nock = Empty("Nock Rest Point", bow.transform, new Vector3(0f, 0f, -0.12f));
            var grip = Empty("Grip Attach", bow.transform, Vector3.zero);

            var rb = bow.AddComponent<Rigidbody>();
            rb.mass = 1f;
            var bowGrab = bow.AddComponent<XRGrabInteractable>();
            bowGrab.attachTransform = grip;
            bowGrab.colliders.Clear();
            bowGrab.colliders.Add(handle.GetComponent<Collider>()); // solo el mango coge el arco

            var str = Empty("Cuerda", bow.transform, new Vector3(0f, 0f, -0.12f)).gameObject;
            var strCol = str.AddComponent<BoxCollider>();
            strCol.isTrigger = true;
            strCol.size = new Vector3(0.08f, 0.25f, 0.12f);
            var strInteractable = str.AddComponent<XRSimpleInteractable>();
            strInteractable.colliders.Clear();
            strInteractable.colliders.Add(strCol);
            var line = str.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.004f;
            line.sharedMaterial = UnlitMat("Cuerda", new Color(0.9f, 0.9f, 0.85f));
            line.positionCount = 3;
            line.SetPositions(new[] { stringTop.position, nock.position, stringBottom.position });
            var audio = str.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var bowString = str.AddComponent<BowString>();
            SetRef(bowString, "bow", bowGrab);
            SetRef(bowString, "nockRestPoint", nock);
            SetRef(bowString, "stringTop", stringTop);
            SetRef(bowString, "stringBottom", stringBottom);
            SetRef(bowString, "stringLine", line);
            SetRef(bowString, "autoArrowPrefab", arrowPrefab.GetComponent<Arrow>());
            SetRef(bowString, "audioSource", audio);
            bow.AddComponent<ReturnToSpawn>();
            Finish(bow, 0.6f, 1.1f);

            // ---- Carcaj ----
            var quiver = Prim(PrimitiveType.Cylinder, "Carcaj", null, Vector3.zero, new Vector3(0.12f, 0.25f, 0.12f), Mat("Cuero", new Color(0.35f, 0.2f, 0.1f)), true);
            quiver.AddComponent<XRSimpleInteractable>();
            var spawner = quiver.AddComponent<SpawnOnGrab>();
            SetRef(spawner, "prefab", arrowPrefab.GetComponent<XRGrabInteractable>());
            Finish(quiver, 0.6f, 1.0f, 0.4f);

            Debug.Log("[VRKit] Arco creado. Coge el arco por el mango con una mano y la cuerda con la otra. " +
                      "Por defecto aparece una flecha sola al coger la cuerda (Auto Arrow Prefab); vacía ese campo para cargar solo desde el carcaj.");
        }


        static GameObject CreateArrowPrefab()
        {
            string path = kGenerated + "/Flecha.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var arrow = new GameObject("Flecha");
            var shaft = Prim(PrimitiveType.Cylinder, "Astil", arrow.transform, new Vector3(0f, 0f, 0.35f), new Vector3(0.008f, 0.35f, 0.008f), Mat("Madera clara", new Color(0.8f, 0.65f, 0.4f)), false);
            shaft.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var head = Prim(PrimitiveType.Cube, "Punta", arrow.transform, new Vector3(0f, 0f, 0.7f), new Vector3(0.015f, 0.015f, 0.04f), Mat("Metal", new Color(0.6f, 0.6f, 0.65f)), false);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var fletch = Prim(PrimitiveType.Cube, "Plumas", arrow.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.002f, 0.03f, 0.08f), Mat("Plumas", new Color(0.9f, 0.2f, 0.2f)), false);
            fletch.name = "Plumas";
            var tip = Empty("Tip", arrow.transform, new Vector3(0f, 0f, 0.72f));
            var attach = Empty("Hold Attach", arrow.transform, new Vector3(0f, 0f, 0.15f));

            var col = arrow.AddComponent<CapsuleCollider>();
            col.direction = 2;
            col.center = new Vector3(0f, 0f, 0.36f);
            col.height = 0.72f;
            col.radius = 0.012f;
            var rb = arrow.AddComponent<Rigidbody>();
            rb.mass = 0.05f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var grab = arrow.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;

            var trail = arrow.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.widthMultiplier = 0.01f;
            trail.sharedMaterial = UnlitMat("Estela", new Color(1f, 1f, 1f, 0.6f));
            trail.emitting = false;

            var a = arrow.AddComponent<Arrow>();
            SetRef(a, "tip", tip);
            SetRef(a, "trail", trail);
            return SavePrefab(arrow, path);
        }


        [MenuItem(kArmas + "Pistola (impacto instantáneo)", false, 13)]
        static void CreateGun()
        {
            var gun = new GameObject("Pistola");
            var dark = Mat("Metal oscuro", new Color(0.15f, 0.15f, 0.17f));
            Prim(PrimitiveType.Cube, "Cuerpo", gun.transform, new Vector3(0f, 0f, 0.05f), new Vector3(0.035f, 0.05f, 0.18f), dark, true);
            Prim(PrimitiveType.Cube, "Empuñadura", gun.transform, new Vector3(0f, -0.06f, -0.01f), new Vector3(0.03f, 0.1f, 0.04f), dark, true)
                .transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            var muzzle = Empty("Muzzle", gun.transform, new Vector3(0f, 0f, 0.15f));
            var attach = Empty("Grip Attach", gun.transform, new Vector3(0f, -0.05f, -0.01f));

            gun.AddComponent<Rigidbody>();
            var grab = gun.AddComponent<XRGrabInteractable>();
            grab.attachTransform = attach;

            var tracer = muzzle.gameObject.AddComponent<LineRenderer>();
            tracer.widthMultiplier = 0.006f;
            tracer.sharedMaterial = UnlitMat("Trazo", new Color(1f, 0.85f, 0.4f));
            tracer.enabled = false;
            var audio = gun.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var launcher = gun.AddComponent<ProjectileLauncher>();
            SetEnum(launcher, "mode", (int)ProjectileLauncher.FireMode.Raycast);
            SetRef(launcher, "muzzle", muzzle);
            SetRef(launcher, "tracer", tracer);
            SetRef(launcher, "audioSource", audio);
            Finish(gun, 0.5f, 1.1f, -0.3f);
        }


        [MenuItem(kArmas + "Varita mágica (proyectil)", false, 14)]
        static void CreateWand()
        {
            var orb = CreateOrbPrefab();
            var wand = new GameObject("Varita");
            Prim(PrimitiveType.Cylinder, "Vara", wand.transform, new Vector3(0f, 0f, 0.12f), new Vector3(0.015f, 0.15f, 0.015f), Mat("Madera", new Color(0.45f, 0.28f, 0.14f)), true)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Gema", wand.transform, new Vector3(0f, 0f, 0.28f), Vector3.one * 0.03f, Mat("Gema", new Color(0.5f, 0.3f, 1f)), false);
            var muzzle = Empty("Muzzle", wand.transform, new Vector3(0f, 0f, 0.3f));
            wand.AddComponent<Rigidbody>();
            wand.AddComponent<XRGrabInteractable>();
            var launcher = wand.AddComponent<ProjectileLauncher>();
            SetEnum(launcher, "mode", (int)ProjectileLauncher.FireMode.Projectile);
            SetRef(launcher, "muzzle", muzzle);
            SetRef(launcher, "projectilePrefab", orb.GetComponent<Projectile>());
            SetFloat(launcher, "fireRate", 2f);
            SetFloat(launcher, "projectileSpeed", 12f);
            Finish(wand, 0.5f, 1.1f, 0.3f);
        }


        static GameObject CreateOrbPrefab()
        {
            string path = kGenerated + "/Orbe magico.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var orb = Prim(PrimitiveType.Sphere, "Orbe magico", null, Vector3.zero, Vector3.one * 0.08f, UnlitMat("Orbe", new Color(0.6f, 0.4f, 1f)), false);
            var rb = orb.AddComponent<Rigidbody>();
            rb.useGravity = false;
            var light = new GameObject("Luz").AddComponent<Light>();
            light.transform.SetParent(orb.transform, false);
            light.type = LightType.Point;
            light.range = 2f;
            light.color = new Color(0.6f, 0.4f, 1f);
            var p = orb.AddComponent<Projectile>();
            SetFloat(p, "explosionRadius", 1.5f);
            return SavePrefab(orb, path);
        }
    }
}
#endif
