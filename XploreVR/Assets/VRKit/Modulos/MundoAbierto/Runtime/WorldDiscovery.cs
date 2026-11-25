using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Exploración: la primera vez que entras en un bioma (o en uno nuevo de noche), aparece un aviso con su nombre.
    /// Lo descubierto se guarda con la partida (OpenWorld.Discovered), para un mapa o logros más adelante.
    /// Las cuevas se descubren solas (CaveSystem).
    /// </summary>
    public class WorldDiscovery : MonoBehaviour
    {
        [SerializeField] bool showSubtitle = true;
        [SerializeField] string prefix = "Descubres: ";
        [SerializeField] AudioClip discoverSound;
        [SerializeField] float checkEvery = 1.5f;

        public UnityEvent<string> onDiscover = new UnityEvent<string>();

        float m_Next;

        void Update()
        {
            var world = OpenWorld.Instance;
            var player = VRPlayer.Instance;
            if (world == null || player == null || !world.IsReady || Time.time < m_Next) return;
            m_Next = Time.time + checkEvery;
            if (CaveSystem.Instance != null && CaveSystem.Instance.InCave) return;

            var biome = world.BiomeAt(player.FeetPosition);
            if (biome == Biome.Fondo) return;
            if (!world.Discover("bioma:" + biome)) return;
            var profile = world.ProfileOf(biome);
            string name = profile != null ? profile.displayName : biome.ToString();
            if (showSubtitle) SubtitleSystem.SayStatic("", prefix + name);
            if (discoverSound != null) VRAudio.Play2D(discoverSound, 0.6f);
            onDiscover.Invoke(name);
        }
    }
}
