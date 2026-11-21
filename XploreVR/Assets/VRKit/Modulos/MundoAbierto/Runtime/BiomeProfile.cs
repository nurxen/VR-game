using System;
using System.Collections.Generic;
using UnityEngine;

namespace VRKit
{
    /// <summary>Un tipo de objeto que sale en un bioma: un árbol, una roca, hierba… con sus reglas.</summary>
    [Serializable]
    public class BiomePropRule
    {
        public GameObject prefab;
        [Tooltip("Cuántos por cada 100 m² (un cuadrado de 10 × 10 m). Árboles de bosque: 3–5. Rocas: 0,3.")]
        [Min(0f)] public float density = 1f;
        [Tooltip("Pendiente en grados donde puede salir.")]
        public Vector2 slope = new Vector2(0f, 30f);
        [Tooltip("Metros sobre el agua donde puede salir.")]
        public Vector2 altitude = new Vector2(0.5f, 500f);
        public Vector2 scale = new Vector2(0.8f, 1.25f);
        [Tooltip("Más cerca de los ríos: 1 = igual, 3 = el triple en las orillas.")]
        [Min(0f)] public float riverBoost = 1f;
        [Tooltip("Inclinar con la ladera (rocas, hierba). Desmarcado: recto (árboles).")]
        public bool alignToSlope = false;
        [Tooltip("Metros que se hunde en el suelo (para que no flote en las cuestas).")]
        public float sink = 0.15f;
        [Tooltip("Tiene colisión (árboles, rocas grandes). La hierba y las flores, no.")]
        public bool collide = true;
    }

    /// <summary>
    /// Cómo es un bioma: color del suelo, qué crece y qué suena. Uno por bioma
    /// (Create › VRKit › Mundo abierto › Bioma). El OpenWorld los usa todos.
    /// </summary>
    [CreateAssetMenu(menuName = "VRKit/Mundo abierto/Bioma", fileName = "Bioma")]
    public class BiomeProfile : ScriptableObject
    {
        public Biome biome = Biome.Pradera;
        [Tooltip("Nombre que se ve al descubrirlo.")]
        public string displayName = "Pradera";
        public Color groundColor = new Color(0.46f, 0.67f, 0.31f);
        public List<BiomePropRule> props = new List<BiomePropRule>();

        [Header("Sonido (opcional)")]
        public AudioClip[] dayMusic = new AudioClip[0];
        public AudioClip[] nightMusic = new AudioClip[0];
        public AudioClip dayAmbience;
        public AudioClip nightAmbience;
    }
}
