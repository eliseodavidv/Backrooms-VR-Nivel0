using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Entidad de los Backrooms. Maquina de estados de la figura 8 del informe:
// Patrulla -> Sospecha -> Persecucion -> Busqueda -> Patrulla, y Persecucion -> Captura (< 1 m).
[RequireComponent(typeof(NavMeshAgent))]
public class EntityAI : MonoBehaviour
{
    public enum Estado { Patrulla, Sospecha, Persecucion, Busqueda, Captura }

    [Header("Referencias")]
    public Transform[] puntosDePatrulla;
    public Transform jugador; // CenterEyeAnchor

    [Header("Vision")]
    [Tooltip("Distancia maxima para pasar a Persecucion (con linea de vision).")]
    public float radioDeteccion = 8f;
    [Tooltip("Distancia maxima para notar algo lejos y pasar a Sospecha.")]
    public float radioSospecha = 16f;
    public float anguloVision = 100f;
    public LayerMask capasObstaculo = ~0;
    [Tooltip("Altura de los ojos de la entidad sobre su pivote.")]
    public float alturaOjos = 1.2f;

    [Header("Oido")]
    public float radioOido = 15f;
    public int teletransportesParaRuido = 2;
    public float ventanaRuido = 8f;

    [Header("Escondite")]
    [Tooltip("Altura maxima de la cabeza (CenterEyeAnchor) para considerar al jugador agachado.")]
    public float alturaAgachado = 1.1f;

    [Header("Escondite: comportamiento de la amenaza")]
    [Tooltip("Si pierde al jugador porque se escondio, mira alrededor de su ultimo punto conocido entre estos dos tiempos (s) y luego se aleja.")]
    public Vector2 tiempoMirarAlrededor = new Vector2(4f, 6f);
    [Tooltip("Distancia minima (m) al nicho de los puntos de patrulla a los que se aleja.")]
    public float distanciaAlejarse = 10f;
    [Tooltip("Tiempo (s) que no vuelve a ese nicho desde que el jugador estuvo escondido en el.")]
    public float tiempoEvitarNicho = 20f;
    [Tooltip("Si el jugador sale o se levanta a menos de esta distancia y con vision, lo vuelve a detectar aunque no lo mire de frente.")]
    public float radioRedeteccionCercana = 3f;

    [Header("Velocidades")]
    public float velocidadPatrulla = 1.5f;
    public float velocidadSospecha = 2.0f;
    public float velocidadBusqueda = 2.2f;
    public float velocidadPersecucion = 3.2f;

    [Header("Tiempos")]
    public float tiempoSospecha = 5f;
    public float tiempoBusqueda = 10f;

    [Header("Captura")]
    public float distanciaCaptura = 1f;
    [Tooltip("Distancia de captura cuando el jugador esta en un nicho y la entidad no puede acercarse mas.")]
    public float distanciaCapturaNicho = 2f;

    public Estado estadoActual { get; private set; } = Estado.Patrulla;
    public bool jugadorEscondido { get; private set; }
    public Vector3 ultimaPosicionConocida { get; private set; }

    public System.Action<Estado> OnCambioEstado;
    public System.Action OnCaptura;

    private NavMeshAgent agente;
    private int indicePatrulla = 0;
    private float inicioEstado;
    private float ultimoEstimulo;
    private float proximaExploracion;
    private readonly List<float> tiemposTeletransporte = new List<float>();

    // Escondite: ultimo nicho donde estuvo el jugador y hasta cuando se evita; la busqueda por escondite dura pocos segundos.
    private NichoZona nichoEvitado;
    private float evitarHasta;
    private bool buscaPorEscondite;
    private float finMirada;
    private int patrullaForzada = -1;

    private Renderer[] mallas;
    private Vector3 posInicial;
    private Quaternion rotInicial;

    public bool mallaVisible { get; private set; } = true;

    void Awake()
    {
        agente = GetComponent<NavMeshAgent>();
        mallas = GetComponentsInChildren<Renderer>(true);
        // Punto de patrulla inicial: ahi vuelve a aparecer tras Reiniciar().
        posInicial = transform.position;
        rotInicial = transform.rotation;
    }

    void OnEnable() { PlayerNoiseEmitter.Teletransporte += AlTeletransportarse; }
    void OnDisable() { PlayerNoiseEmitter.Teletransporte -= AlTeletransportarse; }

    void Start()
    {
        agente.speed = velocidadPatrulla;
        inicioEstado = Time.time;
        IrAPuntoDePatrulla();
    }

