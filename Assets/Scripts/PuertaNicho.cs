using UnityEngine;

// Puerta de un nicho (RF-08). Entreabierta mientras el jugador no esta escondido; cuando entra al nicho y se agacha (la misma
// condicion de escondido de NichoZona) se cierra en ~0,65 s con un crujido corto, dejando una mirilla (rendija horizontal de
// 6 x 38 cm a ~1 m, la altura de los ojos agachado). Al levantarse o salir se vuelve a abrir.
//
// SIN colliders ni NavMeshObstacle: nunca empuja, atrapa ni bloquea teletransportes, y no cambia el NavMesh. La hoja gira hacia
// AFUERA (hacia el pasillo), asi que al cerrarse no pasa por donde esta el jugador. Solo mueve una bisagra (un Transform).
public class PuertaNicho : MonoBehaviour
{
    [Tooltip("El nicho (ZonaEscondite) que cierra esta puerta.")]
    public NichoZona nicho;
    [Tooltip("Transform de la bisagra: gira alrededor de Y (0 = cerrada).")]
    public Transform bisagra;
    [Tooltip("Angulo con el que queda entreabierta (grados).")]
    public float anguloAbierta = 100f;
    [Tooltip("Duracion de abrir o cerrar (s): 0,5 a 0,8.")]
    public float duracion = 0.65f;
    public float alturaAgachado = 1.1f;

    private float progreso;            // 0 = abierta, 1 = cerrada
    private bool cerrando;
    private float desde;               // desde cuando pide el estado contrario (anti-rebote)
    private Transform cabeza;
    private GameManager gm;
    private AudioSource fuente;
    private static AudioClip crujido;

    public bool Cerrada { get { return progreso >= 0.999f; } }
    public float Progreso { get { return progreso; } }

    void Start()
    {
        gm = FindObjectOfType<GameManager>();
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) cabeza = rig.centerEyeAnchor;
        var e = FindObjectOfType<EntityAI>(true);
        if (e != null) alturaAgachado = e.alturaAgachado;      // misma altura de agachado que la entidad

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        Sonido3D.Preparar(fuente, BusAudio.SFX, true, 1f, 15f);
        if (crujido == null) crujido = CrearCrujido();

        Aplicar();
    }

    void Update()
    {
        bool escondido = cabeza != null && nicho != null && (gm == null || gm.fase == GameManager.Fase.Jugando)
                         && nicho.Escondido(cabeza, alturaAgachado);

        // Anti-rebote: el cambio de estado se acepta si se mantiene ~0,1 s (el umbral de agachado puede vibrar).
        if (escondido != cerrando)
        {
            if (desde < 0f) desde = Time.time;
            if (Time.time - desde >= 0.1f)
            {
                cerrando = escondido;
                desde = -1f;
                Sonar(cerrando);
            }
        }
        else desde = -1f;

        float objetivo = cerrando ? 1f : 0f;
        if (!Mathf.Approximately(progreso, objetivo))
        {
            progreso = Mathf.MoveTowards(progreso, objetivo, Time.deltaTime / Mathf.Max(0.05f, duracion));
            Aplicar();
        }
    }

    void Aplicar()
    {
        if (bisagra == null) return;
        float k = progreso * progreso * (3f - 2f * progreso);       // suave al empezar y al terminar
        bisagra.localRotation = Quaternion.Euler(0f, anguloAbierta * (1f - k), 0f);
    }

    void Sonar(bool cierra)
    {
        if (fuente == null || crujido == null) return;
        fuente.pitch = cierra ? 1f : 0.85f;
        fuente.PlayOneShot(crujido, cierra ? 1f : 0.7f);
    }

    // Crujido corto de bisagra seca: sierra de tono que sube, con "agarrones" (stick-slip) y ruido suave. Procedural, ~0,65 s.
    static AudioClip CrearCrujido()
    {
        const int sr = 44100;
        const float dur = 0.65f;
        int n = (int)(dur * sr);
        var d = new float[n];
        var rnd = new System.Random(7);
        float fase = 0f, lp = 0f, pico = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float f = 110f + 100f * (t / dur) + 22f * Mathf.Sin(2f * Mathf.PI * 7f * t);
            fase += f / sr;
            float sierra = (fase - Mathf.Floor(fase)) * 2f - 1f;
            float agarron = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 31f * t + 3f * Mathf.Sin(2f * Mathf.PI * 5f * t));
            agarron *= agarron;
            float ruido = (float)rnd.NextDouble() * 2f - 1f;
            lp += (ruido - lp) * 0.12f;
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-Mathf.Max(0f, t - 0.35f) * 7f);
            float v = (sierra * 0.35f + lp * 0.9f) * agarron * env;
            d[i] = v;
            pico = Mathf.Max(pico, Mathf.Abs(v));
        }
        float g = 0.7f / pico;
        for (int i = 0; i < n; i++) d[i] *= g;
        var clip = AudioClip.Create("ph_crujido_puerta", n, 1, sr, false);
        clip.SetData(d, 0);
        return clip;
    }
}
