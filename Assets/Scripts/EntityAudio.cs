using UnityEngine;
using UnityEngine.AI;

// Audio espacial 3D de la entidad: pasos (segun velocidad) y respiracion (segun estado).
public class EntityAudio : MonoBehaviour
{
    public AudioClip pasos;
    public AudioClip respiracion;
    public float distanciaMaxima = 20f;

    private AudioSource sPasos;
    private AudioSource sRespiracion;
    private EntityAI ia;
    private NavMeshAgent agente;

    void Awake()
    {
        ia = GetComponent<EntityAI>();
        agente = GetComponent<NavMeshAgent>();
        // Si hay audio real en Assets/Audio/Reales (pasos_entidad / respiracion_entidad), reemplaza al provisional.
        var realPasos = SonidosPlaceholder.PasosEntidad; if (realPasos != null) pasos = realPasos;
        var realResp = SonidosPlaceholder.RespiracionEntidad; if (realResp != null) respiracion = realResp;
        sPasos = CrearFuente("Audio_Pasos", pasos, 0f);
        sRespiracion = CrearFuente("Audio_Respiracion", respiracion, 2.0f);
    }

    AudioSource CrearFuente(string nombre, AudioClip clip, float altura)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, altura - 1f, 0f);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip;
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 1.5f;
        s.maxDistance = distanciaMaxima;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        Sonido3D.Preparar(s, BusAudio.SFX, true);   // espacializador de Meta + grupo SFX (eco de pasillo)
        if (clip != null) s.Play();
        return s;
    }

    void Update()
    {
        // Captura: la entidad se calla del todo (pasos y respiracion) hasta que Reiniciar() la devuelva a Patrulla.
        if (ia.estadoActual == EntityAI.Estado.Captura)
        {
            if (sPasos.isPlaying) sPasos.Stop();
            if (sRespiracion.isPlaying) sRespiracion.Stop();
            return;
        }
        if (!sPasos.isPlaying && sPasos.clip != null) sPasos.Play();
        if (!sRespiracion.isPlaying && sRespiracion.clip != null) sRespiracion.Play();

        float v = agente.velocity.magnitude;
        float rel = Mathf.Clamp01(v / Mathf.Max(0.01f, ia.velocidadPersecucion));
        sPasos.volume = v < 0.1f ? 0f : Mathf.Lerp(0.5f, 1f, rel);
        sPasos.pitch = Mathf.Lerp(0.85f, 1.5f, rel);

        float vol; float pitch;
        switch (ia.estadoActual)
        {
            case EntityAI.Estado.Persecucion: vol = 1f; pitch = 1.35f; break;
            case EntityAI.Estado.Sospecha: vol = 0.7f; pitch = 1.1f; break;
            case EntityAI.Estado.Busqueda: vol = 0.6f; pitch = 1.15f; break;
            case EntityAI.Estado.Captura: vol = 0f; pitch = 1f; break;
            default: vol = 0.35f; pitch = 1f; break;
        }
        sRespiracion.volume = Mathf.MoveTowards(sRespiracion.volume, vol, Time.deltaTime * 2f);
        sRespiracion.pitch = pitch;
    }
}