    void Update()
    {
        if (estadoActual == Estado.Captura) return;

        if (jugador == null)
        {
            ActualizarPatrulla();
            return;
        }

        var nichoActual = NichoZona.NichoDelJugador(jugador, alturaAgachado);
        jugadorEscondido = nichoActual != null;
        if (jugadorEscondido)
        {
            // Mientras esta escondido (y 'tiempoEvitarNicho' s despues) no patrulla ni busca cerca de ese nicho.
            nichoEvitado = nichoActual;
            evitarHasta = Time.time + tiempoEvitarNicho;
            if (estadoActual == Estado.Busqueda && !buscaPorEscondite) IniciarMirada();
        }
        bool evitando = nichoEvitado != null && Time.time < evitarHasta;
        float distancia = DistanciaPlana(jugador.position);
        bool lineaDeVision = !jugadorEscondido && distancia <= radioSospecha && TieneLineaDeVision();
        // Tras un escondite, si el jugador sale a menos de ~3 m y con vision, lo detecta aunque no este de frente.
        bool veEnCono = lineaDeVision && (EnCono() || (evitando && distancia <= radioRedeteccionCercana));

        switch (estadoActual)
        {
            case Estado.Patrulla:
                if (veEnCono && distancia <= radioDeteccion)
                    Cambiar(Estado.Persecucion, "lo ve a " + distancia.ToString("F1") + " m");
                else if (veEnCono)
                    EntrarEnSospecha(jugador.position, "ve algo lejos (" + distancia.ToString("F1") + " m)");
                else
                    ActualizarPatrulla();
                break;

            case Estado.Sospecha:
                if (veEnCono && distancia <= radioDeteccion)
                {
                    Cambiar(Estado.Persecucion, "lo ve a " + distancia.ToString("F1") + " m");
                }
                else
                {
                    if (veEnCono) { ultimaPosicionConocida = jugador.position; ultimoEstimulo = Time.time; IrA(ultimaPosicionConocida); }
                    if (Time.time - ultimoEstimulo >= tiempoSospecha)
                        Cambiar(Estado.Patrulla, "nada en " + tiempoSospecha + " s");
                }
                break;

            case Estado.Persecucion:
                // La alcoba de un nicho no es caminable: si la entidad llega al borde del NavMesh y el jugador
                // sigue visible a corta distancia, tambien lo captura.
                bool llegoAlFinal = !agente.pathPending && agente.remainingDistance <= 0.6f;
                if (!jugadorEscondido && (distancia <= distanciaCaptura || (llegoAlFinal && distancia <= distanciaCapturaNicho)))
                {
                    Capturar();
                }
                else if (!lineaDeVision)
                {
                    Cambiar(Estado.Busqueda, jugadorEscondido ? "jugador escondido" : "pierde la linea de vision");
                }
                else
                {
                    ultimaPosicionConocida = jugador.position;
                    IrA(ultimaPosicionConocida);
                }
                break;

            case Estado.Busqueda:
                if (lineaDeVision && distancia <= radioDeteccion)
                    Cambiar(Estado.Persecucion, "lo vuelve a ver a " + distancia.ToString("F1") + " m");
                else if (buscaPorEscondite && Time.time >= finMirada)
                    AlejarseDelNicho();
                else if (Time.time - inicioEstado >= tiempoBusqueda)
                    Cambiar(Estado.Patrulla, "10 s sin encontrarlo");
                else
                    ActualizarBusqueda();
                break;
        }
    }

    // ---------- estados ----------

    void Cambiar(Estado nuevo, string motivo)
    {
        var anterior = estadoActual;
        estadoActual = nuevo;
        inicioEstado = Time.time;
        agente.isStopped = false;

        switch (nuevo)
        {
            case Estado.Patrulla:
                agente.speed = velocidadPatrulla;
                IrAPuntoDePatrulla(true);
                break;
            case Estado.Sospecha:
                agente.speed = velocidadSospecha;
                ultimoEstimulo = Time.time;
                IrA(ultimaPosicionConocida);
                break;
            case Estado.Persecucion:
                agente.speed = velocidadPersecucion;
                ultimaPosicionConocida = jugador.position;
                IrA(ultimaPosicionConocida);
                break;
            case Estado.Busqueda:
                agente.speed = velocidadBusqueda;
                proximaExploracion = Time.time + 2f;
                IrA(ultimaPosicionConocida);
                buscaPorEscondite = false;
                if (jugadorEscondido) IniciarMirada();
                break;
        }
        if (nuevo != Estado.Busqueda) buscaPorEscondite = false;

        Debug.Log("[EntityAI] " + anterior + " -> " + nuevo + " (" + motivo + ")");
        OnCambioEstado?.Invoke(nuevo);
    }

