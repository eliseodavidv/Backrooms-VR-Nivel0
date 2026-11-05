using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using UnityEngine;

// Base de las armas. Se agarran con el Interaction SDK de tres formas, todas sobre el mismo Grabbable:
//   - controles (grip): GrabInteractable
//   - manos (gesto de agarre): HandGrabInteractable
//   - a distancia (apuntar y agarrar; el arma vuela a la mano): DistanceGrabInteractable / DistanceHandGrabInteractable
// Aqui se detecta si esta sostenida, con que mano y de que forma, se resalta cuando se puede agarrar,
// se oculta el rotulo de la mesa al agarrarla y se devuelve a su sitio al empezar una partida nueva.
// Las subclases (BarraMetal, Pistola) implementan el combate.
[RequireComponent(typeof(Rigidbody))]
public class ArmaAgarrable : MonoBehaviour
{
    [Tooltip("Los interactables del SDK que agarran esta arma (cualquier IInteractableView).")]
    public MonoBehaviour[] interactables;
    [Tooltip("Material (Unlit) que se pone mientras el jugador puede agarrar el arma. Brilla amarillo/blanco sin luces.")]
    public Material materialResalte;
    [Tooltip("Cartel sobre la mesa; se oculta al agarrar el arma.")]
    public GameObject rotulo;

    // Campo antiguo (bloque 1 original, solo controles). Ya no se usa; se conserva para que compile el builder viejo.
    [HideInInspector] public GrabInteractable interactable;

    public bool Sostenida { get; private set; }
    public bool ConManos { get; private set; }
    public OVRInput.Controller Mano { get; private set; } = OVRInput.Controller.None;
    public string ViaAgarre { get; private set; } = "";

    // El combate solo funciona durante la partida (no en menu, pausa ni pantalla de fin).
    protected bool EnPartida { get { return gm == null || gm.fase == GameManager.Fase.Jugando; } }

    private OVRHand[] manosTracking;
    private GameManager gm;
    private Rigidbody rb;
    private Vector3 posInicial;
    private Quaternion rotInicial;
    private GameManager.Fase ultimaFase = GameManager.Fase.Menu;
    private readonly bool[] pinchPrevio = new bool[2];

    private Renderer[] cuerpos;
    private Material[][] materialesOriginales;
    private Material resalteInstancia;
    private bool resaltado;
    private readonly List<IInteractableView> vistas = new List<IInteractableView>();

    const float AlturaMinima = -5f; // por debajo del suelo => se devuelve a su mesa

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        posInicial = transform.position;
        rotInicial = transform.rotation;

