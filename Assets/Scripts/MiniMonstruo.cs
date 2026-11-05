using UnityEngine;
using UnityEngine.AI;

// Mini monstruo: pequeno y rapido. Vaga cerca de su sitio; si ve al jugador lo persigue y lo muerde al tocarlo.
// Tiene vida (las armas lo danyan via IDanable). Al morir "desaparece" (se apagan renderers, collider y agente, no el
// GameObject, para seguir recibiendo la orden de reaparecer en la partida siguiente) y suma al contador de estadisticas.
[RequireComponent(typeof(NavMeshAgent))]
public class MiniMonstruo : MonoBehaviour, IDanable
{
    public enum Estado { Vagar, Perseguir, Muriendo, Muerto }

    [Header("Vida")]
    public int vidaMax = 3;

    [Header("Movimiento")]
    public float velocidadVagar = 1.8f;
    public float velocidadPersecucion = 4.5f;   // la entidad principal va a 3,2
    public float radioVagar = 6f;

    [Header("Vision")]
    public float radioVision = 10f;
    public float anguloVision = 140f;
    public float radioCercano = 2.5f;           // a esta distancia (con linea de vision) te nota sin importar hacia donde mira
    public float alturaOjos = 0.3f;
    public float tiempoPerdida = 4f;            // sigue buscando tanto tiempo tras perderte de vista

    [Header("Ataque")]
    public float distanciaMordisco = 0.75f;     // horizontal, hasta la cabeza del jugador
    public float pausaTrasMordisco = 0.8f;

    [Header("Referencias (las asigna el builder)")]
    public Renderer cuerpo;
    public Renderer[] extras;                    // ojos, patas...
    public Collider colision;
    public AudioSource audioFuente;
    public Animator animador;                    // opcional: parametros Vel (float), Morder y Morir (triggers)

    public Estado estado { get; private set; } = Estado.Vagar;
    public int Vida { get; private set; }

