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

        jugadorEscondido = NichoZona.JugadorEscondido(jugador, alturaAgachado);
        float distancia = DistanciaPlana(jugador.position);
        bool lineaDeVision = !jugadorEscondido && distancia <= radioSospecha && TieneLineaDeVision();
        bool veEnCono = lineaDeVision && EnCono();

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
                break;
        }

        Debug.Log("[EntityAI] " + anterior + " -> " + nuevo + " (" + motivo + ")");
        OnCambioEstado?.Invoke(nuevo);
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
        if (elMasCercano)
        {
            float mejor = float.MaxValue;
            for (int i = 0; i < puntosDePatrulla.Length; i++)
            {
                float d = (puntosDePatrulla[i].position - transform.position).sqrMagnitude;
                if (d < mejor) { mejor = d; indicePatrulla = i; }
            }
        }
        IrA(puntosDePatrulla[indicePatrulla].position);
    }

    void ActualizarPatrulla()
    {
        if (puntosDePatrulla == null || puntosDePatrulla.Length == 0) return;
        if (!agente.pathPending && agente.remainingDistance < 0.5f)
        {
            indicePatrulla = (indicePatrulla + 1) % puntosDePatrulla.Length;
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
