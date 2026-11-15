using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Diana: suma puntos al recibir un impacto (flecha, piedra, bala...).
    /// Más puntos cuanto más cerca del centro. Puede desaparecer y reaparecer, o moverse a otro sitio.
    /// Ideal para probar el arco y para galerías de tiro o minijuegos.
    /// </summary>
    public class Target : MonoBehaviour, IDamageable
    {
        [Tooltip("Centro de la diana. Vacío = este objeto.")]
        [SerializeField] Transform center;
        [Tooltip("Radio de la diana en metros (para calcular la precisión).")]
        [SerializeField] float radius = 0.5f;
        [SerializeField] int minPoints = 10;
        [SerializeField] int maxPoints = 100;

        [Header("Tras el impacto")]
        [Tooltip("Se oculta y vuelve a aparecer tras estos segundos (0 = no se oculta).")]
        [SerializeField] float hideForSeconds = 0f;
        [Tooltip("Al reaparecer, salta a uno de estos puntos al azar.")]
        [SerializeField] Transform[] respawnPoints = new Transform[0];
        [SerializeField] GameObject hitEffectPrefab;
        [SerializeField] AudioClip hitSound;

        [Tooltip("Objetivo que avanza con cada acierto (ObjectiveTracker). Vacío = ninguno.")]
        [SerializeField] string objectiveId = "";

        [Tooltip("Se lanza con los puntos conseguidos.")]
        public UnityEvent<int> onHit = new UnityEvent<int>();

        bool m_Hidden;

        public void TakeDamage(DamageInfo info)
        {
            if (m_Hidden) return;
            Transform c = center != null ? center : transform;
            float accuracy = 1f - Mathf.Clamp01(Vector3.Distance(info.point, c.position) / Mathf.Max(0.01f, radius));
            int points = Mathf.RoundToInt(Mathf.Lerp(minPoints, maxPoints, accuracy));

            if (GameManager.Instance != null) GameManager.Instance.AddScore(points);
            FloatingText.Spawn("+" + points, info.point + Vector3.up * 0.1f, points >= 50 ? new Color(1f, 0.85f, 0.2f) : Color.white);
            if (hitEffectPrefab != null) Instantiate(hitEffectPrefab, info.point, Quaternion.identity);
            if (hitSound != null) AudioSource.PlayClipAtPoint(hitSound, info.point);
            ObjectiveTracker.ReportStatic(objectiveId);
            onHit.Invoke(points);

            if (hideForSeconds > 0f) StartCoroutine(HideAndRespawn());
        }

        IEnumerator HideAndRespawn()
        {
            m_Hidden = true;
            SetVisible(false);
            yield return new WaitForSeconds(hideForSeconds);
            if (respawnPoints.Length > 0)
            {
                var p = respawnPoints[Random.Range(0, respawnPoints.Length)];
                if (p != null) transform.SetPositionAndRotation(p.position, p.rotation);
            }
            SetVisible(true);
            m_Hidden = false;
        }

        void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = visible;
        }
    }
}
