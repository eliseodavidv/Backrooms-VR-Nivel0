using UnityEngine;

// Vibracion por mano (ControllerHaptics solo vibra ambos controladores a la vez).
// Un unico objeto oculto apaga cada mano cuando vence su pulso.
public class VibracionMano : MonoBehaviour
{
    private static VibracionMano instancia;

    // Contador de pulsos pedidos (util para verificar en pruebas sin visor).
    public static int PulsosSolicitados;

    private float hastaIzq, hastaDer;

    public static void Pulso(OVRInput.Controller mano, float amplitud, float duracion)
    {
        if (mano != OVRInput.Controller.LTouch && mano != OVRInput.Controller.RTouch) return;
        PulsosSolicitados++;
        if (instancia == null)
        {
            var go = new GameObject("VibracionMano");
            instancia = go.AddComponent<VibracionMano>();
        }
        instancia.Pedir(mano, Mathf.Clamp01(amplitud), duracion);
    }

    void Pedir(OVRInput.Controller mano, float amp, float duracion)
    {
        OVRInput.SetControllerVibration(0.6f, amp, mano);
        float hasta = Time.unscaledTime + duracion;
        if (mano == OVRInput.Controller.LTouch) hastaIzq = hasta; else hastaDer = hasta;
    }

    void Update()
    {
        Apagar(ref hastaIzq, OVRInput.Controller.LTouch);
        Apagar(ref hastaDer, OVRInput.Controller.RTouch);
    }

    static void Apagar(ref float hasta, OVRInput.Controller mano)
    {
        if (hasta > 0f && Time.unscaledTime >= hasta)
        {
            OVRInput.SetControllerVibration(0f, 0f, mano);
            hasta = 0f;
        }
    }

    void OnDisable()
    {
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
        if (instancia == this) instancia = null;
    }
}
