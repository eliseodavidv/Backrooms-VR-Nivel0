using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
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

    [Header("Menu de inicio (Opciones / Creditos / ambiente)")]
    public GameObject panelOpciones;
    public GameObject panelCreditos;
    public TMP_Text opcionesVolumenTexto;
    public RectTransform opcionesBarraRelleno;
    public float opcionesBarraAncho = 400f;
    [Tooltip("Los paneles del menu de inicio se muestran a esta distancia (m).")]
    public float distanciaMenu = 2f;
    [Tooltip("Zumbido fluorescente en bucle mientras se ve el menu.")]
    public AudioSource ambienteMenu;
    [Tooltip("Vineta pegada a la camara, solo en el menu.")]
    public GameObject vineta;
    public Color nieblaMenuColor = new Color(0.05f, 0.04f, 0.015f);
    public float nieblaMenuDensidad = 0.16f;
    [Tooltip("Luz ambiente (plana) mientras se ve el menu: casi penumbra.")]
    public Color luzAmbienteMenu = new Color(0.02f, 0.017f, 0.01f);
    [Tooltip("Silueta, grano, susurros y parpadeo del menu.")]
    public AmbienteMenuTerror terror;
    [Tooltip("Mesas, armas y rotulos: se ocultan mientras el menu esta abierto y aparecen al empezar la partida.")]
    public GameObject[] ocultosEnMenu;
    public TMP_Text pausaVolumenTMP;

    [Header("Pantalla de fin")]
    public Text finTitulo;
    public Text finResumen;
    public Text finDetalle;
    public Text finReintentarTexto;
    public VRButton botonVotoMeGusta;
    public VRButton botonVotoNoMeGusta;
    [Tooltip("Version TextMeshPro (estilo oscuro y sucio) de los textos de la pantalla de fin. Si estan asignados, se usan en vez de los Text antiguos.")]
    public TMP_Text finTituloTMP;
    public TMP_Text finResumenTMP;
    public TMP_Text finDetalleTMP;
    public TMP_Text finReintentarTMP;
    public Color colorVictoria = new Color(0.45f, 0.62f, 0.38f);
    public Color colorDerrota = new Color(0.80f, 0.13f, 0.09f);

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

    // La fase avisa al cambiar (lo usa el indicador de vidas para mostrarse solo durante la partida).
    public event System.Action<Fase> OnFaseCambiada;
    private Fase faseValor = Fase.Menu;
    public Fase fase
    {
        get { return faseValor; }
        private set
        {
            if (faseValor == value) return;
            faseValor = value;
            OnFaseCambiada?.Invoke(value);
        }
    }
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
        // Audio de la partida (drone, susurros, whoosh, victoria/derrota) y sonidos generados listos antes de jugar.
        SonidosPlaceholder.Precalentar();
        AmbienteJuego.Asegurar();

        // El tracking del visor no es fiable en el primer frame: se prepara todo ya y se muestra el menu un instante despues.
        fase = Fase.Menu;
        PrepararSinPartida();
        if (reloj != null) reloj.gameObject.SetActive(false);
        yield return new WaitForSeconds(0.3f);

        // Tras INICIAR DE NUEVO la escena se recargo: se empieza la partida directo (sin pasar por el menu de inicio).
        if (EmpezarDirecto)
        {
            EmpezarDirecto = false;
            RecentrarVisor();
            IniciarPartida();
            Debug.Log("[GameManager] Escena recargada: partida nueva desde cero.");
        }
        else MostrarMenu();
    }

    // Bandera que sobrevive a la recarga de la escena (los estaticos no se reinician con LoadScene).
    public static bool EmpezarDirecto;
    private bool recargando;

    // En el visor, la cabeza real puede estar lejos del origen o girada: se recentra para que quede en el punto de inicio mirando al pasillo.
    // Sin visor (editor) no hace nada.
    static void RecentrarVisor()
    {
        try
        {
            if (OVRManager.display != null) OVRManager.display.RecenterPose();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[GameManager] No se pudo recentrar el visor: " + e.Message);
        }
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
        AtmosferaMenu(true);
        Mostrar(panelMenu, distanciaMenu);
    }

    // Menu de inicio -> Opciones / Creditos (la fase sigue siendo Menu; "Volver" regresa al menu).
    public void AbrirOpciones()
    {
        OcultarPaneles();
        ActualizarVolumenUI();
        Mostrar(panelOpciones, distanciaMenu);
    }

    public void AbrirCreditos()
    {
        OcultarPaneles();
        Mostrar(panelCreditos, distanciaMenu);
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
        AtmosferaMenu(false);

        ColocarJugadorEnInicio();
        entidad.gameObject.SetActive(true);
        var agente = entidad.GetComponent<NavMeshAgent>();
        agente.Warp(posInicioEntidad);
        entidad.transform.rotation = rotInicioEntidad;
        entidad.Reiniciar();
        ReiniciarComponentes();

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

    // Todo lo que no depende de las variables propias del GameManager vuelve a su estado inicial: vidas (3), mini monstruos
    // vivos en su sitio y vagando, armas en las mesas (aunque estuvieran en la mano), borde rojo y latido apagados,
    // paneles fluorescentes estables. La entidad, la posicion del jugador, el tiempo y las estadisticas ya se
    // reinician en IniciarPartida. Se llama desde IniciarPartida, asi tambien limpia la primera partida.
    void ReiniciarComponentes()
    {
        var salud = FindObjectOfType<SaludJugador>(true);
        if (salud != null) salud.ReiniciarPartida();
        foreach (var m in FindObjectsOfType<MiniMonstruo>(true)) m.ReiniciarPartida();
        foreach (var a in FindObjectsOfType<ArmaAgarrable>(true)) a.ReiniciarPartida();
        foreach (var p in FindObjectsOfType<PursuitFeedback>(true)) p.Reiniciar();
        foreach (var f in FindObjectsOfType<FluorescentFlicker>(true)) f.Reiniciar();
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
        if (AmbienteJuego.Instancia != null) AmbienteJuego.Instancia.Final(victoria, causa);   // sonido breve de victoria / derrota

        string vez = detectado == 1 ? "vez" : "veces";
        string tTitulo, tResumen, tBoton;
        Color cTitulo;
        if (victoria)
        {
            tTitulo = "ESCAPASTE";
            cTitulo = colorVictoria;
            tResumen = "Tiempo: " + Formato(tiempoJugado) + "   ·   Detectado: " + detectado + " " + vez;
            tBoton = "JUGAR DE NUEVO";
        }
        else
        {
            tTitulo = causa == "tiempo" ? "SE ACABÓ EL TIEMPO" : causa == "monstruo" ? "TE DEVORARON" : "TE ENCONTRÓ";
            cTitulo = colorDerrota;
            tResumen = "Tiempo sobrevivido: " + Formato(tiempoJugado) + "   ·   Detectado: " + detectado + " " + vez;
            tBoton = "INICIAR DE NUEVO";   // reinicia TODO desde cero (vidas, tiempo, posiciones...), no continua la partida anterior
        }
        string tDetalle = "Teletransportes: " + teletransportes + "   ·   Escondites: " + escondites + "   ·   Mini monstruos: " + minisMuertos;
        if (finTitulo != null) { finTitulo.text = tTitulo; finTitulo.color = cTitulo; }
        if (finResumen != null) finResumen.text = tResumen;
        if (finReintentarTexto != null) finReintentarTexto.text = tBoton;
        if (finDetalle != null) finDetalle.text = tDetalle;
        if (finTituloTMP != null) { finTituloTMP.text = tTitulo; finTituloTMP.color = cTitulo; }
        if (finResumenTMP != null) finResumenTMP.text = tResumen;
        if (finDetalleTMP != null) finDetalleTMP.text = tDetalle;
        if (finReintentarTMP != null) finReintentarTMP.text = tBoton;
        botonVotoMeGusta.SetSeleccionado(false);
        botonVotoNoMeGusta.SetSeleccionado(false);

        Mostrar(panelFin, distanciaMenu);
        Debug.Log("[GameManager] Fin: " + (victoria ? "VICTORIA" : "DERROTA") + " (" + causa + ") a los " + Formato(tiempoJugado));
    }

    // Boton "INICIAR DE NUEVO": partida completa desde cero (ver ReiniciarComponentes). El resultado de la partida
    // anterior ya se guardo en el CSV al terminar.
    public void IniciarDeNuevo()
    {
        if (recargando) return;
        Debug.Log("[GameManager] INICIAR DE NUEVO: se recarga la escena completa.");
        StartCoroutine(RecargarEscena());
    }

    // Recarga Backrooms_Main para que todo quede como recien abierto (jugador, locomotor del SDK, entidad, mini monstruos,
    // armas, luces horneadas, audio, niebla, borde rojo). En el visor, reposicionar solo el rig no bastaba.
    IEnumerator RecargarEscena()
    {
        recargando = true;

        // 1) Si la partida seguia en curso (por ejemplo desde la pausa) se guarda como abandonada. Si ya termino (derrota o
        //    victoria) su resultado se guardo en Terminar(), antes de mostrar la pantalla de fin.
        if (fase == Fase.Jugando || (fase == Fase.Pausa && faseAnterior == Fase.Jugando))
        {
            float jugado = duracion - Mathf.Max(0f, tiempoRestante);
            StatsLogger.Guardar("derrota", "reinicio", jugado, detectado, teletransportes, escondites, minisMuertos, voto);
        }

        // 2) Nada queda en las manos ni en marcha: se sueltan las armas, se apaga la locomocion y el laser, y se restauran tiempo y audio.
        foreach (var a in FindObjectsOfType<ArmaAgarrable>(true)) a.SoltarAhora();
        SetLocomocion(false);
        if (laser != null) laser.activo = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        // Dos cuadros para que el SDK procese las soltadas antes de destruir la escena.
        yield return null;
        yield return null;

        // 3) Recarga. El volumen ya esta en PlayerPrefs y se vuelve a aplicar en Start.
        EmpezarDirecto = true;
        var escena = SceneManager.GetActiveScene();
        if (escena.buildIndex >= 0) SceneManager.LoadScene(escena.buildIndex);
        else SceneManager.LoadScene(escena.name);
    }

    public void Reintentar() { IniciarDeNuevo(); }

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
        if (panelOpciones != null) panelOpciones.SetActive(false);
        if (panelCreditos != null) panelCreditos.SetActive(false);
        if (faseAnterior == Fase.Jugando)
        {
            Time.timeScale = 0f;
            AudioListener.pause = true;
            SetLocomocion(false);
        }
        ActualizarVolumenUI();
        Mostrar(panelPausa, distanciaMenu);
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
            case Fase.Menu: Mostrar(panelMenu, distanciaMenu); break;
            case Fase.Tutorial: Mostrar(panelTutorial); break;
            case Fase.Fin: Mostrar(panelFin, distanciaMenu); break;
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
        string pct = Mathf.RoundToInt(volumen * 100f) + "%";
        if (opcionesVolumenTexto != null) opcionesVolumenTexto.text = pct;
        if (pausaVolumenTMP != null) pausaVolumenTMP.text = pct;
        if (opcionesBarraRelleno != null) opcionesBarraRelleno.sizeDelta = new Vector2(opcionesBarraAncho * volumen, opcionesBarraRelleno.sizeDelta.y);
    }

    // ---------- ambiente del menu de inicio: zumbido, niebla oscura y vineta ----------

    private bool nieblaGuardada;
    private Color nieblaColorOriginal, ambienteOriginal;
    private float nieblaDensidadOriginal;

    void AtmosferaMenu(bool activa)
    {
        if (!nieblaGuardada)
        {
            nieblaGuardada = true;
            nieblaColorOriginal = RenderSettings.fogColor;
            nieblaDensidadOriginal = RenderSettings.fogDensity;
            ambienteOriginal = RenderSettings.ambientLight;
        }
        if (activa)
        {
            RenderSettings.fogColor = nieblaMenuColor;
            RenderSettings.fogDensity = nieblaMenuDensidad;
            RenderSettings.ambientLight = luzAmbienteMenu;
        }
        else
        {
            RenderSettings.fogColor = nieblaColorOriginal;
            RenderSettings.fogDensity = nieblaDensidadOriginal;
            RenderSettings.ambientLight = ambienteOriginal;
        }
        // Mesas, armas y rotulos: solo existen en la partida.
        if (ocultosEnMenu != null)
            foreach (var o in ocultosEnMenu) if (o != null) o.SetActive(!activa);
        if (terror != null) terror.Activar(activa);
        if (vineta != null) vineta.SetActive(activa);
        if (ambienteMenu != null)
        {
            if (activa && !ambienteMenu.isPlaying) ambienteMenu.Play();
            else if (!activa) ambienteMenu.Stop();
        }
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
        if (panelOpciones != null) panelOpciones.SetActive(false);
        if (panelCreditos != null) panelCreditos.SetActive(false);
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
    void Mostrar(GameObject panel, float distancia = -1f)
    {
        if (distancia <= 0f) distancia = distanciaPaneles;
        Vector3 frente = cabeza.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.01f) frente = rig.forward;
        frente.Normalize();

        // Altura de los ojos: si el tracking aun no llego (cabeza a ras del suelo) se usa una altura de pie por defecto.
        float alturaOjos = Mathf.Max(cabeza.position.y, rig.position.y + 1.2f);
        Vector3 pos = cabeza.position + frente * distancia;
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
