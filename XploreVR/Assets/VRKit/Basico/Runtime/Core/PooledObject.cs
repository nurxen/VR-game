using UnityEngine;

namespace VRKit
{
    /// <summary>Marca interna de ObjectPool: de qué prefab salió este objeto.</summary>
    public class PooledObject : MonoBehaviour { [HideInInspector] public GameObject prefab; }
}
