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

    [Header("Escondite del jugador (nichos)")]
    [Tooltip("Altura maxima de la cabeza para considerar al jugador agachado (la misma de la entidad).")]
    public float alturaAgachado = 1.1f;
    [Tooltip("Si pierde al jugador porque se escondio, mira alrededor de donde lo vio entre estos dos tiempos (s) y luego se aleja.")]
    public Vector2 tiempoMirarAlrededor = new Vector2(4f, 6f);
    public float distanciaAlejarse = 10f;       // m al nicho
    public float tiempoEvitarNicho = 20f;       // s sin volver a ese nicho

    [Header("Ataque")]
    public float distanciaMordisco = 0.75f;     // horizontal, hasta la cabeza del jugador
    public float pausaTrasMordisco = 0.8f;

    [Header("Referencias (las asigna el builder)")]
    public Renderer cuerpo;
    public Renderer[] extras;                    // ojos, patas...
    public Collider colision;
    public AudioSource audioFuente;
    public Animator animador;                    // opcional: parametros Vel (float), Morder y Morir (triggers)
    [Tooltip("Velocidad de la animacion de caminar: vagando / persiguiendo (no hay clip de correr).")]
    public float velAnimVagar = 1.6f;
    public float velAnimPersecucion = 2.2f;

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

    // Escondite: ultimo nicho evitado, ultimo punto donde vio al jugador y fases "mira alrededor" / "se aleja".
    private NichoZona nichoEvitado;
    private float evitarHasta;
    private bool escondido;
    private bool miraAlrededor;
    private float finMirada;
    private bool alejandose;
    private float finAlejarse;
    private Vector3 ultimaPosVista;
    private MaterialPropertyBlock bloque;
    private Color colorOriginal;
    private readonly RaycastHit[] rayos = new RaycastHit[12];

    static readonly int IdColor = Shader.PropertyToID("_Color");
    static readonly int IdVel = Animator.StringToHash("Vel");
    static readonly int IdMorder = Animator.StringToHash("Morder");
    static readonly int IdMorir = Animator.StringToHash("Morir");
    static readonly int IdVelCaminar = Animator.StringToHash("VelCaminar");
    static readonly Color ColorGolpe = new Color(1.8f, 0.5f, 0.5f);

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        hogar = transform.position;
        hogarRot = transform.rotation;
        escalaOriginal = transform.localScale;
        Vida = vidaMax;
        bloque = new MaterialPropertyBlock();
        Sonido3D.Preparar(audioFuente, BusAudio.SFX, true, 1f, 20f);   // espacializador de Meta + eco de pasillo
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
        {
            animador.SetFloat(IdVel, (estado == Estado.Vagar || estado == Estado.Perseguir) ? agente.velocity.magnitude : 0f);
            // Sin clip de correr: al perseguir se acelera la animacion de caminar.
            animador.SetFloat(IdVelCaminar, estado == Estado.Perseguir ? velAnimPersecucion : velAnimVagar);
        }

        if (estado == Estado.Muriendo) { AnimarMuerte(); return; }
        if (estado == Estado.Muerto) return;

        bool enPartida = gm != null && gm.fase == GameManager.Fase.Jugando && jugador != null;
        if (!agente.isOnNavMesh) return;
        if (!enPartida) { agente.isStopped = true; return; }
        if (Time.time < reanudarMordisco) { agente.isStopped = true; return; }
        agente.isStopped = false;

        // Escondido en un nicho (misma condicion que la entidad): no lo ve, no lo muerde y pierde el rastro.
        var nichoActual = NichoZona.NichoDelJugador(jugador, alturaAgachado);
        escondido = nichoActual != null;
        if (escondido) { nichoEvitado = nichoActual; evitarHasta = Time.time + tiempoEvitarNicho; }
        bool evitando = nichoEvitado != null && Time.time < evitarHasta;

        if (Time.time >= proximaVista)
        {
            proximaVista = Time.time + 0.1f;
            if (Ve()) { ultimaVistaTiempo = Time.time; ultimaPosVista = jugador.position; }
        }
        bool viendo = !escondido && Time.time - ultimaVistaTiempo < 0.25f;

        if (estado == Estado.Vagar)
        {
            if (viendo) { estado = Estado.Perseguir; agente.speed = velocidadPersecucion; proximoDestino = 0f; alejandose = false; }
            else
            {
                // Si el jugador esta escondido y este mini esta cerca del nicho, no se queda ahi: se aleja.
                if (evitando && !alejandose && Time.time >= finAlejarse && DistanciaPlana(transform.position, nichoEvitado.Centro) < distanciaAlejarse) Alejarse();
                Vagar();
            }
        }
        else // Perseguir
        {
            // Perdio al jugador porque se escondio: mira alrededor de donde lo vio unos segundos y luego se aleja (sin seguirlo).
            if (escondido && !miraAlrededor) { miraAlrededor = true; finMirada = Time.time + Random.Range(tiempoMirarAlrededor.x, tiempoMirarAlrededor.y); proximoDestino = 0f; }
            if (miraAlrededor)
            {
                if (viendo) miraAlrededor = false;                       // salio y lo vio de nuevo
                else if (Time.time >= finMirada) { Alejarse(); return; }
                else { MirarAlrededor(); return; }
            }
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

    // Revisa los alrededores del ultimo punto donde lo vio (radio ~3 m), sin acercarse al jugador escondido.
    void MirarAlrededor()
    {
        agente.speed = velocidadVagar;
        if (Time.time < proximoDestino) return;
        proximoDestino = Time.time + Random.Range(1.2f, 2f);
        Vector2 r = Random.insideUnitCircle * 3f;
        NavMeshHit h;
        if (NavMesh.SamplePosition(ultimaPosVista + new Vector3(r.x, 0f, r.y), out h, 3f, NavMesh.AllAreas)) agente.SetDestination(h.position);
    }

    // Vuelve a vagar, pero primero se va a un punto del NavMesh a >= distanciaAlejarse del nicho. Durante 'tiempoEvitarNicho'
    // sus destinos de vagar tambien respetan esa distancia.
    void Alejarse()
    {
        miraAlrededor = false;
        estado = Estado.Vagar;
        agente.speed = velocidadVagar;
        alejandose = false;
        finAlejarse = Time.time + 2f;              // si no hay punto valido, no reintenta cada cuadro
        if (nichoEvitado != null)
        {
            Vector3 c = nichoEvitado.Centro;
            for (int i = 0; i < 16; i++)
            {
                Vector2 r = Random.insideUnitCircle.normalized * Random.Range(distanciaAlejarse, distanciaAlejarse + 8f);
                NavMeshHit h;
                if (!NavMesh.SamplePosition(c + new Vector3(r.x, 0f, r.y), out h, 3f, NavMesh.AllAreas)) continue;
                if (DistanciaPlana(h.position, c) < distanciaAlejarse) continue;
                agente.SetDestination(h.position);
                alejandose = true;
                finAlejarse = Time.time + 12f;
                break;
            }
        }
        proximoDestino = Time.time + 8f;
    }

    static float DistanciaPlana(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------- percepcion ----------

    bool Ve()
    {
        if (escondido) return false;
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
        // Se esta alejando de un nicho: sigue hasta llegar (o 12 s) antes de vagar de nuevo.
        if (alejandose)
        {
            if (Time.time < finAlejarse && (agente.pathPending || agente.remainingDistance > 0.6f)) return;
            alejandose = false;
            proximoDestino = 0f;
        }
        if (Time.time < proximoDestino && agente.remainingDistance > 0.3f) return;
        proximoDestino = Time.time + Random.Range(3f, 6f);
        bool evitando = nichoEvitado != null && Time.time < evitarHasta;
        for (int intento = 0; intento < (evitando ? 8 : 1); intento++)
        {
            Vector2 r = Random.insideUnitCircle * radioVagar;
            NavMeshHit h;
            if (!NavMesh.SamplePosition(hogar + new Vector3(r.x, 0f, r.y), out h, 3f, NavMesh.AllAreas)) continue;
            // 20 s sin volver al nicho: los destinos de vagar quedan a >= distanciaAlejarse de el.
            if (evitando && DistanciaPlana(h.position, nichoEvitado.Centro) < distanciaAlejarse) continue;
            agente.SetDestination(h.position);
            break;
        }
    }

    void Morder()
    {
        if (escondido) return;
        Vector3 d = jugador.position - transform.position;
        d.y = 0f;
        if (d.magnitude > distanciaMordisco) return;
        if (salud != null && salud.RecibirMordisco())
        {
            reanudarMordisco = Time.time + pausaTrasMordisco;
            var grito = SonidosPlaceholder.MiniAtaque;          // grito / mordisco real (si hay archivo mini_monstruo)
            if (grito != null) Sonar(grito);
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

    // Partida nueva desde cero (lo llama el GameManager): vivo, en su sitio inicial y vagando.
    public void ReiniciarPartida() { Reaparecer(); }

    void Reaparecer()
    {
        flashFin = 0f;
        fin = 0f;
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
        nichoEvitado = null;
        evitarHasta = 0f;
        escondido = false;
        miraAlrededor = false;
        alejandose = false;
        finAlejarse = 0f;
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
