using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Va solo en cada árbol, roca o planta que pone el mundo. Sabe en qué chunk está y su número fijo, así
    /// que si lo quitáis (talar un árbol, recoger una seta) se guarda y no vuelve a salir al volver a esa zona.
    ///
    ///   GetComponentInParent&lt;WorldProp&gt;()?.Remove();
    /// </summary>
    public class WorldProp : MonoBehaviour
    {
        public long ChunkKey { get; internal set; }
        public int Id { get; internal set; }
        public int Rule { get; internal set; }
        internal GameObject SourcePrefab { get; set; }

        /// <summary>Lo quita del mundo para siempre (en esta partida).</summary>
        public void Remove()
        {
            var w = OpenWorld.Instance;
            if (w != null) w.RemoveProp(this);
            else Destroy(gameObject);
        }
    }
}
