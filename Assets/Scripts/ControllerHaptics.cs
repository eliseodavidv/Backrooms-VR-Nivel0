using UnityEngine;

// Punto unico de vibracion de los controladores: varias fuentes piden pulsos y se aplica el de mayor amplitud.
public class ControllerHaptics : MonoBehaviour
{
    private static ControllerHaptics instancia;

    // Contador de pulsos pedidos (util para verificar en pruebas sin visor).
    public static int PulsosSolicitados;

    private float amplitud;
    private float hasta;
    private bool activa;

    public static void Pulse(float amplitud, float duracion)
    {
        PulsosSolicitados++;
        if (instancia == null)
        {
            var go = new GameObject("ControllerHaptics");
            instancia = go.AddComponent<ControllerHaptics>();
        }
        instancia.Pedir(Mathf.Clamp01(amplitud), duracion);
    }

    void Pedir(float a, float duracion)
    {
        if (Time.time >= hasta || a >= amplitud) amplitud = a;
        hasta = Mathf.Max(hasta, Time.time + duracion);
    }

    void Update()
    {
        if (Time.time < hasta)
        {
            Vibrar(0.5f, amplitud);
            activa = true;
        }
        else if (activa)
        {
            Vibrar(0f, 0f);
            activa = false;
        }
    }

    void OnDisable()
    {
        if (activa) Vibrar(0f, 0f);
        if (instancia == this) instancia = null;
    }

    static void Vibrar(float frecuencia, float amp)
    {
        OVRInput.SetControllerVibration(frecuencia, amp, OVRInput.Controller.LTouch);
        OVRInput.SetControllerVibration(frecuencia, amp, OVRInput.Controller.RTouch);
    }
}
