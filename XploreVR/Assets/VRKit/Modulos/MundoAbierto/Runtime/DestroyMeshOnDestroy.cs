using UnityEngine;

namespace VRKit
{
    /// <summary>Borra la malla creada por código cuando se borra el objeto (si no, se quedaría ocupando memoria).</summary>
    public class DestroyMeshOnDestroy : MonoBehaviour
    {
        void OnDestroy()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            var mc = GetComponent<MeshCollider>();
            if (mc != null && mc.sharedMesh != null && (mf == null || mc.sharedMesh != mf.sharedMesh)) Destroy(mc.sharedMesh);
        }
    }
}