        var lista = new List<Renderer>();
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            if (r.gameObject.name != "Destello" && r.GetComponent<TextMesh>() == null) lista.Add(r);
        cuerpos = lista.ToArray();
        materialesOriginales = new Material[cuerpos.Length][];
        for (int i = 0; i < cuerpos.Length; i++) materialesOriginales[i] = cuerpos[i].sharedMaterials;
        if (materialResalte != null) resalteInstancia = new Material(materialResalte);
    }

    protected virtual void Start()
    {
        gm = FindObjectOfType<GameManager>();
        manosTracking = FindObjectsOfType<OVRHand>();
        vistas.Clear();
        if (interactables != null)
            foreach (var mb in interactables) { var v = mb as IInteractableView; if (v != null) vistas.Add(v); }
    }

    protected virtual void Update()
    {
        bool ahora = LeerAgarre(out var mano, out bool manos, out string via);
        if (ahora && !Sostenida)
        {
            Sostenida = true;
            Mano = mano; ConManos = manos; ViaAgarre = via;
            if (rotulo != null) rotulo.SetActive(false);
            Debug.Log("[Arma] " + name + " AGARRADA con " + (manos ? "MANO " : "CONTROL ") + (manos ? "" : mano.ToString() + " ") + "(" + via + ")");
            AjustarMientrasSostenida(true);
            AlAgarrar();
        }
        else if (ahora && Sostenida)
        {
            Mano = mano; ConManos = manos; ViaAgarre = via;
        }
        else if (!ahora && Sostenida)
        {
            Sostenida = false;
            Debug.Log("[Arma] " + name + " SOLTADA (cae con fisicas).");
            AjustarMientrasSostenida(false);
            AlSoltar();
            Mano = OVRInput.Controller.None;
            ConManos = false;
        }

        ActualizarResalte();
        ComprobarReinicio();
        if (!Sostenida && transform.position.y < AlturaMinima) VolverAMesa();
    }

    // Mientras el arma esta en la mano: sus colliders solidos se apagan (no empujan al jugador ni estorban al arco de
    // teletransporte; el golpe y el disparo usan consultas de fisica, no dependen de ellos).
    // No toca ningun interactor del SDK ni nada de la mano libre.
    void AjustarMientrasSostenida(bool sostenida)
    {
        foreach (var c in GetComponentsInChildren<Collider>(true))
            if (c != null && !c.isTrigger) c.enabled = !sostenida;
    }

    // ---------- lectura del SDK ----------

    bool LeerAgarre(out OVRInput.Controller mano, out bool conManos, out string via)
    {
        mano = OVRInput.Controller.None; conManos = false; via = "";
        foreach (var v in vistas)
        {
            if (v == null) continue;
            foreach (var iv in v.SelectingInteractorViews)
            {
                var comp = iv as Component;
                if (comp == null) continue;
                via = comp.GetType().Name;
                var ctrl = comp.GetComponentInParent<Controller>();
                if (ctrl != null)
                {
                    mano = ctrl.Handedness == Handedness.Left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                    return true;
                }
                var hand = comp.GetComponentInParent<Hand>();
                if (hand != null)
                {
                    conManos = true;
                    mano = hand.Handedness == Handedness.Left ? OVRInput.Controller.LHand : OVRInput.Controller.RHand;
                    return true;
                }
                return true; // sostenida por algo que no es mano ni control
            }
        }
        return false;
    }

    bool PuedeAgarrarse()
    {
        foreach (var v in vistas)
            if (v != null && v.State == InteractableState.Hover) return true;
        return false;
    }

    // Gatillo del arma: con control, el boton del indice de la mano que la sostiene; con manos, pinza de indice (cualquiera de las dos).
    protected bool GatilloPulsado()
    {
        if (!ConManos) return OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, Mano);
        bool disparo = false;
        if (manosTracking == null) return false;
        for (int i = 0; i < manosTracking.Length && i < 2; i++)
        {
            var h = manosTracking[i];
            bool pinza = h != null && h.IsTracked && h.GetFingerIsPinching(OVRHand.HandFinger.Index);
            if (pinza && !pinchPrevio[i]) disparo = true;
            pinchPrevio[i] = pinza;
        }
        return disparo;
    }

    // ---------- resalte ----------

    void ActualizarResalte()
    {
        bool quiere = resalteInstancia != null && !Sostenida && PuedeAgarrarse();
        if (quiere)
        {
            float k = Mathf.PingPong(Time.unscaledTime * 3f, 1f);
            resalteInstancia.color = Color.Lerp(new Color(1f, 0.85f, 0.1f), Color.white, k);
        }
        if (quiere == resaltado) return;
        resaltado = quiere;
        for (int i = 0; i < cuerpos.Length; i++)
        {
            if (quiere)
            {
                var m = new Material[materialesOriginales[i].Length];
                for (int j = 0; j < m.Length; j++) m[j] = resalteInstancia;
                cuerpos[i].sharedMaterials = m;
            }
            else cuerpos[i].sharedMaterials = materialesOriginales[i];
        }
    }

    // ---------- reinicio ----------

    // Partida nueva (Menu/Tutorial/Fin -> Jugando; la pausa no cuenta): municion, rotulo y posicion originales.
    void ComprobarReinicio()
    {
        if (gm == null) return;
        var f = gm.fase;
        if (f == GameManager.Fase.Pausa) return;
        if (f == GameManager.Fase.Jugando && ultimaFase != GameManager.Fase.Jugando)
        {
            ReiniciarEstado();
            if (rotulo != null) rotulo.SetActive(true);
            if (!Sostenida) VolverAMesa();
        }
        ultimaFase = f;
    }

    void VolverAMesa()
    {
        if (rb.isKinematic) return;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(posInicial, rotInicial);
    }

    protected virtual void ReiniciarEstado() { }
    protected virtual void AlAgarrar() { }
    protected virtual void AlSoltar() { }

    // Collider que cuelga de la entidad principal: intocable.
    protected static bool EsEntidad(Collider c)
    {
        return c.GetComponentInParent<EntityAI>() != null;
    }

    protected bool EsPropio(Collider c)
    {
        return c.transform.IsChildOf(transform);
    }
}
