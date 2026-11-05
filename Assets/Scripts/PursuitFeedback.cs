using System.Collections;
using UnityEngine;

// Feedback de persecucion (RF-11): borde rojo en la vista, latido y vibracion de los controladores.
// El borde pulsa con cada latido, asi que es el equivalente visual del sonido (RNF-06); la vibracion es el haptico.
public class PursuitFeedback : MonoBehaviour
{
    public EntityAI entidad;
    public Transform jugador;
    public Renderer borde;
    public AudioClip latido;
    public float intensidadMaxima = 0.85f;
    public float bpmLejos = 95f;
    public float bpmCerca = 150f;

    private Material material;
    private AudioSource fuente;
    private bool activo;
    private bool capturado; // tras la captura el borde rojo se queda fijo (sin latido ni vibracion)
    private float intensidad;
    private float pulso;
    private float proximoLatido;

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = latido;
        fuente.spatialBlend = 0f;
        fuente.playOnAwake = false;
        if (borde != null)
        {
            material = borde.material;
            borde.enabled = false;
        }
    }

    void OnEnable() { if (entidad != null) entidad.OnCambioEstado += AlCambiarEstado; }
    void OnDisable() { if (entidad != null) entidad.OnCambioEstado -= AlCambiarEstado; }

    void AlCambiarEstado(EntityAI.Estado estado)
    {
        activo = estado == EntityAI.Estado.Persecucion;
        capturado = estado == EntityAI.Estado.Captura;
        if (activo) proximoLatido = 0f;
        if (capturado) fuente.Stop(); // corta el latido que estuviera sonando; la vibracion ya no se pide
    }

    void Update()
    {
        // Persecucion: borde que pulsa con el latido. Captura: el borde se queda fijo en rojo (sin latido ni vibracion).
        bool mantener = activo || capturado;
        intensidad = Mathf.MoveTowards(intensidad, mantener ? 1f : 0f, Time.deltaTime * (mantener ? 3f : 1.2f));
        pulso = Mathf.MoveTowards(pulso, capturado ? 0.5f : 0f, Time.deltaTime * 4f);

        if (activo && jugador != null && entidad != null)
        {
            Vector3 d = jugador.position - entidad.transform.position;
            d.y = 0f;
            float t = Mathf.InverseLerp(entidad.radioDeteccion, entidad.distanciaCaptura, d.magnitude);
            float bpm = Mathf.Lerp(bpmLejos, bpmCerca, t);
            if (Time.time >= proximoLatido)
            {
                Latir();
                proximoLatido = Time.time + 60f / bpm;
            }
        }

        if (material != null)
        {
            float a = intensidad * (0.55f + 0.45f * pulso) * intensidadMaxima;
            material.SetFloat("_Intensidad", a);
            borde.enabled = a > 0.01f;
        }
    }

    void Latir()
    {
        pulso = 1f;
        if (latido != null) fuente.PlayOneShot(latido, 0.9f);
        ControllerHaptics.Pulse(0.9f, 0.1f);
        StartCoroutine(SegundoPulso());
    }

    IEnumerator SegundoPulso()
    {
        yield return new WaitForSeconds(0.22f);
        if (activo) ControllerHaptics.Pulse(0.6f, 0.1f);
    }
}
