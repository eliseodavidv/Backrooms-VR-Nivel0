using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Titulo de tubo fluorescente: sigue la luz del menu (AmbienteMenuTerror.Luz), asi que todos los titulos y el cono
// parpadean a la vez. El titulo grande (permitirGlitch) ademas tiene un glitch muy breve y raro: el texto salta
// unos pixeles y aparece un "fantasma" rojizo desplazado durante ~0,1 s, cada 9 a 22 s.
// Solo toca el color cuando cambia la luz, asi que cuesta casi nada en el Quest 2.
[RequireComponent(typeof(TMP_Text))]
public class TituloFluorescente : MonoBehaviour
{
    public Color colorBase = new Color(0.70f, 0.60f, 0.20f);
    [Tooltip("Imagenes que se apagan junto con el titulo (sangre / grietas).")]
    public Graphic[] acompanantes;

    [Header("Glitch (solo el titulo grande)")]
    public bool permitirGlitch;
    public RectTransform grupo;          // contenedor que salta
    public TMP_Text fantasma;            // copia rojiza desplazada
    public Vector2 intervaloGlitch = new Vector2(9f, 22f);

    private TMP_Text txt;
    private float luzPrevia = -1f;
    private Vector2 posBase;
    private Vector3 escalaBase = Vector3.one;
    private float proxGlitch, finGlitch;
    private bool enGlitch;

    void Awake()
    {
        txt = GetComponent<TMP_Text>();
        if (grupo != null) { posBase = grupo.anchoredPosition; escalaBase = grupo.localScale; }
    }

    void OnEnable()
    {
        luzPrevia = -1f;
        Aplicar(AmbienteMenuTerror.Luz);
        proxGlitch = Time.unscaledTime + Random.Range(intervaloGlitch.x, intervaloGlitch.y);
        TerminarGlitch();
    }

    void OnDisable() { TerminarGlitch(); }

    void Update()
    {
        float l = AmbienteMenuTerror.Luz;
        if (!Mathf.Approximately(l, luzPrevia)) Aplicar(l);

        if (!permitirGlitch || grupo == null) return;
        float t = Time.unscaledTime;
        if (enGlitch)
        {
            if (t >= finGlitch) { TerminarGlitch(); return; }
            // Cada cuadro salta distinto mientras dura.
            grupo.anchoredPosition = posBase + new Vector2(Random.Range(-22f, 22f), Random.Range(-5f, 5f));
            grupo.localScale = new Vector3(escalaBase.x * Random.Range(0.95f, 1.06f), escalaBase.y, escalaBase.z);
            if (fantasma != null)
                fantasma.rectTransform.anchoredPosition = new Vector2(Random.Range(10f, 26f) * (Random.value < 0.5f ? -1f : 1f), Random.Range(-4f, 4f));
        }
        else if (t >= proxGlitch && l > 0.5f)
        {
            enGlitch = true;
            finGlitch = t + Random.Range(0.07f, 0.13f);
            if (fantasma != null) fantasma.gameObject.SetActive(true);
        }
    }

    void TerminarGlitch()
    {
        enGlitch = false;
        proxGlitch = Time.unscaledTime + Random.Range(intervaloGlitch.x, intervaloGlitch.y);
        if (grupo != null) { grupo.anchoredPosition = posBase; grupo.localScale = escalaBase; }
        if (fantasma != null) fantasma.gameObject.SetActive(false);
    }

    void Aplicar(float l)
    {
        luzPrevia = l;
        if (txt == null) txt = GetComponent<TMP_Text>();
        txt.color = new Color(colorBase.r * l, colorBase.g * l, colorBase.b * l, 1f);
        if (acompanantes != null)
            foreach (var g in acompanantes) if (g != null) g.color = new Color(l, l, l, 1f);
    }
}
