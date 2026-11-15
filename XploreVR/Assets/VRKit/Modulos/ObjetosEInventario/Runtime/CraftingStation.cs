using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VRKit
{
    /// <summary>
    /// Caldero, yunque, horno, mesa de alquimia: echa objetos dentro (zona Trigger) y, si forman una receta,
    /// salen convertidos en otra cosa. Si mezclas algo que no lleva a ninguna receta, «explota» y te los devuelve.
    ///  · Auto Craft: fabrica en cuanto la mezcla coincide con una receta.
    ///  · Si no, hay que llamar a Craft() (remover con un cucharón, pulsar un botón, golpear el yunque).
    /// Los ingredientes necesitan VRItem (con su id) y Rigidbody.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CraftingStation : MonoBehaviour
    {
        [SerializeField] CraftingRecipe[] recipes = new CraftingRecipe[0];
        [SerializeField] bool autoCraft = true;
        [Tooltip("Dónde aparece el resultado. Vacío = encima de la estación.")]
        [SerializeField] Transform output;
        [SerializeField] float craftDelay = 0.8f;
        [Tooltip("Texto opcional con lo que hay dentro.")]
        [SerializeField] TMP_Text contentsLabel;

        [Header("Efectos")]
        [SerializeField] AudioClip addSound;
        [SerializeField] AudioClip successSound;
        [SerializeField] AudioClip failSound;
        [SerializeField] ParticleSystem bubbles;

        public UnityEvent<string> onCrafted = new UnityEvent<string>();
        public UnityEvent onFailed = new UnityEvent();
        public UnityEvent<string> onIngredientAdded = new UnityEvent<string>();

        readonly List<GameObject> m_Inside = new List<GameObject>();
        readonly Dictionary<string, int> m_Contents = new Dictionary<string, int>();
        bool m_Busy;

        void Awake() { GetComponent<Collider>().isTrigger = true; RefreshLabel(); }

        /// <summary>Configura las recetas por código (lo usa el montador del editor).</summary>
        public void SetRecipes(CraftingRecipe[] newRecipes) { recipes = newRecipes; }

        void OnTriggerStay(Collider other)
        {
            if (m_Busy) return;
            var item = other.GetComponentInParent<VRItem>();
            if (item == null || m_Inside.Contains(item.gameObject)) return;
            var grab = item.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected) return; // aún en la mano
            Absorb(item);
        }

        void Absorb(VRItem item)
        {
            m_Inside.Add(item.gameObject);
            item.gameObject.SetActive(false);
            int n; m_Contents.TryGetValue(item.id, out n); m_Contents[item.id] = n + 1;
            if (addSound != null) VRAudio.PlayAt(addSound, transform.position, 0.8f, 0.15f);
            if (bubbles != null) bubbles.Play();
            onIngredientAdded.Invoke(item.id);
            RefreshLabel();

            if (autoCraft && FindMatch() != null) StartCoroutine(CraftRoutine());
            else if (!AnyCouldMatch()) StartCoroutine(Fail());
        }

        /// <summary>Intentar fabricar con lo que hay dentro (para cucharón, botón, martillo…).</summary>
        public void Craft()
        {
            if (m_Busy || m_Inside.Count == 0) return;
            if (FindMatch() != null) StartCoroutine(CraftRoutine());
            else StartCoroutine(Fail());
        }

        /// <summary>Devuelve todo lo de dentro.</summary>
        public void Empty() { if (!m_Busy) StartCoroutine(Eject(false)); }

        CraftingRecipe FindMatch()
        {
            foreach (var r in recipes) if (r != null && r.Matches(m_Contents)) return r;
            return null;
        }

        bool AnyCouldMatch()
        {
            foreach (var r in recipes) if (r != null && r.CouldMatch(m_Contents)) return true;
            return false;
        }

        IEnumerator CraftRoutine()
        {
            m_Busy = true;
            var recipe = FindMatch();
            yield return new WaitForSeconds(craftDelay);
            foreach (var go in m_Inside) if (go != null) Destroy(go);
            m_Inside.Clear(); m_Contents.Clear();
            Vector3 pos = output != null ? output.position : transform.position + Vector3.up * 0.4f;
            for (int i = 0; i < recipe.resultCount; i++)
                if (recipe.result != null) Instantiate(recipe.result, pos + Random.insideUnitSphere * 0.05f, Quaternion.identity);
            if (successSound != null) VRAudio.PlayAt(successSound, pos);
            FloatingText.Spawn(recipe.recipeName, pos + Vector3.up * 0.2f, new Color(0.7f, 0.9f, 1f));
            ObjectiveTracker.ReportStatic(recipe.objectiveId);
            onCrafted.Invoke(recipe.recipeName);
            RefreshLabel();
            m_Busy = false;
        }

        IEnumerator Fail()
        {
            if (failSound != null) VRAudio.PlayAt(failSound, transform.position);
            FloatingText.Spawn("¡Puaj!", transform.position + Vector3.up * 0.4f, new Color(1f, 0.5f, 0.4f));
            onFailed.Invoke();
            yield return Eject(true);
        }

        IEnumerator Eject(bool violent)
        {
            m_Busy = true;
            yield return new WaitForSeconds(0.3f);
            foreach (var go in m_Inside)
            {
                if (go == null) continue;
                go.transform.position = transform.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.1f;
                go.SetActive(true);
                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) rb.AddForce((Vector3.up * 3f + Random.insideUnitSphere) * (violent ? 1.5f : 0.6f), ForceMode.VelocityChange);
            }
            m_Inside.Clear(); m_Contents.Clear();
            RefreshLabel();
            yield return new WaitForSeconds(0.8f); // que los objetos salgan antes de volver a aceptar
            m_Busy = false;
        }

        void RefreshLabel()
        {
            if (contentsLabel == null) return;
            if (m_Contents.Count == 0) { contentsLabel.text = "(vacío)"; return; }
            var sb = new StringBuilder();
            foreach (var kv in m_Contents) sb.Append(kv.Value).Append(" × ").Append(kv.Key).Append('\n');
            contentsLabel.text = sb.ToString();
        }
    }
}
