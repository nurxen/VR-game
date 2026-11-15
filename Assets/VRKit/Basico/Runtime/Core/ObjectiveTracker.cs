using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VRKit
{
    /// <summary>
    /// Lista de objetivos del nivel: «Recoge 5 setas», «Enciende los 3 faros», «Derrota a los duendes».
    /// Cualquier script avisa con ObjectiveTracker.Report("setas"), y desde el Inspector con Report(string).
    /// Coleccionables, enemigos (Health › On Death) y crafteo ya lo hacen con su campo objectiveId.
    /// Al completarlos todos puede dar la victoria.
    /// </summary>
    public class ObjectiveTracker : MonoBehaviour
    {
        public static ObjectiveTracker Instance { get; private set; }

        [Serializable]
        public class Objective
        {
            public string id = "objetivo";
            public string description = "Haz algo";
            [Min(1)] public int required = 1;
            [Tooltip("Oculto hasta que se cumple el anterior (objetivos en orden).")]
            public bool sequential;
            [HideInInspector] public int current;
            public bool Done { get { return current >= required; } }
        }

        [SerializeField] List<Objective> objectives = new List<Objective>();
        [Tooltip("Al cumplir todos, GameManager.Victory().")]
        [SerializeField] bool victoryWhenAllDone = true;
        [Tooltip("Texto opcional donde se pinta la lista (muñeca, HUD, pizarra del mundo).")]
        [SerializeField] TMP_Text listLabel;
        [SerializeField] AudioClip progressSound;
        [SerializeField] AudioClip completeSound;

        public UnityEvent<string> onObjectiveCompleted = new UnityEvent<string>();
        public UnityEvent onAllCompleted = new UnityEvent();
        public event Action Changed;

        public IReadOnlyList<Objective> Objectives { get { return objectives; } }
        public bool AllDone { get { foreach (var o in objectives) if (!o.Done) return false; return objectives.Count > 0; } }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }
        void Start() { Refresh(); }

        /// <summary>Atajo estático: ObjectiveTracker.Report("setas") desde cualquier script.</summary>
        public static void ReportStatic(string id, int amount = 1) { if (Instance != null) Instance.Report(id, amount); }

        public void Report(string id) { Report(id, 1); }

        public void Report(string id, int amount)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < objectives.Count; i++)
            {
                var o = objectives[i];
                if (o.id != id || o.Done || !IsVisible(i)) continue;
                o.current = Mathf.Min(o.required, o.current + amount);
                if (o.Done)
                {
                    VRAudio.Play2D(completeSound);
                    onObjectiveCompleted.Invoke(o.id);
                    FloatingText.SpawnInFront("Hecho: " + o.description, new Color(0.5f, 1f, 0.6f));
                }
                else VRAudio.Play2D(progressSound, 0.6f);
                break;
            }
            Refresh();
            if (AllDone)
            {
                onAllCompleted.Invoke();
                if (victoryWhenAllDone && GameManager.Instance != null) GameManager.Instance.Victory();
            }
        }

        public void AddObjective(string id, string description, int required)
        {
            objectives.Add(new Objective { id = id, description = description, required = required });
            Refresh();
        }

        bool IsVisible(int i)
        {
            if (!objectives[i].sequential) return true;
            for (int j = 0; j < i; j++) if (!objectives[j].Done) return false;
            return true;
        }

        void Refresh()
        {
            if (listLabel != null)
            {
                var sb = new StringBuilder();
                for (int i = 0; i < objectives.Count; i++)
                {
                    if (!IsVisible(i)) continue;
                    var o = objectives[i];
                    sb.Append(o.Done ? "<color=#8a8a8a><s>· " : "· ").Append(o.description);
                    if (o.required > 1) sb.Append("  ").Append(o.current).Append('/').Append(o.required);
                    if (o.Done) sb.Append("</s></color>");
                    sb.Append('\n');
                }
                listLabel.text = sb.ToString();
            }
            if (Changed != null) Changed();
        }
    }
}
