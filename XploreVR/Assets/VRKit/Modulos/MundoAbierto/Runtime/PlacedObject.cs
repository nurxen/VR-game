using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Un objeto que el jugador ha puesto en el mundo (una tienda, una hoguera, un cofre…). Se guarda en la zona
    /// donde está: al alejarte desaparece y al volver aparece donde lo dejaste.
    /// No se pone a mano: usad OpenWorld.Instance.Place(prefab, posición, giro). El prefab tiene que estar en la
    /// lista «Placeable Prefabs» del OpenWorld (se guarda por su nombre).
    /// </summary>
    public class PlacedObject : MonoBehaviour
    {
        [Tooltip("Nombre del prefab en la lista Placeable Prefabs. Lo pone OpenWorld.Place.")]
        public string prefabKey;

        void OnEnable() { if (OpenWorld.Instance != null) OpenWorld.Instance.RegisterPlaced(this); }
        void OnDisable() { if (OpenWorld.Instance != null) OpenWorld.Instance.UnregisterPlaced(this); }

        /// <summary>Quitarlo del mundo (y de la partida guardada).</summary>
        public void Remove() { Destroy(gameObject); }
    }
}
