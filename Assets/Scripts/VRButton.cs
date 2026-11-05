using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Boton de un panel en espacio mundo. Se activa apuntando con el puntero laser (LaserPointer) y el gatillo.
[RequireComponent(typeof(BoxCollider))]
public class VRButton : MonoBehaviour
{
    public UnityEvent alHacerClick = new UnityEvent();
    public Image fondo;
    public Text texto;
    public Color fondoSeleccionado = new Color(0.88f, 0.78f, 0.38f);
    public Color textoSeleccionado = new Color(0.1f, 0.09f, 0.06f);

    private Color baseFondo, baseTexto;
    private bool capturado;
    private bool hover, seleccionado;

    void Capturar()
    {
        if (capturado) return;
        capturado = true;
        if (fondo != null) baseFondo = fondo.color;
        if (texto != null) baseTexto = texto.color;
    }

    void Awake() { Capturar(); }

    public void SetHover(bool valor) { hover = valor; Aplicar(); }
    public void SetSeleccionado(bool valor) { seleccionado = valor; Aplicar(); }
    public void Click() { alHacerClick.Invoke(); }

    void OnDisable() { hover = false; Aplicar(); }

    void Aplicar()
    {
        Capturar();
        if (fondo == null) return;
        Color c = seleccionado ? fondoSeleccionado : baseFondo;
        if (hover) c = Color.Lerp(c, Color.white, 0.25f);
        fondo.color = c;
        if (texto != null) texto.color = seleccionado ? textoSeleccionado : baseTexto;
    }
}