    private NavMeshAgent agente;
    private GameManager gm;
    private SaludJugador salud;
    private Transform jugador;
    private Vector3 hogar;
    private Quaternion hogarRot;
    private Vector3 escalaOriginal;
    private GameManager.Fase ultimaFase = GameManager.Fase.Menu;
    private float proximaVista, proximoDestino, ultimaVistaTiempo, reanudarMordisco, fin;
    private float flashFin;
    private MaterialPropertyBlock bloque;
    private Color colorOriginal;
    private readonly RaycastHit[] rayos = new RaycastHit[12];

    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdVel = Animator.StringToHash("Vel");
    static readonly int IdMorder = Animator.StringToHash("Morder");
    static readonly int IdMorir = Animator.StringToHash("Morir");
    static readonly Color ColorGolpe = new Color(1.8f, 0.5f, 0.5f);

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        hogar = transform.position;
        hogarRot = transform.rotation;
        escalaOriginal = transform.localScale;
        Vida = vidaMax;
        bloque = new MaterialPropertyBlock();
        if (cuerpo != null) colorOriginal = cuerpo.sharedMaterial.color;
    }

    void Start()
    {
        gm = FindObjectOfType<GameManager>();
        salud = FindObjectOfType<SaludJugador>();
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) jugador = rig.centerEyeAnchor;
        // Por si el monstruo se colocó un poco fuera del NavMesh: se ajusta al punto mas cercano.
        NavMeshHit h;
        if (!agente.isOnNavMesh && NavMesh.SamplePosition(transform.position, out h, 3f, NavMesh.AllAreas)) agente.Warp(h.position);
        hogar = transform.position;
    }

    void Update()
    {
        // Partida nueva (Menu/Tutorial/Fin -> Jugando; la pausa no cuenta): reaparece entero en su sitio.
        if (gm != null && gm.fase != GameManager.Fase.Pausa)
        {
            if (gm.fase == GameManager.Fase.Jugando && ultimaFase != GameManager.Fase.Jugando) Reaparecer();
            ultimaFase = gm.fase;
        }

        if (flashFin > 0f && Time.time >= flashFin) { flashFin = 0f; PonerColor(colorOriginal); }

        if (animador != null && agente.enabled && agente.isOnNavMesh)
            animador.SetFloat(IdVel, (estado == Estado.Vagar || estado == Estado.Perseguir) ? agente.velocity.magnitude : 0f);

        if (estado == Estado.Muriendo) { AnimarMuerte(); return; }
        if (estado == Estado.Muerto) return;

        bool enPartida = gm != null && gm.fase == GameManager.Fase.Jugando && jugador != null;
        if (!agente.isOnNavMesh) return;
        if (!enPartida) { agente.isStopped = true; return; }
        if (Time.time < reanudarMordisco) { agente.isStopped = true; return; }
        agente.isStopped = false;

        if (Time.time >= proximaVista)
        {
            proximaVista = Time.time + 0.1f;
            if (Ve()) ultimaVistaTiempo = Time.time;
        }
        bool viendo = Time.time - ultimaVistaTiempo < 0.25f;

        if (estado == Estado.Vagar)
        {
            if (viendo) { estado = Estado.Perseguir; agente.speed = velocidadPersecucion; proximoDestino = 0f; }
            else Vagar();
        }
        else // Perseguir
        {
            if (!viendo && Time.time - ultimaVistaTiempo > tiempoPerdida)
            {
                estado = Estado.Vagar;
                agente.speed = velocidadVagar;
                proximoDestino = 0f;
                return;
            }
            if (Time.time >= proximoDestino)
            {
                proximoDestino = Time.time + 0.2f;
                NavMeshHit h;
                if (NavMesh.SamplePosition(jugador.position, out h, 2.5f, NavMesh.AllAreas)) agente.SetDestination(h.position);
            }
            Morder();
        }
    }

    // ---------- percepcion ----------

    bool Ve()
    {
        Vector3 ojos = transform.position + Vector3.up * alturaOjos;
        Vector3 haciaJugador = jugador.position - ojos;
        float distPlana = new Vector2(haciaJugador.x, haciaJugador.z).magnitude;
        if (distPlana > radioVision) return false;

        if (distPlana > radioCercano)
        {
            Vector3 plano = new Vector3(haciaJugador.x, 0f, haciaJugador.z);
            if (Vector3.Angle(transform.forward, plano) > anguloVision * 0.5f) return false;
        }
        return !Bloqueado(ojos, jugador.position);
    }

    // Hay pared (o cualquier cosa solida que no sea arma/monstruo) entre los dos puntos.
    bool Bloqueado(Vector3 desde, Vector3 hasta)
    {
        Vector3 d = hasta - desde;
        float dist = d.magnitude;
        int n = Physics.RaycastNonAlloc(desde, d / dist, rayos, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var c = rayos[i].collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<ArmaAgarrable>() != null) continue;
            if (c.GetComponentInParent<MiniMonstruo>() != null) continue;
            return true;
        }
        return false;
    }

    // ---------- acciones ----------

    void Vagar()
    {
        agente.speed = velocidadVagar;
        if (Time.time < proximoDestino && agente.remainingDistance > 0.3f) return;
        proximoDestino = Time.time + Random.Range(3f, 6f);
        Vector2 r = Random.insideUnitCircle * radioVagar;
        NavMeshHit h;
        if (NavMesh.SamplePosition(hogar + new Vector3(r.x, 0f, r.y), out h, 3f, NavMesh.AllAreas)) agente.SetDestination(h.position);
    }

    void Morder()
    {
        Vector3 d = jugador.position - transform.position;
        d.y = 0f;
        if (d.magnitude > distanciaMordisco) return;
        if (salud != null && salud.RecibirMordisco())
        {
            reanudarMordisco = Time.time + pausaTrasMordisco;
            if (animador != null) animador.SetTrigger(IdMorder);
            agente.ResetPath();
        }
    }

    // ---------- danyo y muerte ----------

    public void RecibirDanio(int cantidad, Vector3 puntoImpacto)
    {
        if (estado == Estado.Muriendo || estado == Estado.Muerto) return;
        Vida -= cantidad;
        // Golpe recibido: se pone blanco un instante y avisa a los demas sentidos (te "ve" aunque no mirara).
        PonerColor(ColorGolpe);
        flashFin = Time.time + 0.08f;
        ultimaVistaTiempo = Time.time;
        if (estado == Estado.Vagar) { estado = Estado.Perseguir; agente.speed = velocidadPersecucion; }
        Sonar(SonidosPlaceholder.Impacto);
        if (Vida <= 0) Morir();
    }

    void Morir()
    {
        estado = Estado.Muriendo;
        if (animador != null) animador.SetTrigger(IdMorir);
        fin = Time.time + 0.35f;
        if (agente.isOnNavMesh) { agente.isStopped = true; agente.ResetPath(); }
        if (colision != null) colision.enabled = false;
        if (gm != null) gm.RegistrarMiniMuerto();
        SonidosPlaceholder.EnPunto(SonidosPlaceholder.Muerte, transform.position);
        Debug.Log("[Mini] " + name + " muerto.");
    }

    // Efecto simple: se encoge girando mientras sube un poco, y desaparece.
    void AnimarMuerte()
    {
        float k = 1f - Mathf.Clamp01((fin - Time.time) / 0.35f);
        transform.localScale = escalaOriginal * (1f - k);
        transform.Rotate(0f, 900f * Time.deltaTime, 0f, Space.World);
        PonerColor(Color.Lerp(ColorGolpe, colorOriginal, k));
        if (k >= 1f) Ocultar();
    }

    void Ocultar()
    {
        estado = Estado.Muerto;
        VisibleRenders(false);
        if (agente.enabled) agente.enabled = false;
        if (colision != null) colision.enabled = false;
        transform.localScale = escalaOriginal;
    }

    void Reaparecer()
    {
        Vida = vidaMax;
        estado = Estado.Vagar;
        transform.localScale = escalaOriginal;
        transform.rotation = hogarRot;
        VisibleRenders(true);
        if (colision != null) colision.enabled = true;
        agente.enabled = true;
        agente.Warp(hogar);
        agente.speed = velocidadVagar;
        agente.isStopped = false;
        PonerColor(colorOriginal);
        if (animador != null) { animador.Rebind(); animador.Update(0f); }
        ultimaVistaTiempo = -100f;
        proximoDestino = 0f;
        reanudarMordisco = 0f;
    }

    // ---------- utilidades ----------

    void VisibleRenders(bool v)
    {
        if (cuerpo != null) cuerpo.enabled = v;
        if (extras != null) foreach (var r in extras) if (r != null) r.enabled = v;
    }

    void PonerColor(Color c)
    {
        if (cuerpo == null) return;
        cuerpo.GetPropertyBlock(bloque);
        bloque.SetColor(IdColor, c);
        cuerpo.SetPropertyBlock(bloque);
    }

    void Sonar(AudioClip clip)
    {
        if (audioFuente != null) audioFuente.PlayOneShot(clip);
        else SonidosPlaceholder.EnPunto(clip, transform.position);
    }
}
