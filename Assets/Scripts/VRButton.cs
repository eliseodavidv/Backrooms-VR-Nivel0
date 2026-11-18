using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Boton de un panel en espacio mundo. Se activa apuntando con el puntero laser (LaserPointer) y el gatillo.
// Los botones antiguos (pausa, tutorial, fin) siguen igual. Los del menu de inicio usan estiloPropio:
// colores de hover propios, un pequeño aumento de tamaño, y un clic leve al apuntar / pulsar.
[RequireComponent(typeof(BoxCollider))]
public class VRButton : MonoBehaviour
{
    public UnityEvent alHacerClick = new UnityEvent();
    public Image fondo;
    public Text texto;
    public Color fondoSeleccionado = new Color(0.88f, 0.78f, 0.38f);
    public Color textoSeleccionado = new Color(0.1f, 0.09f, 0.06f);

    [Header("Estilo del menu de inicio (opcional)")]
    public TMP_Text textoTMP;
    public bool estiloPropio;
    public Color fondoHover = new Color(0.40f, 0.34f, 0.10f, 0.95f);
    public Color textoHover = new Color(1f, 0.93f, 0.45f);
    public float escalaHover = 1.06f;
    [Tooltip("Placa que se ve al apuntar (amarillo apagado). Si es null solo cambia el color.")]
    public Sprite spriteHover;
    public AudioClip sonidoHover;
    public AudioClip sonidoClick;
    [Range(0f, 1f)] public float volumenSonido = 0.5f;

    private Color baseFondo, baseTexto;
    private Vector3 baseEscala = Vector3.one;
    private Sprite baseSprite;
    private bool capturado;
    private bool hover, seleccionado;
    private AudioSource fuente;

    void Capturar()
    {
        if (capturado) return;
        capturado = true;
        if (fondo != null) { baseFondo = fondo.color; baseSprite = fondo.sprite; }
        if (texto != null) baseTexto = texto.color;
        else if (textoTMP != null) baseTexto = textoTMP.color;
        baseEscala = transform.localScale;
    }

    void Awake() { Capturar(); }

    public void SetHover(bool valor)
    {
        bool entra = valor && !hover;
        hover = valor;
        Aplicar();
        if (entra) Sonar(SonidosPlaceholder.UiHover != null ? SonidosPlaceholder.UiHover : sonidoHover, 0.5f);
    }

    public void SetSeleccionado(bool valor) { seleccionado = valor; Aplicar(); }

    public void Click()
    {
        Sonar(SonidosPlaceholder.UiClick != null ? SonidosPlaceholder.UiClick : sonidoClick, 1f);
        alHacerClick.Invoke();
    }

    void OnDisable()
    {
        hover = false;
        Aplicar();
    }

    void Sonar(AudioClip clip, float factor)
    {
        if (clip == null) return;
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
            fuente.spatialBlend = 0f;               // 2D: suena igual en cualquier punto
            fuente.ignoreListenerPause = true;      // tambien dentro del menu de pausa
            Sonido3D.Preparar(fuente, BusAudio.Musica, false);   // 2D y sin eco de pasillo
        }
        fuente.PlayOneShot(clip, volumenSonido * factor);
    }

    void Aplicar()
    {
        Capturar();
        if (estiloPropio)
        {
            // Resaltado = apuntado o elegido (por ejemplo el voto "me gusta" ya marcado).
            bool resaltado = hover || seleccionado;
            if (fondo != null)
            {
                if (spriteHover != null) fondo.sprite = resaltado ? spriteHover : baseSprite;
                fondo.color = resaltado ? fondoHover : baseFondo;
            }
            Color ct = resaltado ? textoHover : baseTexto;
            if (textoTMP != null) textoTMP.color = ct;
            if (texto != null) texto.color = ct;
            transform.localScale = hover ? baseEscala * escalaHover : baseEscala;
            return;
        }

        if (fondo == null) return;
        Color c = seleccionado ? fondoSeleccionado : baseFondo;
        if (hover) c = Color.Lerp(c, Color.white, 0.25f);
        fondo.color = c;
        if (texto != null) texto.color = seleccionado ? textoSeleccionado : baseTexto;
    }
}
