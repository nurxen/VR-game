using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// La boca de una cueva en una ladera. La coloca el OpenWorld donde la semilla dice que hay cueva.
    /// Al acercarte se descubre (con su nombre); al entrar por la boca, CaveSystem te lleva dentro.
    /// La boca mira hacia +Z del objeto (cuesta abajo).
    /// </summary>
    public class CaveEntrance : MonoBehaviour
    {
        [Tooltip("Punto de la boca por el que se entra (local).")]
        [SerializeField] Vector3 mouth = new Vector3(0f, 0.2f, 0.6f);
        [Tooltip("Distancia a la boca para entrar (metros).")]
        [SerializeField] float enterRadius = 1.3f;
        [Tooltip("Distancia para «descubrirla» (metros).")]
        [SerializeField] float discoverRadius = 14f;

        public CaveSite Site { get; internal set; }
        public string DisplayName { get { return string.IsNullOrEmpty(Site.name) ? "Cueva" : Site.name; } }
        public Vector3 MouthPosition { get { return transform.TransformPoint(mouth); } }
        /// <summary>Delante de la boca, fuera: donde se vuelve al salir.</summary>
        public Vector3 OutsidePosition { get { return transform.TransformPoint(mouth + new Vector3(0f, 0f, 2.5f)); } }

        float m_Cooldown;

        void OnEnable() { CaveSystem.Register(this); m_Cooldown = Time.time + 1f; }
        void OnDisable() { CaveSystem.Unregister(this); }

        internal void Check(Vector3 feet, CaveSystem system)
        {
            float d = Vector3.Distance(feet, MouthPosition);
            if (d < discoverRadius) system.DiscoverEntrance(this);
            if (d < enterRadius && Time.time > m_Cooldown) { m_Cooldown = Time.time + 3f; system.Enter(this); }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(MouthPosition, enterRadius);
        }
    }
}
