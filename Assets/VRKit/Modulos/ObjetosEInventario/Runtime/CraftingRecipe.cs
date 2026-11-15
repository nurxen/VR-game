using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Una receta (asset): qué ingredientes hacen falta (por id de VRItem y cantidad) y qué sale.
    /// Create › VRKit › Receta. Sirve para pociones, forja, cocina, alquimia, fusionar cristales…
    /// </summary>
    [CreateAssetMenu(menuName = "VRKit/Receta", fileName = "Receta")]
    public class CraftingRecipe : ScriptableObject
    {
        [Serializable]
        public class Ingredient
        {
            public string itemId = "seta_roja";
            [Min(1)] public int count = 1;
        }

        public string recipeName = "Poción";
        public Ingredient[] ingredients = new Ingredient[0];
        [Tooltip("Lo que aparece al completar la receta.")]
        public GameObject result;
        [Min(1)] public int resultCount = 1;
        [Tooltip("Objetivo que avanza al fabricarlo (opcional).")]
        public string objectiveId = "";

        /// <summary>¿Coincide exactamente con estos ingredientes?</summary>
        public bool Matches(Dictionary<string, int> contents)
        {
            int total = 0;
            foreach (var i in ingredients)
            {
                int have;
                if (!contents.TryGetValue(i.itemId, out have) || have != i.count) return false;
                total += i.count;
            }
            int all = 0;
            foreach (var kv in contents) all += kv.Value;
            return all == total;
        }

        /// <summary>¿Se podría llegar a esta receta añadiendo más cosas? (para saber si ya ha fallado)</summary>
        public bool CouldMatch(Dictionary<string, int> contents)
        {
            foreach (var kv in contents)
            {
                bool found = false;
                foreach (var i in ingredients)
                    if (i.itemId == kv.Key) { found = kv.Value <= i.count; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