    // Perdio al jugador porque se escondio: mira alrededor de su ultimo punto conocido unos 4-6 s y luego se aleja.
    void IniciarMirada()
    {
        buscaPorEscondite = true;
        finMirada = Time.time + Random.Range(tiempoMirarAlrededor.x, tiempoMirarAlrededor.y);
    }

    // Se va a un punto de patrulla lejano (>= distanciaAlejarse del nicho). Cambiar(Patrulla) respeta el punto elegido.
    void AlejarseDelNicho()
    {
        patrullaForzada = ElegirPuntoLejano();
        Cambiar(Estado.Patrulla, "jugador escondido: se aleja del nicho (>= " + distanciaAlejarse + " m)");
    }

    // Punto de patrulla lejano al nicho evitado: uno al azar de los validos; si ninguno lo es, el mas lejano.
    int ElegirPuntoLejano()
    {
        if (puntosDePatrulla == null || puntosDePatrulla.Length == 0 || nichoEvitado == null) return -1;
        var validos = new List<int>();
        int masLejano = 0;
        float mayor = -1f;
        for (int i = 0; i < puntosDePatrulla.Length; i++)
        {
            float d = DistanciaPlanaEntre(puntosDePatrulla[i].position, nichoEvitado.Centro);
            if (d >= distanciaAlejarse) validos.Add(i);
            if (d > mayor) { mayor = d; masLejano = i; }
        }
        return validos.Count > 0 ? validos[Random.Range(0, validos.Count)] : masLejano;
    }

    // Durante el tiempo de evitacion, un punto de patrulla cerca del nicho no se usa.
    bool Evita(Vector3 punto)
    {
        return nichoEvitado != null && Time.time < evitarHasta && DistanciaPlanaEntre(punto, nichoEvitado.Centro) < distanciaAlejarse;
    }

