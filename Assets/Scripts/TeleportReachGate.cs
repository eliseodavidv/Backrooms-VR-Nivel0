using System.Collections.Generic;
using Oculus.Interaction.Locomotion;
using UnityEngine;
using UnityEngine.AI;

// Un hotspot solo es un destino valido si el jugador puede llegar a el de verdad:
//   - hay linea de vista libre desde la cabeza hasta el hotspot, a altura de cabeza (sin paredes en medio), O
//   - hay un camino real por el NavMesh de como maximo 'caminoMaximo' metros.
// Si no, el arco se dibuja como invalido y el teletransporte no ocurre (hook AcceptDestination del SDK de Meta).
// Evita elegir hotspots al otro lado de una pared (el arco los alcanza por encima del muro).
[DefaultExecutionOrder(-10)]
public class TeleportReachGate : MonoBehaviour
{
    [Tooltip("CenterEyeAnchor.")]
    public Transform cabeza;
    [Tooltip("Raiz del laberinto: solo sus colliders cuentan como pared (la entidad y los hotspots no bloquean la vista).")]
    public Transform laberinto;
    [Tooltip("Longitud maxima del camino por el NavMesh (m) cuando no hay linea de vista.")]
    public float caminoMaximo = 12f;
    public float radioMuestraJugador = 2f;
    [Tooltip("Radio para ubicar el hotspot en el NavMesh (los nichos no son caminables: se usa el punto mas cercano).")]
    public float radioMuestraDestino = 2.6f;
    public bool registrarRechazos = true;

    private static readonly RaycastHit[] golpes = new RaycastHit[32];
    private NavMeshPath camino; // NavMeshPath no se puede crear en un inicializador de campo de un MonoBehaviour

    private struct Cache { public int frame; public bool ok; }
    private readonly Dictionary<int, Cache> cache = new Dictionary<int, Cache>();
    private float ultimoLog;

    void Awake() { camino = new NavMeshPath(); }

    void Start()
    {
        int n = 0;
        foreach (var ti in GetComponentsInChildren<TeleportInteractor>(true))
        {
            ti.AcceptDestination = Aceptar;
            n++;
        }
        Debug.Log("[TeleportReachGate] Filtro instalado en " + n + " interactores de teletransporte.");
    }

    // Delegate del SDK: true = permite el teletransporte a este destino.
    public bool Aceptar(TeleportInteractable hotspot, Pose destino)
    {
        if (hotspot == null || cabeza == null) return true;

        // El SDK pregunta varias veces por frame (arco, reticula, seleccion): se cachea por hotspot y frame.
        int id = hotspot.GetInstanceID();
        if (cache.TryGetValue(id, out Cache c) && c.frame == Time.frameCount) return c.ok;

        bool ok = PuedeLlegar(cabeza.position, destino.position, out string motivo);
        cache[id] = new Cache { frame = Time.frameCount, ok = ok };

        if (!ok && registrarRechazos && Time.unscaledTime - ultimoLog > 1f)
        {
            ultimoLog = Time.unscaledTime;
            Debug.Log("[TeleportReachGate] Destino rechazado (" + hotspot.name + "): " + motivo);
        }
        return ok;
    }

    public bool PuedeLlegar(Vector3 desdeCabeza, Vector3 destino, out string motivo)
    {
        // 1) Linea de vista libre a altura de cabeza.
        Vector3 objetivo = new Vector3(destino.x, desdeCabeza.y, destino.z);
        if (LineaLibre(desdeCabeza, objetivo))
        {
            motivo = "linea de vista";
            return true;
        }

        // 2) Camino real por el NavMesh, de longitud acotada.
        float largo = LongitudCamino(desdeCabeza, destino);
        if (largo >= 0f && largo <= caminoMaximo)
        {
            motivo = "camino de " + largo.ToString("F1") + " m";
            return true;
        }

        motivo = largo < 0f ? "no hay camino por el NavMesh y hay una pared en medio"
                            : "sin linea de vista y el camino real mide " + largo.ToString("F1") + " m (maximo " + caminoMaximo + ")";
        return false;
    }

    public bool LineaLibre(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a;
        float dist = d.magnitude;
        if (dist < 0.01f) return true;
        int n = Physics.RaycastNonAlloc(a, d / dist, golpes, dist, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = golpes[i].collider;
            if (col != null && (laberinto == null || col.transform.IsChildOf(laberinto))) return false;
        }
        return true;
    }

    // Longitud del camino por el NavMesh entre el suelo bajo la cabeza y el destino; -1 si no hay camino completo.
    public float LongitudCamino(Vector3 desdeCabeza, Vector3 destino)
    {
        Vector3 suelo = new Vector3(desdeCabeza.x, destino.y, desdeCabeza.z);
        if (!NavMesh.SamplePosition(suelo, out NavMeshHit a, radioMuestraJugador, NavMesh.AllAreas)) return -1f;
        if (!NavMesh.SamplePosition(destino, out NavMeshHit b, radioMuestraDestino, NavMesh.AllAreas)) return -1f;
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) return -1f;

        float largo = 0f;
        var esq = camino.corners;
        for (int i = 1; i < esq.Length; i++) largo += Vector3.Distance(esq[i - 1], esq[i]);
        // Tramo extra: del punto del NavMesh al hotspot (alcoba de un nicho).
        largo += Vector3.Distance(b.position, destino);
        return largo;
    }
}
