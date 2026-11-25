using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Objeto que se rompe al recibir un golpe fuerte o al quedarse sin vida:
    /// jarrones, cajas, cristales, barriles. Cambia el objeto por una versión rota
    /// (un prefab con los trozos, cada uno con Rigidbody) y los empuja.
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        [Tooltip("Prefab con los trozos. Cada trozo con Rigidbody + Collider.")]
        [SerializeField] GameObject brokenPrefab;
        [Tooltip("Velocidad de choque para romperse (m/s). 0 = no se rompe por choques.")]
        [SerializeField] float breakSpeed = 4f;
        [Tooltip("Si tiene Health, se rompe al morir.")]
        [SerializeField] bool breakOnDeath = true;
        [SerializeField] float explosionForce = 2f;
        [SerializeField] float piecesLifetime = 8f;
        [SerializeField] AudioClip breakSound;
        [Tooltip("Algo que soltar al romperse (una moneda, una llave...).")]
        [SerializeField] GameObject dropPrefab;

        public UnityEvent onBroken = new UnityEvent();

        bool m_Broken;

        void Awake()
        {
            var health = GetComponent<Health>();
            if (health != null && breakOnDeath) health.onDeath.AddListener(Break);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (breakSpeed > 0f && collision.relativeVelocity.magnitude >= breakSpeed)
                Break();
        }

        public void Break()
        {
            if (m_Broken) return;
            m_Broken = true;

            if (brokenPrefab != null)
            {
                var pieces = Instantiate(brokenPrefab, transform.position, transform.rotation);
                pieces.transform.localScale = transform.lossyScale;
                foreach (var rb in pieces.GetComponentsInChildren<Rigidbody>())
                    rb.AddExplosionForce(explosionForce, transform.position, 1f, 0.2f, ForceMode.Impulse);
                if (piecesLifetime > 0f) Destroy(pieces, piecesLifetime);
            }
            if (dropPrefab != null) Instantiate(dropPrefab, transform.position, Quaternion.identity);
            if (breakSound != null) AudioSource.PlayClipAtPoint(breakSound, transform.position);

            onBroken.Invoke();
            Destroy(gameObject);
        }
    }
}
