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

    private AudioSource musica;      // tension de persecucion: solo suena si hay audio real (musica_persecucion)
    private float peligro;           // 0 = lejos, 1 = encima: sube el volumen y el tono del latido

    void Awake()
    {
        latido = SonidosPlaceholder.Latido;   // audio real si existe (latido); si no, el generado con golpe seco en medios
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = latido;
        fuente.spatialBlend = 0f;
        fuente.playOnAwake = false;
        Sonido3D.Preparar(fuente, BusAudio.Musica, false);   // 2D y sin eco: el latido esta "dentro de la cabeza"
        musica = gameObject.AddComponent<AudioSource>();
        musica.playOnAwake = false;
        musica.loop = true;
        musica.volume = 0f;
        Sonido3D.Preparar(musica, BusAudio.Musica, false);
        if (borde != null)
        {
            material = borde.material;
            borde.enabled = false;
        }
    }

    void OnEnable() { if (entidad != null) entidad.OnCambioEstado += AlCambiarEstado; }
    void OnDisable() { if (entidad != null) entidad.OnCambioEstado -= AlCambiarEstado; }

    // Partida nueva desde cero: borde rojo, latido y vibracion apagados al instante (sin esperar al fundido).
    public void Reiniciar()
    {
        StopAllCoroutines();
        activo = false;
        capturado = false;
        intensidad = 0f;
        pulso = 0f;
        peligro = 0f;
        proximoLatido = 0f;
        if (fuente != null) fuente.Stop();
        if (musica != null) { musica.Stop(); musica.volume = 0f; }
        if (material != null)
        {
            material.SetFloat("_Intensidad", 0f);
            borde.enabled = false;
        }
    }

    private float ultimoRugido = -99f;

    void AlCambiarEstado(EntityAI.Estado estado)
    {
        bool eraPersecucion = activo;
        activo = estado == EntityAI.Estado.Persecucion;
        // Rugido 3D de la entidad: una sola vez al pasar a persecucion (no se repite si vuelve a perderte y verte en pocos segundos).
        if (activo && !eraPersecucion && Time.time - ultimoRugido > 4f && entidad != null)
        {
            ultimoRugido = Time.time;
            var rugido = SonidosPlaceholder.Rugido;
            if (rugido != null) Sonido3D.EnPunto(rugido, entidad.transform.position + Vector3.up * 1.4f, 1f, BusAudio.SFX);
        }
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
            peligro = t;
            float bpm = Mathf.Lerp(bpmLejos, bpmCerca, t);
            if (Time.time >= proximoLatido)
            {
                Latir();
                proximoLatido = Time.time + 60f / bpm;
            }
        }

        // Musica de tension (solo si hay audio real): entra al ser perseguido y se va al escapar.
        if (musica != null)
        {
            var mc = SonidosPlaceholder.MusicaPersecucion;
            if (mc != null && musica.clip != mc) musica.clip = mc;
            float objetivo = (mc != null && activo) ? 0.5f : 0f;
            musica.volume = Mathf.MoveTowards(musica.volume, objetivo, Time.unscaledDeltaTime * (objetivo > 0f ? 0.5f : 0.4f));
            if (musica.volume > 0.001f && !musica.isPlaying && musica.clip != null) musica.Play();
            else if (musica.volume <= 0.001f && musica.isPlaying) musica.Stop();
        }

        if (material != null)
        {
            float a = intensidad * (0.55f + 0.45f * pulso) * intensidadMaxima;
            material.SetFloat("_Intensidad", a);
            borde.enabled = a > 0.01f;
        }
    }

    // Cada latido avisa (lo usa el indicador de vidas para latir al ritmo del sonido).
    public static event System.Action AlLatir;

    void Latir()
    {
        pulso = 1f;
        AlLatir?.Invoke();
        // Mas cerca de la entidad: mas fuerte y un poco mas agudo (ademas de mas rapido, ver bpm).
        fuente.pitch = Mathf.Lerp(0.96f, 1.12f, peligro);
        var l = SonidosPlaceholder.Latido;   // se pide en cada latido: asi toma el audio real en cuanto esta cargado
        if (l != null) latido = l;
        if (latido != null) fuente.PlayOneShot(latido, Mathf.Lerp(0.65f, 1f, peligro));
        ControllerHaptics.Pulse(0.9f, 0.1f);
        StartCoroutine(SegundoPulso());
    }

    IEnumerator SegundoPulso()
    {
        yield return new WaitForSeconds(0.22f);
        if (activo) ControllerHaptics.Pulse(0.6f, 0.1f);
    }
}
