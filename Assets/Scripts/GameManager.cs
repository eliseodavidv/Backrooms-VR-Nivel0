using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

// Flujo del juego (fig. 7): Menu -> (Tutorial) -> Partida -> Victoria / Derrota -> Reintentar o Menu.
// Victoria: llegar a la salida. Derrota: captura de la entidad o fin del tiempo (8:00).
// Tambien: estadisticas (RF-14), pausa y volumen (RNF-08), paneles a 1,5 m (RNF-04).
public class GameManager : MonoBehaviour
{
    public enum Fase { Menu, Tutorial, Jugando, Pausa, Fin }

    [Header("Escena")]
    public Transform rig;
    public Transform cabeza;
    public EntityAI entidad;
    public Transform salida;
    public PlayerNoiseEmitter ruido;
    public WristClock reloj;
    public LaserPointer laser;

    [Header("Paneles")]
    public GameObject panelMenu;
    public GameObject panelTutorial;
    public GameObject panelPausa;
    public GameObject panelFin;

    [Header("Pantalla de fin")]
    public Text finTitulo;
    public Text finResumen;
    public Text finDetalle;
    public Text finReintentarTexto;
    public VRButton botonVotoMeGusta;
    public VRButton botonVotoNoMeGusta;
    public Color colorVictoria = new Color(0.37f, 0.81f, 0.60f);
    public Color colorDerrota = new Color(0.78f, 0.33f, 0.24f);

    [Header("Pausa / volumen")]
    public Text pausaVolumenTexto;
    public RectTransform pausaBarraRelleno;
    public float pausaBarraAncho = 300f;

    [Header("Partida")]
    public float duracion = 480f;
    public float distanciaSalida = 1.2f;
    public float distanciaPaneles = 1.5f;
    [Tooltip("Distancia de dibujo mientras hay un panel visible (sin occlusion culling).")]
    public float farConPaneles = 20f;

    private Camera camara;
    private float farOriginal;
    private bool oclusionOriginal;

    public Fase fase { get; private set; } = Fase.Menu;
    public float tiempoRestante;
    public int detectado { get; private set; }
    public int teletransportes { get; private set; }
    public int escondites { get; private set; }
    public int minisMuertos { get; private set; }
    public string voto { get; private set; }
    public float volumen { get; private set; } = 0.8f;

    private Fase faseAnterior;
    private bool escondidoPrevio;
    private Vector3 posInicioJugador;
    private Quaternion rotInicioJugador;
    private Vector3 posInicioEntidad;
    private Quaternion rotInicioEntidad;
    private readonly List<Behaviour> locomocion = new List<Behaviour>();

    const string KeyTutorial = "backrooms_tutorial_visto";
    const string KeyVolumen = "backrooms_volumen";

    void OnEnable()
    {
        PlayerNoiseEmitter.Teletransporte += AlTeletransportarse;
        if (entidad != null)
        {
            entidad.OnCambioEstado += AlCambiarEstadoEntidad;
            entidad.OnCaptura += AlCapturar;
        }
    }

    void OnDisable()
    {
        PlayerNoiseEmitter.Teletransporte -= AlTeletransportarse;
        if (entidad != null)
        {
            entidad.OnCambioEstado -= AlCambiarEstadoEntidad;
            entidad.OnCaptura -= AlCapturar;
        }
    }

