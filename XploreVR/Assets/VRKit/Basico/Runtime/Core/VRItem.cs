using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Le da identidad a un objeto: qué es («seta_roja»), cómo se llama («Seta roja») y de qué tipo es («ingrediente»).
    /// Lo leen el caldero (recetas), la mochila (qué acepta), los objetivos y los coleccionables.
    /// Así no hace falta crear un Tag de Unity para cada cosa.
    /// </summary>
    public class VRItem : MonoBehaviour
    {
        [Tooltip("Identificador sin espacios: seta_roja, cristal_azul, llave_torre…")]
        public string id = "objeto";
        public string displayName = "Objeto";
        [Tooltip("Categoría libre: ingrediente, llave, arma, comida, gema…")]
        public string category = "";

        /// <summary>El id de un objeto (o su nombre si no tiene VRItem).</summary>
        public static string IdOf(GameObject go)
        {
            if (go == null) return "";
            var i = go.GetComponentInParent<VRItem>();
            return i != null ? i.id : go.name;
        }

        public static string CategoryOf(GameObject go)
        {
            if (go == null) return "";
            var i = go.GetComponentInParent<VRItem>();
            return i != null ? i.category : "";
        }
    }
}
