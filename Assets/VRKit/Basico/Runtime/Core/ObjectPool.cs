using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Reutiliza objetos en lugar de crearlos y destruirlos (balas, flechas, efectos, enemigos).
    /// Crear y destruir muchos objetos provoca tirones en Quest 2 cuando el recolector de basura limpia.
    ///
    ///   var bala = ObjectPool.Spawn(prefabBala, pos, rot);
    ///   ObjectPool.Despawn(bala, 2f);           // la devuelve a la piscina dentro de 2 s
    ///   ObjectPool.Prewarm(prefabBala, 30);     // crea 30 al cargar el nivel, para que no haya tirones luego
    ///
    /// Si el objeto tiene componentes con IPoolable, se les avisa al salir y al volver.
    /// </summary>
    public static class ObjectPool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> s_Free = new Dictionary<GameObject, Stack<GameObject>>();
        static PoolHost s_Host;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;
            Stack<GameObject> stack;
            GameObject go = null;
            if (s_Free.TryGetValue(prefab, out stack))
                while (stack.Count > 0 && go == null) go = stack.Pop(); // por si alguno se destruyó al cambiar de escena

            if (go == null)
            {
                go = Object.Instantiate(prefab, position, rotation, parent);
                var tag = go.AddComponent<PooledObject>();
                tag.prefab = prefab;
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(position, rotation);
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                go.SetActive(true);
            }
            foreach (var p in go.GetComponentsInChildren<IPoolable>()) p.OnSpawned();
            return go;
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            var go = Spawn(prefab.gameObject, position, rotation, null);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>Devuelve el objeto a la piscina (o lo destruye si no salió de una).</summary>
        public static void Despawn(GameObject go, float delay = 0f)
        {
            if (go == null) return;
            if (delay > 0f) { Host().StartCoroutine(DespawnLater(go, delay)); return; }
            var tag = go.GetComponent<PooledObject>();
            if (tag == null || tag.prefab == null) { Object.Destroy(go); return; }
            if (!go.activeSelf) return; // ya estaba devuelto
            foreach (var p in go.GetComponentsInChildren<IPoolable>()) p.OnDespawned();
            go.SetActive(false);
            go.transform.SetParent(Host().transform, false);
            Stack<GameObject> stack;
            if (!s_Free.TryGetValue(tag.prefab, out stack)) { stack = new Stack<GameObject>(); s_Free[tag.prefab] = stack; }
            stack.Push(go);
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            var list = new List<GameObject>();
            for (int i = 0; i < count; i++) list.Add(Spawn(prefab, new Vector3(0f, -1000f, 0f), Quaternion.identity, null));
            foreach (var go in list) Despawn(go);
        }

        static IEnumerator DespawnLater(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            Despawn(go);
        }

        static PoolHost Host()
        {
            if (s_Host == null)
            {
                s_Host = new GameObject("[ObjectPool]").AddComponent<PoolHost>();
                s_Free.Clear();
            }
            return s_Host;
        }
    }

    /// <summary>Para scripts que necesitan reiniciarse al reutilizarse (vida, temporizadores…).</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

}
