using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Las cuevas por dentro. El terreno es una malla de alturas y no puede tener agujeros, así que cada cueva es un
    /// interior aparte, generado con la semilla (siempre igual para la misma cueva) y colocado muy por debajo del suelo.
    /// Entrar por la boca = fundido a negro y apareces dentro; volver al principio del túnel = sales.
    /// Dentro: oscuro, con cristales que brillan y una luz que lleva el jugador.
    /// </summary>
    public class CaveSystem : MonoBehaviour
    {
        public static CaveSystem Instance { get; private set; }

        [SerializeField] Material wallMaterial;
        [SerializeField] Material crystalMaterial;
        [Tooltip("Metros por debajo del agua donde se colocan los interiores.")]
        [SerializeField] float depth = 600f;
        [SerializeField, Range(6, 14)] int tunnelSegments = 9;
        [Header("Luz del jugador dentro")]
        [SerializeField] bool lantern = true;
        [SerializeField] Color lanternColor = new Color(1f, 0.82f, 0.6f);
        [SerializeField] float lanternRange = 14f;
        [SerializeField] float lanternIntensity = 1.4f;
        [Header("Sonido (opcional)")]
        [SerializeField] AudioClip enterSound;
        [SerializeField] AudioClip caveAmbience;

        public UnityEvent<string> onEnterCave = new UnityEvent<string>();
        public UnityEvent<string> onExitCave = new UnityEvent<string>();
        public UnityEvent<string> onDiscoverCave = new UnityEvent<string>();

        static readonly List<CaveEntrance> s_Entrances = new List<CaveEntrance>();
        internal static void Register(CaveEntrance e) { if (!s_Entrances.Contains(e)) s_Entrances.Add(e); }
        internal static void Unregister(CaveEntrance e) { s_Entrances.Remove(e); }

        GameObject m_Interior;
        Light m_Lantern;
        AudioSource m_Ambience;
        CaveSite m_Site;
        Vector3 m_Outside, m_Exit, m_Start;
        float m_OutsideYaw;
        bool m_Busy;

        public bool InCave { get; private set; }
        public string CurrentCave { get { return InCave ? m_Site.name : null; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var player = VRPlayer.Instance;
            if (player == null || m_Busy) return;
            var feet = player.FeetPosition;
            if (!InCave)
            {
                for (int i = s_Entrances.Count - 1; i >= 0; i--)
                    if (s_Entrances[i] != null) s_Entrances[i].Check(feet, this);
                return;
            }
            if (m_Lantern != null && player.Head != null) m_Lantern.transform.position = player.Head.position;
            // Volver a la boca = salir. Caerse fuera de la cueva = volver al principio.
            if (Vector3.Distance(feet, m_Exit) < 1.8f) StartCoroutine(Exit());
            else if (feet.y < m_Start.y - 25f) player.TeleportTo(m_Start);
        }

        internal void DiscoverEntrance(CaveEntrance e)
        {
            var w = OpenWorld.Instance;
            if (w != null && w.Discover("cueva:" + e.Site.id))
            {
                SubtitleSystem.SayStatic("", "Has encontrado: " + e.DisplayName);
                onDiscoverCave.Invoke(e.DisplayName);
            }
        }

        public void Enter(CaveEntrance e) { if (!InCave && !m_Busy) StartCoroutine(DoEnter(e)); }

        IEnumerator DoEnter(CaveEntrance e)
        {
            m_Busy = true;
            var player = VRPlayer.Instance;
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeTo(1f, 0.5f);
            if (enterSound != null) VRAudio.Play2D(enterSound, 0.7f);

            m_Site = e.Site;
            m_Outside = e.OutsidePosition;
            m_OutsideYaw = e.transform.eulerAngles.y;
            float water = OpenWorld.Instance != null ? OpenWorld.Instance.WaterLevel : 0f;
            var origin = new Vector3(m_Site.x, water - depth, m_Site.z);
            var rot = Quaternion.Euler(0f, m_Site.yaw + 180f, 0f);   // el túnel va hacia dentro de la montaña

            var interior = CaveGen.BuildInterior(m_Site.id, OpenWorld.Instance != null ? OpenWorld.Instance.Seed : 0, tunnelSegments);
            BuildInterior(interior, origin, rot);
            m_Start = origin + rot * interior.start;
            m_Exit = origin + rot * interior.floorPoints[0];
            Vector3 fwd = rot * interior.startForward;

            InCave = true;
            if (OpenWorld.Instance != null) OpenWorld.Instance.PlayerUnderground = true;
            if (SkyCycle.Instance != null) SkyCycle.Instance.InCave = true;
            if (player != null) player.TeleportTo(m_Start + fwd * 2f, Quaternion.LookRotation(fwd).eulerAngles.y);
            if (lantern) EnsureLantern(true);
            if (caveAmbience != null) PlayAmbience(true);

            var w = OpenWorld.Instance;
            if (w != null && w.Discover("dentro:" + m_Site.id)) SubtitleSystem.SayStatic("", "Entras en la " + m_Site.name.ToLower());
            onEnterCave.Invoke(m_Site.name);
            yield return new WaitForSeconds(0.2f);
            if (fader != null) yield return fader.FadeTo(0f, 0.6f);
            m_Busy = false;
        }

        IEnumerator Exit()
        {
            m_Busy = true;
            var fader = ScreenFader.Instance;
            if (fader != null) yield return fader.FadeTo(1f, 0.5f);
            InCave = false;
            if (OpenWorld.Instance != null) OpenWorld.Instance.PlayerUnderground = false;
            if (SkyCycle.Instance != null) SkyCycle.Instance.InCave = false;
            var player = VRPlayer.Instance;
            float ground = OpenWorld.Instance != null ? OpenWorld.Instance.GroundHeight(m_Outside.x, m_Outside.z) : m_Outside.y;
            if (player != null) player.TeleportTo(new Vector3(m_Outside.x, ground + 0.05f, m_Outside.z), m_OutsideYaw);
            if (m_Interior != null) Destroy(m_Interior);
            EnsureLantern(false);
            PlayAmbience(false);
            onExitCave.Invoke(m_Site.name);
            yield return new WaitForSeconds(0.2f);
            if (fader != null) yield return fader.FadeTo(0f, 0.6f);
            m_Busy = false;
        }

        void BuildInterior(CaveGen.Interior it, Vector3 origin, Quaternion rot)
        {
            if (m_Interior != null) Destroy(m_Interior);
            m_Interior = new GameObject("Interior de " + m_Site.name);
            m_Interior.transform.SetPositionAndRotation(origin, rot);

            var mesh = new Mesh { name = "Cueva" };
            mesh.indexFormat = it.mesh.vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(it.mesh.vertices);
            mesh.SetNormals(it.mesh.normals);
            mesh.SetColors(it.mesh.colors);
            mesh.SetTriangles(it.mesh.triangles, 0);
            mesh.RecalculateBounds();
            m_Interior.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = m_Interior.AddComponent<MeshRenderer>();
            r.sharedMaterial = wallMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Interior.AddComponent<MeshCollider>().sharedMesh = mesh;
            m_Interior.AddComponent<DestroyMeshOnDestroy>();

            // Cristales en las salas y unos pocos por el túnel
            var cm = new LowPolyMeshData();
            int k = 0;
            foreach (var c in it.chambers)
            {
                for (int i = 0; i < 7; i++)
                {
                    float a = i / 7f * Mathf.PI * 2f + k;
                    var p = c + new Vector3(Mathf.Cos(a) * 4.5f, 0f, Mathf.Sin(a) * 4.5f);
                    LowPolyShapes.Cylinder(cm, p, 0.25f + (i % 3) * 0.1f, 0f, 0.8f + (i % 4) * 0.5f, 5, Hue(k + i));
                }
                k++;
            }
            for (int i = 3; i < it.floorPoints.Count - 1; i += 4)
                LowPolyShapes.Cylinder(cm, it.floorPoints[i] + new Vector3(1.4f, 0f, 0f), 0.18f, 0f, 0.7f, 5, Hue(i));
            if (cm.vertices.Count > 0)
            {
                var go = new GameObject("Cristales");
                go.transform.SetParent(m_Interior.transform, false);
                var cmesh = new Mesh { name = "Cristales" };
                cmesh.SetVertices(cm.vertices); cmesh.SetNormals(cm.normals); cmesh.SetColors(cm.colors); cmesh.SetTriangles(cm.triangles, 0);
                go.AddComponent<MeshFilter>().sharedMesh = cmesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = crystalMaterial != null ? crystalMaterial : wallMaterial;
                go.AddComponent<DestroyMeshOnDestroy>();
            }

            // Salida: un resplandor en la boca del túnel
            var exit = new LowPolyMeshData();
            LowPolyShapes.Blob(exit, it.floorPoints[0] + new Vector3(0f, 1.3f, 0f), new Vector3(1.1f, 1.3f, 0.2f), 0.1f, 3, new Color32(255, 240, 200, 255), 0f);
            var ex = new GameObject("Salida");
            ex.transform.SetParent(m_Interior.transform, false);
            var emesh = new Mesh { name = "Salida" };
            emesh.SetVertices(exit.vertices); emesh.SetNormals(exit.normals); emesh.SetColors(exit.colors); emesh.SetTriangles(exit.triangles, 0);
            ex.AddComponent<MeshFilter>().sharedMesh = emesh;
            ex.AddComponent<MeshRenderer>().sharedMaterial = crystalMaterial != null ? crystalMaterial : wallMaterial;
            ex.AddComponent<DestroyMeshOnDestroy>();
        }

        static Color32 Hue(int i)
        {
            Color c = Color.HSVToRGB(Mathf.Repeat(0.5f + i * 0.07f, 1f), 0.55f, 1f);
            return (Color32)c;
        }

        void EnsureLantern(bool on)
        {
            if (on && m_Lantern == null)
            {
                m_Lantern = new GameObject("Luz del jugador").AddComponent<Light>();
                m_Lantern.type = LightType.Point;
                m_Lantern.color = lanternColor;
                m_Lantern.range = lanternRange;
                m_Lantern.intensity = lanternIntensity;
                m_Lantern.shadows = LightShadows.None;
            }
            if (m_Lantern != null) m_Lantern.gameObject.SetActive(on);
        }

        void PlayAmbience(bool on)
        {
            if (caveAmbience == null) return;
            if (m_Ambience == null)
            {
                m_Ambience = gameObject.AddComponent<AudioSource>();
                m_Ambience.loop = true;
                m_Ambience.spatialBlend = 0f;
                m_Ambience.volume = 0.6f;
                m_Ambience.clip = caveAmbience;
            }
            if (on) m_Ambience.Play(); else m_Ambience.Stop();
        }
    }
}
