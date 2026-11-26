using System.Collections;
using System.Reflection;
using UnityEngine;
using VRKit;

namespace NuestroJuego
{
    /// <summary>
    /// Para probar objetos en el mundo abierto. El terreno se genera al darle a Play, así que un objeto puesto en la
    /// escena a mano quedaría flotando o enterrado. Este script lo deja quieto, espera a que el suelo bajo el jugador
    /// esté listo y entonces lo pone delante del jugador, sobre el terreno.
    ///
    /// Uso: un objeto vacío «Pruebas» con este componente, y dentro (como hijos) todo lo que queráis probar:
    /// arco, carcaj, piedras… Se mueve el grupo entero, con sus posiciones entre ellos. Funciona también sin mundo
    /// abierto (busca el suelo con un rayo).
    /// </summary>
    public class PonerDelanteDelJugador : MonoBehaviour
    {
        [Tooltip("Metros delante del jugador.")]
        [SerializeField] float distancia = 1.2f;
        [Tooltip("Metros a la derecha (negativo = izquierda).")]
        [SerializeField] float lado = 0f;
        [Tooltip("Altura sobre el suelo del punto de origen del grupo. Poned los hijos a su altura dentro del grupo (una mesa, el suelo…).")]
        [SerializeField] float altura = 0f;
        [Tooltip("Girar el grupo para que mire hacia el jugador.")]
        [SerializeField] bool mirarAlJugador = true;

        IEnumerator Start()
        {
            // 1. Quietos hasta que haya suelo (si no, se caen al vacío)
            var cuerpos = GetComponentsInChildren<Rigidbody>(true);
            var eraCinematico = new bool[cuerpos.Length];
            for (int i = 0; i < cuerpos.Length; i++) { eraCinematico[i] = cuerpos[i].isKinematic; cuerpos[i].isKinematic = true; }

            // 2. Esperar al jugador y, si hay mundo abierto, a que su suelo tenga colisión
            float limite = Time.time + 30f;
            while (Time.time < limite)
            {
                var jugador = VRPlayer.Instance;
                bool mundoListo = OpenWorld.Instance == null || OpenWorld.Instance.IsReady;
                if (jugador != null && jugador.Head != null && mundoListo) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);   // que los chunks de alrededor también tengan colisión

            // 3. Colocar
            var p = VRPlayer.Instance;
            if (p != null && p.Head != null)
            {
                Vector3 delante = p.HeadForwardFlat;
                Vector3 derecha = Vector3.Cross(Vector3.up, delante);
                Vector3 pos = p.FeetPosition + delante * distancia + derecha * lado;
                pos.y = Suelo(pos) + altura;
                if (mirarAlJugador) transform.rotation = Quaternion.LookRotation(-delante, Vector3.up);
                transform.position = pos;
                Physics.SyncTransforms();
            }

            // 4. Los que vuelven solos a su sitio (ReturnToSpawn) tienen que volver AQUÍ, no a donde estaban en el editor
            foreach (var r in GetComponentsInChildren<ReturnToSpawn>(true)) ApuntarSitio(r);

            // 5. Física otra vez como estaba
            for (int i = 0; i < cuerpos.Length; i++) if (cuerpos[i] != null) cuerpos[i].isKinematic = eraCinematico[i];
        }

        static float Suelo(Vector3 pos)
        {
            if (OpenWorld.Instance != null) return OpenWorld.Instance.GroundHeight(pos.x, pos.z);
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 50f, Vector3.down, out hit, 200f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
            return pos.y;
        }

        void ApuntarSitio(ReturnToSpawn r)
        {
            var campo = typeof(ReturnToSpawn).GetField("spawnPoint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (campo == null || campo.FieldType != typeof(Transform) || campo.GetValue(r) as Transform != null) return;
            var sitio = new GameObject("Sitio de " + r.name).transform;
            sitio.SetParent(transform, true);
            sitio.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            campo.SetValue(r, sitio);
        }
    }
}
