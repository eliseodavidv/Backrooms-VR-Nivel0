using UnityEngine;

// Evita atravesar paredes caminando fisicamente con el visor. Si la cabeza (CenterEyeAnchor) queda dentro de la
// geometria del laberinto, o cruza una pared entre un frame y el siguiente, la pantalla se funde a negro y se muestra
// "Vuelve atras". Cuando la cabeza vuelve al lado correcto, el fundido se retira.
// No afecta al teletransporte (un salto brusco de la cabeza reinicia la referencia) ni a los nichos.
[DefaultExecutionOrder(20000)] // al final del frame: la pose de la cabeza (tracking / EditorEyeHeight) ya esta aplicada
public class HeadWallGuard : MonoBehaviour
{
    [Header("Referencias")]
    public Transform cabeza;
    [Tooltip("Raiz del laberinto: solo sus colliders cuentan como pared (la entidad no ennegrece la vista).")]
    public Transform laberinto;
    public GameManager gm;
    public Renderer fundido;
    public GameObject mensaje;

    [Header("Ajustes")]
    [Tooltip("Radio de la esfera de la cabeza (m): se funde cuando una pared esta a menos de esta distancia.")]
    public float radioCabeza = 0.09f;
    [Tooltip("Salto horizontal de la cabeza en un frame que se considera teletransporte / reposicion, no caminar.")]
    public float saltoTeletransporte = 0.6f;
    public float duracionFundidoEntrada = 0.12f;
    public float duracionFundidoSalida = 0.30f;

    public bool bloqueado { get; private set; }
    public float alfa { get; private set; }

    private static readonly Collider[] colisiones = new Collider[24];
    private static readonly RaycastHit[] golpes = new RaycastHit[16];
    private Material material;
    private Vector3 previa;
    private Vector3 ultimaSegura;
    private bool inicializado;

    void Awake()
    {
        if (fundido != null)
        {
            material = fundido.material;
            fundido.enabled = false;
        }
        if (mensaje != null) mensaje.SetActive(false);
    }

    void LateUpdate()
    {
        if (cabeza == null) return;
        Vector3 h = cabeza.position;

        // Solo durante la partida; en menus y pantallas de fin no hay fundido.
        bool activo = gm == null || gm.fase == GameManager.Fase.Jugando;
        if (!activo)
        {
            Reiniciar(h);
            bloqueado = false;
            AplicarAlfa(0f, true);
            return;
        }

        if (!inicializado) Reiniciar(h);

        // Teletransporte o reposicion por codigo: la cabeza salta; se toma la nueva posicion como segura.
        Vector3 d = h - previa;
        d.y = 0f;
        if (d.magnitude > saltoTeletransporte) Reiniciar(h);
        previa = h;

        bool dentro = DentroDeGeometria(h);
        bool cruzo = !dentro && CruzaPared(ultimaSegura, h);
        bloqueado = dentro || cruzo;
        if (!bloqueado) ultimaSegura = h;

        AplicarAlfa(bloqueado ? 1f : 0f, false);
    }

    void Reiniciar(Vector3 h)
    {
        previa = h;
        ultimaSegura = h;
        inicializado = true;
    }

    bool DentroDeGeometria(Vector3 h)
    {
        int n = Physics.OverlapSphereNonAlloc(h, radioCabeza, colisiones, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = colisiones[i];
            if (col != null && (laberinto == null || col.transform.IsChildOf(laberinto))) return true;
        }
        return false;
    }

    bool CruzaPared(Vector3 desde, Vector3 hasta)
    {
        Vector3 v = hasta - desde;
        float dist = v.magnitude;
        if (dist < 0.005f) return false;
        int n = Physics.RaycastNonAlloc(desde, v / dist, golpes, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = golpes[i].collider;
            if (col != null && (laberinto == null || col.transform.IsChildOf(laberinto))) return true;
        }
        return false;
    }

    void AplicarAlfa(float objetivo, bool inmediato)
    {
        float dur = objetivo > alfa ? duracionFundidoEntrada : duracionFundidoSalida;
        alfa = inmediato ? objetivo : Mathf.MoveTowards(alfa, objetivo, Time.unscaledDeltaTime / Mathf.Max(0.01f, dur));

        if (material != null)
        {
            var c = material.color;
            c.a = alfa;
            material.color = c;
            fundido.enabled = alfa > 0.001f;
        }
        if (mensaje != null) mensaje.SetActive(alfa > 0.6f);
    }
}
