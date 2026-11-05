using UnityEngine;

// Puntero laser de los controladores para los menus (RF-01). Un rayo por mano contra los botones (capa UI);
// el gatillo (PrimaryIndexTrigger) pulsa el boton apuntado. Se activa solo mientras hay un panel visible.
public class LaserPointer : MonoBehaviour
{
    public Transform manoDerecha;
    public Transform manoIzquierda;
    [Tooltip("Origen alternativo del rayo (pruebas sin visor).")]
    public Transform rayoDePrueba;
    public LayerMask capasUI;
    public float alcance = 6f;
    public bool activo;

    private LineRenderer lineaD, lineaI, lineaP;
    private VRButton hoverD, hoverI, hoverP;

    public VRButton BotonApuntado
    {
        get
        {
            if (hoverP != null) return hoverP;
            if (hoverD != null) return hoverD;
            return hoverI;
        }
    }

    public void ClickApuntado()
    {
        var b = BotonApuntado;
        if (b != null) b.Click();
    }

    void Awake()
    {
        lineaD = CrearLinea("Laser_Derecha");
        lineaI = CrearLinea("Laser_Izquierda");
        lineaP = CrearLinea("Laser_Prueba");
    }

    LineRenderer CrearLinea(string nombre)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.startWidth = 0.006f;
        lr.endWidth = 0.003f;
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = new Color(0.95f, 0.85f, 0.4f, 0.9f);
        lr.endColor = new Color(0.95f, 0.85f, 0.4f, 0.2f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    void Update()
    {
        if (!activo)
        {
            Limpiar();
            return;
        }
        Procesar(manoDerecha, lineaD, ref hoverD, OVRInput.Controller.RTouch, true);
        Procesar(manoIzquierda, lineaI, ref hoverI, OVRInput.Controller.LTouch, true);
        Procesar(rayoDePrueba, lineaP, ref hoverP, OVRInput.Controller.None, false);
    }

    void Procesar(Transform origen, LineRenderer linea, ref VRButton hover, OVRInput.Controller mando, bool permitirClick)
    {
        if (origen == null)
        {
            if (hover != null) { hover.SetHover(false); hover = null; }
            linea.enabled = false;
            return;
        }

        VRButton nuevo = null;
        float distancia = alcance;
        if (Physics.Raycast(origen.position, origen.forward, out RaycastHit hit, alcance, capasUI, QueryTriggerInteraction.Collide))
        {
            nuevo = hit.collider.GetComponentInParent<VRButton>();
            distancia = hit.distance;
        }

        if (nuevo != hover)
        {
            if (hover != null) hover.SetHover(false);
            hover = nuevo;
            if (hover != null)
            {
                hover.SetHover(true);
                ControllerHaptics.Pulse(0.2f, 0.03f); // tick haptico al apuntar a un boton
            }
        }

        linea.enabled = true;
        linea.SetPosition(0, origen.position);
        linea.SetPosition(1, origen.position + origen.forward * distancia);

        if (permitirClick && hover != null && OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, mando))
        {
            ControllerHaptics.Pulse(0.5f, 0.06f);
            hover.Click();
        }
    }

    void Limpiar()
    {
        if (hoverD != null) { hoverD.SetHover(false); hoverD = null; }
        if (hoverI != null) { hoverI.SetHover(false); hoverI = null; }
        if (hoverP != null) { hoverP.SetHover(false); hoverP = null; }
        lineaD.enabled = lineaI.enabled = lineaP.enabled = false;
    }
}
