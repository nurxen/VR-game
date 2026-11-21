using System;
using UnityEngine;

namespace VRKit
{
    /// <summary>
    /// Añade por código una zona de teletransporte de XRI (TeleportationArea) a un objeto con collider: suelos que se
    /// generan al jugar (terreno, chunks). Usa reflexión para funcionar con varias versiones de XRI, y si algo no
    /// encaja lo avisa una vez y sigue: nunca rompe la generación del mundo.
    /// </summary>
    public static class TeleportAreaUtil
    {
        static Type s_Area, s_Mask;
        static bool s_Warned;

        public static Component Add(GameObject go, float upToleranceDegrees = 30f)
        {
            try
            {
                if (s_Area == null)
                    s_Area = Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit")
                          ?? Type.GetType("UnityEngine.XR.Interaction.Toolkit.TeleportationArea, Unity.XR.Interaction.Toolkit");
                if (s_Area == null) return null;
                var area = go.AddComponent(s_Area);

                // Solo encima del suelo, no en paredes (si la versión de XRI lo permite)
                SetProp(area, "filterSelectionByHitNormal", true);
                SetProp(area, "upNormalToleranceDegrees", upToleranceDegrees);

                // Por código, XRI no le pone la capa de interacción «Teleport» (desde el editor, sí)
                if (s_Mask == null) s_Mask = Type.GetType("UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask, Unity.XR.Interaction.Toolkit");
                var layers = s_Area.GetProperty("interactionLayers");
                var getMask = s_Mask != null ? s_Mask.GetMethod("GetMask", new[] { typeof(string[]) }) : null;
                if (layers != null && layers.CanWrite && getMask != null)
                {
                    object raw = getMask.Invoke(null, new object[] { new[] { "Teleport" } });
                    int bits = raw is int ? (int)raw : 0;                       // GetMask devuelve un int
                    if (bits != 0)
                    {
                        object mask = null;
                        var fromInt = s_Mask.GetMethod("op_Implicit", new[] { typeof(int) });
                        if (fromInt != null) mask = fromInt.Invoke(null, new object[] { bits });
                        if (mask != null && layers.PropertyType.IsInstanceOfType(mask)) layers.SetValue(area, mask);
                    }
                }
                return area;
            }
            catch (Exception e)
            {
                if (!s_Warned) { s_Warned = true; Debug.LogWarning("[VRKit] No se pudo configurar el teletransporte sobre el terreno (" + e.GetType().Name + "): se puede andar igualmente. " + e.Message); }
                return null;
            }
        }

        static void SetProp(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name);
            if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(value)) p.SetValue(target, value);
        }
    }
}