    IEnumerator Start()
    {
        posInicioJugador = rig.position;
        rotInicioJugador = rig.rotation;
        posInicioEntidad = entidad.transform.position;
        rotInicioEntidad = entidad.transform.rotation;

        foreach (var b in rig.GetComponentsInChildren<Behaviour>(true))
        {
            string n = b.GetType().Name;
            if (n == "TeleportInteractor" || n == "LocomotionTurnerInteractor" || n == "LocomotionAxisTurnerInteractor")
                locomocion.Add(b);
        }

        volumen = PlayerPrefs.GetFloat(KeyVolumen, 0.8f);
        AplicarVolumen();

        // El tracking del visor no es fiable en el primer frame: se prepara todo ya y se muestra el menu un instante despues.
        fase = Fase.Menu;
        PrepararSinPartida();
        if (reloj != null) reloj.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.3f);
        MostrarMenu();
    }

    void Update()
    {
        if (OVRInput.GetDown(OVRInput.Button.Start)) AlternarPausa();

        if (fase != Fase.Jugando) return;

        tiempoRestante -= Time.deltaTime;
        if (reloj != null) reloj.Mostrar(tiempoRestante);

        // Se calcula aqui (no con entidad.jugadorEscondido, que queda obsoleto mientras la entidad esta apagada).
        bool esc = NichoZona.JugadorEscondido(cabeza, entidad.alturaAgachado);
        if (esc && !escondidoPrevio) escondites++;
        escondidoPrevio = esc;

        if (tiempoRestante <= 0f)
        {
            Terminar(false, "tiempo");
            return;
        }

        Vector3 d = cabeza.position - salida.position;
        d.y = 0f;
        if (d.magnitude <= distanciaSalida) Terminar(true, "salida");
    }

    // ---------- flujo ----------

    public void MostrarMenu()
    {
        fase = Fase.Menu;
        PrepararSinPartida();
        if (reloj != null) reloj.gameObject.SetActive(false);
        Mostrar(panelMenu);
    }

    public void Comenzar()
    {
        // Primera vez: el tutorial; despues va directo a la partida (fig. 7: "omitir tutorial").
        if (PlayerPrefs.GetInt(KeyTutorial, 0) == 0) AbrirTutorial();
        else IniciarPartida();
    }

    public void AbrirTutorial()
    {
        fase = Fase.Tutorial;
        PrepararSinPartida();
        if (reloj != null)
        {
            reloj.gameObject.SetActive(true);
            reloj.Mostrar(duracion);
        }
        Mostrar(panelTutorial);
    }

    public void TutorialEntendido()
    {
        PlayerPrefs.SetInt(KeyTutorial, 1);
        IniciarPartida();
    }

    public void IniciarPartida()
    {
        detectado = teletransportes = escondites = minisMuertos = 0;
        voto = null;
        escondidoPrevio = false;
        tiempoRestante = duracion;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        ColocarJugadorEnInicio();
        entidad.gameObject.SetActive(true);
        var agente = entidad.GetComponent<NavMeshAgent>();
        agente.Warp(posInicioEntidad);
        entidad.transform.rotation = rotInicioEntidad;
        entidad.Reiniciar();

        OcultarPaneles();
        SetLocomocion(true);
        if (reloj != null)
        {
            reloj.gameObject.SetActive(true);
            reloj.Mostrar(tiempoRestante);
        }
        fase = Fase.Jugando;
        ActualizarLaser();
        Debug.Log("[GameManager] Partida iniciada (" + duracion + " s).");
    }

    void Terminar(bool victoria, string causa)
    {
        if (fase != Fase.Jugando) return;
        fase = Fase.Fin;
        float tiempoJugado = duracion - Mathf.Max(0f, tiempoRestante);

        // La entidad se retira salvo en la captura, donde queda frente al jugador.
        if (causa != "captura")
        {
            entidad.Reiniciar(); // apaga el borde rojo y la vibracion
            entidad.gameObject.SetActive(false);
        }
        SetLocomocion(false);

        StatsLogger.Guardar(victoria ? "victoria" : "derrota", causa, tiempoJugado, detectado, teletransportes, escondites, minisMuertos, voto);

        string vez = detectado == 1 ? "vez" : "veces";
        if (victoria)
        {
            finTitulo.text = "ESCAPASTE";
            finTitulo.color = colorVictoria;
            finResumen.text = "Tiempo: " + Formato(tiempoJugado) + "   ·   Detectado: " + detectado + " " + vez;
            finReintentarTexto.text = "Jugar de nuevo";
        }
        else
        {
            finTitulo.text = causa == "tiempo" ? "SE ACABÓ EL TIEMPO" : causa == "monstruo" ? "TE DEVORARON" : "TE ENCONTRÓ";
            finTitulo.color = colorDerrota;
            finResumen.text = "Tiempo sobrevivido: " + Formato(tiempoJugado) + "   ·   Detectado: " + detectado + " " + vez;
            finReintentarTexto.text = "Reintentar";
        }
        finDetalle.text = "Teletransportes: " + teletransportes + "   ·   Escondites: " + escondites + "   ·   Mini monstruos: " + minisMuertos;
        botonVotoMeGusta.SetSeleccionado(false);
        botonVotoNoMeGusta.SetSeleccionado(false);

        Mostrar(panelFin);
        Debug.Log("[GameManager] Fin: " + (victoria ? "VICTORIA" : "DERROTA") + " (" + causa + ") a los " + Formato(tiempoJugado));
    }

    public void Reintentar() { IniciarPartida(); }

    public void Votar(string valor)
    {
        voto = valor;
        botonVotoMeGusta.SetSeleccionado(valor == "me_gusta");
        botonVotoNoMeGusta.SetSeleccionado(valor == "no_me_gusta");
        StatsLogger.ActualizarUltimoVoto(valor);
    }

    public void VotoMeGusta() { Votar("me_gusta"); }
    public void VotoNoMeGusta() { Votar("no_me_gusta"); }

    public void Salir()
    {
        Debug.Log("[GameManager] Salir");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- pausa y volumen (RNF-08) ----------

    public void AlternarPausa()
    {
        if (fase == Fase.Pausa) Reanudar();
        else Pausar();
    }

    public void Pausar()
    {
        if (fase == Fase.Pausa) return;
        faseAnterior = fase;
        fase = Fase.Pausa;
        panelMenu.SetActive(false);
        panelTutorial.SetActive(false);
        panelFin.SetActive(false);
        if (faseAnterior == Fase.Jugando)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            SetLocomocion(false);
        }
        ActualizarVolumenUI();
        Mostrar(panelPausa);
    }

    public void Reanudar()
    {
        if (fase != Fase.Pausa) return;
        panelPausa.SetActive(false);
        fase = faseAnterior;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        switch (fase)
        {
            case Fase.Menu: Mostrar(panelMenu); break;
            case Fase.Tutorial: Mostrar(panelTutorial); break;
            case Fase.Fin: Mostrar(panelFin); break;
            case Fase.Jugando: SetLocomocion(true); ActualizarLaser(); break;
        }
    }

    public void VolumenMas() { CambiarVolumen(+0.1f); }
    public void VolumenMenos() { CambiarVolumen(-0.1f); }

    void CambiarVolumen(float delta)
    {
        volumen = Mathf.Round(Mathf.Clamp01(volumen + delta) * 10f) / 10f;
        PlayerPrefs.SetFloat(KeyVolumen, volumen);
        AplicarVolumen();
    }

    void AplicarVolumen()
    {
        AudioListener.volume = volumen;
        ActualizarVolumenUI();
    }

    void ActualizarVolumenUI()
    {
        if (pausaVolumenTexto != null) pausaVolumenTexto.text = "Volumen: " + Mathf.RoundToInt(volumen * 100f) + "%";
        if (pausaBarraRelleno != null) pausaBarraRelleno.sizeDelta = new Vector2(pausaBarraAncho * volumen, pausaBarraRelleno.sizeDelta.y);
    }

    // ---------- eventos del juego ----------

    void AlTeletransportarse(Vector3 destino)
    {
        if (fase == Fase.Jugando) teletransportes++;
    }

    void AlCambiarEstadoEntidad(EntityAI.Estado estado)
    {
        if (fase == Fase.Jugando && estado == EntityAI.Estado.Persecucion) detectado++;
    }

    void AlCapturar()
    {
        if (fase == Fase.Jugando) Terminar(false, "captura");
    }

    // Combate: lo llaman los mini monstruos (conteo de bajas) y SaludJugador (derrota al quedarse sin vida).
    public void RegistrarMiniMuerto()
    {
        if (fase == Fase.Jugando) minisMuertos++;
    }

    public void DerrotaPorMonstruo()
    {
        Terminar(false, "monstruo");
    }

    // ---------- utilidades ----------

    void PrepararSinPartida()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        entidad.gameObject.SetActive(false);
        ColocarJugadorEnInicio();
        SetLocomocion(false);
        OcultarPaneles();
        ActualizarLaser();
    }

    void ColocarJugadorEnInicio()
    {
        rig.SetPositionAndRotation(posInicioJugador, rotInicioJugador);
        if (ruido != null) ruido.Reanclar();
    }

    void SetLocomocion(bool activa)
    {
        foreach (var b in locomocion)
            if (b != null) b.enabled = activa;
    }

    void OcultarPaneles()
    {
        panelMenu.SetActive(false);
        panelTutorial.SetActive(false);
        panelPausa.SetActive(false);
        panelFin.SetActive(false);
    }

    void ActualizarLaser()
    {
        if (laser != null) laser.activo = fase != Fase.Jugando;
        AjustarCamaraParaPaneles(fase != Fase.Jugando);
    }

    // Con un panel visible la camara apaga el occlusion culling: Umbra descarta los objetos dinamicos que quedan en
    // celdas sin visibilidad (p. ej. un panel detras de una pared) y el panel desapareceria aunque use ZTest Always.
    // Mientras hay panel el jugador esta quieto, asi que el coste se acota con un far corto; en partida se restaura.
    void AjustarCamaraParaPaneles(bool hayPanel)
    {
        if (camara == null)
        {
            camara = cabeza.GetComponent<Camera>();
            if (camara == null) return;
            farOriginal = camara.farClipPlane;
            oclusionOriginal = camara.useOcclusionCulling;
        }
        camara.useOcclusionCulling = hayPanel ? false : oclusionOriginal;
        camara.farClipPlane = hayPanel ? Mathf.Min(farOriginal, farConPaneles) : farOriginal;
    }

    // Coloca el panel a 1,5 m justo delante de la mirada y a la altura de los ojos, mirando al jugador.
    // Los paneles usan el shader Backrooms/UIAlways (ZTest Always): se dibujan por encima de la geometria,
    // asi que ninguna pared ni la entidad los puede tapar, aunque el panel quede dentro de un muro.
    void Mostrar(GameObject panel)
    {
        Vector3 frente = cabeza.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.01f) frente = rig.forward;
        frente.Normalize();

        // Altura de los ojos: si el tracking aun no llego (cabeza a ras del suelo) se usa una altura de pie por defecto.
        float alturaOjos = Mathf.Max(cabeza.position.y, rig.position.y + 1.2f);
        Vector3 pos = cabeza.position + frente * distanciaPaneles;
        pos.y = alturaOjos;
        panel.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(frente, Vector3.up));
        panel.SetActive(true);
        // Con Time.timeScale = 0 no corre FixedUpdate: se sincronizan los colliders para que el laser acierte en la pausa.
        Physics.SyncTransforms();
        ActualizarLaser();
    }

    static string Formato(float segundos)
    {
        int s = Mathf.FloorToInt(Mathf.Max(0f, segundos));
        return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
