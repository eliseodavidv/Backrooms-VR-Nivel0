using Oculus.Interaction.Locomotion;
using UnityEngine;

// Solo diagnostico: escribe en el log cada giro y teletransporte que emite el SDK (con la mano de origen y el frame),
// para comprobar que cada empujon del stick gira una sola vez y que el teletransporte sale con los dos mandos.
public class LocomotionLogger : MonoBehaviour
{
    private LocomotionEventsConnection[] conexiones;

    void OnEnable()
    {
        conexiones = GetComponentsInChildren<LocomotionEventsConnection>(true);
        foreach (var c in conexiones)
        {
            var conexion = c;
            conexion.WhenLocomotionPerformed += Registrar;
        }
    }

    void OnDisable()
    {
        if (conexiones == null) return;
        foreach (var c in conexiones) if (c != null) c.WhenLocomotionPerformed -= Registrar;
    }

    void Registrar(LocomotionEvent e)
    {
        string que = e.Rotation != LocomotionEvent.RotationType.None && e.Translation == LocomotionEvent.TranslationType.None ? "GIRO"
                   : e.Translation != LocomotionEvent.TranslationType.None ? "TELETRANSPORTE" : "OTRO";
        Debug.Log("[Locomocion] " + que + " frame=" + Time.frameCount + " traslacion=" + e.Translation + " rotacion=" + e.Rotation +
                  " id=" + e.Identifier + " pose=" + e.Pose.position.ToString("F2") + " yaw=" + e.Pose.rotation.eulerAngles.y.ToString("F0"));
    }
}
