using UnityEngine;

// Parpadeo de un panel fluorescente: cambia la emision del panel y la intensidad de una luz.
// Solo se usa en 2-3 paneles del laberinto; el resto de la iluminacion es bakeada.
public class FluorescentFlicker : MonoBehaviour
{
    [Header("Referencias")]
    public Renderer panel;
    public Light luz;

    [Header("Emision (HDR)")]
    public Color colorEncendido = new Color(1.6f, 1.55f, 1.3f);
    public Color colorApagado = new Color(0.08f, 0.08f, 0.07f);
    public float intensidadLuz = 1.2f;

    [Header("Ritmo")]
    public Vector2 tiempoEstable = new Vector2(1.5f, 5f);
    public Vector2Int parpadeos = new Vector2Int(2, 6);

    private MaterialPropertyBlock bloque;
    private float siguiente;
    private int restantes;
    private bool encendida = true;

    [Header("Audio (zumbido + chasquido sincronizados con el parpadeo)")]
    public float distanciaAudio = 8f;       // solo se oye (y solo suena) a esta distancia: cuida la CPU del Quest
    public float volumenZumbido = 0.5f;

    // El zumbido real es muy bajo (rms 0,02): el procesado lo sube al maximo posible y aqui se compensa el resto con el volumen.
    float VolZumbido { get { return volumenZumbido * (SonidosPlaceholder.ZumbidoLuzReal ? 2f : 1f); } }

    private AudioSource zumbido, chasquido;
    private Transform oyente;
    private GameManager gm;
    private float proximoChequeo;
    private bool cerca;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        siguiente = Time.time + Random.Range(0f, tiempoEstable.y);
        CrearAudio();
    }

    void Start()
    {
        gm = FindObjectOfType<GameManager>();
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) oyente = rig.centerEyeAnchor;
    }

    void CrearAudio()
    {
        Vector3 pos = panel != null ? panel.transform.position : transform.position;
        zumbido = NuevaFuente("Zumbido", pos, true, BusAudio.Ambiente, distanciaAudio);
        zumbido.clip = SonidosPlaceholder.ZumbidoLuz;
        zumbido.volume = VolZumbido;
        zumbido.pitch = Random.Range(0.97f, 1.03f);      // cada luz suena un poco distinta
        chasquido = NuevaFuente("Chasquido", pos, false, BusAudio.SFX, distanciaAudio + 2f);
    }

    AudioSource NuevaFuente(string nombre, Vector3 pos, bool bucle, BusAudio bus, float max)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = bucle;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;         // llega a silencio total en "max"
        Sonido3D.Preparar(s, bus, true, 1f, max);
        return s;
    }

    // Cada 0,3 s: solo suena si el jugador esta cerca y la partida en curso.
    void ActualizarAudio()
    {
        bool dentro = oyente != null && gm != null && gm.fase == GameManager.Fase.Jugando &&
                      Vector3.Distance(oyente.position, zumbido.transform.position) <= distanciaAudio;
        if (dentro && !cerca)
        {
            var z = SonidosPlaceholder.ZumbidoLuz;   // audio real procesado en cuanto esta cargado
            if (z != null) zumbido.clip = z;
            zumbido.Play();
            if (zumbido.clip != null) zumbido.time = Random.value * zumbido.clip.length * 0.9f;
        }
        else if (!dentro && cerca) zumbido.Stop();
        cerca = dentro;
    }

    void Update()
    {
        if (Time.unscaledTime >= proximoChequeo)
        {
            proximoChequeo = Time.unscaledTime + 0.3f;
            ActualizarAudio();
        }

        if (Time.time < siguiente) return;

        if (restantes > 0)
        {
            restantes--;
            encendida = !encendida;
            if (restantes == 0)
            {
                encendida = true;
                siguiente = Time.time + Random.Range(tiempoEstable.x, tiempoEstable.y);
            }
            else
            {
                siguiente = Time.time + Random.Range(0.04f, 0.14f);
            }
        }
        else
        {
            restantes = Random.Range(parpadeos.x, parpadeos.y + 1);
            encendida = false;
            siguiente = Time.time + 0.05f;
        }

        Aplicar(true);
    }

    // Partida nueva desde cero: el panel queda encendido y estable.
    public void Reiniciar()
    {
        restantes = 0;
        encendida = true;
        siguiente = Time.time + Random.Range(0f, tiempoEstable.y);
        Aplicar(false);
    }

    void Aplicar(bool sonar)
    {
        // Sonido sincronizado: al apagarse el zumbido se corta y suena un chasquido fuerte; al encender, uno mas suave.
        if (zumbido != null) zumbido.volume = encendida ? VolZumbido : 0f;
        if (sonar && cerca && chasquido != null)
        {
            chasquido.pitch = Random.Range(0.9f, 1.1f);
            var cl = SonidosPlaceholder.ChasquidoLuz;
            if (cl != null) chasquido.PlayOneShot(cl, encendida ? 0.55f : 1f);
        }

        if (panel != null)
        {
            panel.GetPropertyBlock(bloque);
            bloque.SetColor("_EmissionColor", encendida ? colorEncendido : colorApagado);
            panel.SetPropertyBlock(bloque);
        }
        if (luz != null) luz.intensity = encendida ? intensidadLuz : 0f;
    }
}