    static float DistanciaPlanaEntre(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    void EntrarEnSospecha(Vector3 donde, string motivo)
    {
        ultimaPosicionConocida = donde;
        if (estadoActual == Estado.Sospecha)
        {
            ultimoEstimulo = Time.time;
            IrA(donde);
            return;
        }
        Cambiar(Estado.Sospecha, motivo);
    }

    void Capturar()
    {
        estadoActual = Estado.Captura;
        agente.isStopped = true;
        agente.velocity = Vector3.zero;
        MostrarMalla(false); // queda frente a la cara del jugador: se oculta para no tapar la pantalla de derrota
        Debug.Log("[EntityAI] Persecucion -> Captura (< " + distanciaCaptura + " m). Fin de la partida: derrota (pantalla en E4).");
        OnCambioEstado?.Invoke(Estado.Captura);
        OnCaptura?.Invoke();
    }

    // Para reintentar la partida (E4): vuelve a Patrulla desde cualquier estado, incluida la Captura.
    public void Reiniciar()
    {
        tiemposTeletransporte.Clear();
        jugadorEscondido = false;
        nichoEvitado = null;
        evitarHasta = 0f;
        buscaPorEscondite = false;
        patrullaForzada = -1;
        MostrarMalla(true);
        // Vuelve a su punto de patrulla inicial.
        if (agente != null && agente.enabled && gameObject.activeInHierarchy)
        {
            agente.Warp(posInicial);
            transform.rotation = rotInicial;
        }
        Cambiar(Estado.Patrulla, "reinicio");
    }

    void MostrarMalla(bool visible)
    {
        mallaVisible = visible;
        if (mallas == null) return;
        foreach (var r in mallas)
            if (r != null) r.enabled = visible;
    }

    // ---------- movimiento ----------

    void IrA(Vector3 destino)
    {
        // Si el destino cae en zona no caminable (p. ej. un nicho), va al punto mas cercano del NavMesh.
        if (NavMesh.SamplePosition(destino, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            agente.SetDestination(hit.position);
        else
            agente.SetDestination(destino);
    }

    void IrAPuntoDePatrulla(bool elMasCercano = false)
    {
        if (puntosDePatrulla == null || puntosDePatrulla.Length == 0) return;
        if (patrullaForzada >= 0 && patrullaForzada < puntosDePatrulla.Length)
        {
            indicePatrulla = patrullaForzada;      // se aleja de un escondite: va a ese punto lejano
            patrullaForzada = -1;
        }
        else if (elMasCercano)
        {
            float mejor = float.MaxValue;
            for (int i = 0; i < puntosDePatrulla.Length; i++)
            {
                if (Evita(puntosDePatrulla[i].position)) continue;
                float d = (puntosDePatrulla[i].position - transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; indicePatrulla = i; }
            }
            if (mejor == float.MaxValue) indicePatrulla = ElegirPuntoLejano();   // todos cerca del nicho: el mas lejano
        }
        IrA(puntosDePatrulla[indicePatrulla].position);
    }

    void ActualizarPatrulla()
    {
        if (puntosDePatrulla == null || puntosDePatrulla.Length == 0) return;
        if (!agente.pathPending && agente.remainingDistance < 0.5f)
        {
            // Siguiente punto de la ronda, saltando los que quedan cerca del nicho mientras se evita.
            int n = puntosDePatrulla.Length;
            int siguiente = (indicePatrulla + 1) % n;
            for (int k = 0; k < n && Evita(puntosDePatrulla[siguiente].position); k++) siguiente = (siguiente + 1) % n;
            indicePatrulla = siguiente;
            IrA(puntosDePatrulla[indicePatrulla].position);
        }
        else if (nichoEvitado != null && Time.time < evitarHasta && Evita(puntosDePatrulla[indicePatrulla].position))
        {
            // Iba hacia un punto que quedo cerca del nicho evitado: cambia a uno lejano.
            indicePatrulla = ElegirPuntoLejano();
            IrA(puntosDePatrulla[indicePatrulla].position);
        }
    }

    void ActualizarBusqueda()
    {
        // Revisa los alrededores de la ultima posicion conocida.
        if (!agente.pathPending && agente.remainingDistance < 0.5f && Time.time >= proximaExploracion)
        {
            Vector3 al = ultimaPosicionConocida + Random.insideUnitSphere * 6f;
            al.y = ultimaPosicionConocida.y;
            if (NavMesh.SamplePosition(al, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                agente.SetDestination(hit.position);
            proximaExploracion = Time.time + 2f;
        }
    }

    // ---------- percepcion ----------

    float DistanciaPlana(Vector3 p)
    {
        Vector3 d = p - transform.position;
        d.y = 0f;
        return d.magnitude;
    }

    bool EnCono()
    {
        Vector3 haciaJugador = jugador.position - transform.position;
        haciaJugador.y = 0f;
        if (haciaJugador.sqrMagnitude < 0.01f) return true;
        Vector3 frente = transform.forward;
        frente.y = 0f;
        return Vector3.Angle(frente, haciaJugador) <= anguloVision * 0.5f;
    }

    bool TieneLineaDeVision()
    {
        Vector3 ojos = transform.position + Vector3.up * alturaOjos;
        Vector3 objetivo = jugador.position;
        if (Physics.Linecast(ojos, objetivo, out RaycastHit hit, capasObstaculo, QueryTriggerInteraction.Ignore))
        {
            // Los colliders del propio jugador (rig) o de la entidad no cuentan como obstaculo.
            if (hit.transform.IsChildOf(jugador.root) || hit.transform.IsChildOf(transform)) return true;
            return false;
        }
        return true;
    }

    void AlTeletransportarse(Vector3 destino)
    {
        if (estadoActual == Estado.Captura || estadoActual == Estado.Persecucion) return;

        float ahora = Time.time;
        tiemposTeletransporte.Add(ahora);
        tiemposTeletransporte.RemoveAll(t => ahora - t > ventanaRuido);

        float distancia = DistanciaPlana(destino);
        if (tiemposTeletransporte.Count >= teletransportesParaRuido && distancia <= radioOido)
            EntrarEnSospecha(destino, "oye teletransportes seguidos a " + distancia.ToString("F1") + " m");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radioDeteccion);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioSospecha);
        Vector3 izquierda = Quaternion.Euler(0, -anguloVision * 0.5f, 0) * transform.forward * radioSospecha;
        Vector3 derecha = Quaternion.Euler(0, anguloVision * 0.5f, 0) * transform.forward * radioSospecha;
        Gizmos.DrawLine(transform.position, transform.position + izquierda);
        Gizmos.DrawLine(transform.position, transform.position + derecha);
    }
}
