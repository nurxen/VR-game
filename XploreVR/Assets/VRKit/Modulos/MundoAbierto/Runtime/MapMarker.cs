using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Pone este objeto en el mapa. Para pueblos, ruinas, personajes, tesoros… que coloquéis o generéis.
    /// Al acercarte a menos de Discover Radius se «descubre»: aviso con su nombre y queda en el mapa para siempre
    /// (aunque el objeto se borre al alejarte). Con Always Known sale en el mapa desde el principio.
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        [SerializeField] MapIcon icon = MapIcon.Lugar;
        [SerializeField] string displayName = "";
        [Tooltip("Identificador único. Vacío = sale de la posición (vale si el objeto siempre aparece en el mismo sitio).")]
        [SerializeField] string id = "";
        [Tooltip("A cuántos metros se descubre.")]
        [SerializeField] float discoverRadius = 18f;
        [Tooltip("Sale en el mapa desde el principio (sin descubrirlo).")]
        [SerializeField] bool alwaysKnown = false;
        [Tooltip("Si aún no se ha descubierto, el mapa da una pista («?») cuando estás cerca.")]
        [SerializeField] bool hint = false;
        [Tooltip("Avisar con un subtítulo al descubrirlo.")]
        [SerializeField] bool announce = true;

        internal static readonly List<MapMarker> All = new List<MapMarker>();

        public MapIcon Icon { get { return icon; } set { icon = value; } }
        public string DisplayName { get { return displayName; } set { displayName = value; } }
        public float DiscoverRadius { get { return discoverRadius; } }
        public bool AlwaysKnown { get { return alwaysKnown; } }
        public bool Hint { get { return hint; } }
        public bool Announce { get { return announce; } }

        /// <summary>El id que se guarda («marca:…»).</summary>
        public string Id
        {
            get
            {
                if (!string.IsNullOrEmpty(id)) return "marca:" + id;
                var p = transform.position;
                return "marca:" + Mathf.RoundToInt(p.x / 4f) + "_" + Mathf.RoundToInt(p.z / 4f);
            }
            set { id = value; }
        }

        public MapFeature ToFeature()
        {
            var p = transform.position;
            return new MapFeature { id = Id, name = displayName, x = p.x, z = p.z, icon = icon, hint = hint, discoverRadius = discoverRadius };
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }
}
