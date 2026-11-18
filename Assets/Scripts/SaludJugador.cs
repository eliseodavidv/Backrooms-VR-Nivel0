using UnityEngine;

// Vida del jugador frente a los mini monstruos: cada mordisco quita 1; con 0 es derrota ("monstruo").
// La entidad principal no pasa por aqui (sigue capturando como siempre). Tras un mordisco hay unos segundos de
// invulnerabilidad para que varios monstruos juntos no maten al instante.
public class SaludJugador : MonoBehaviour
{
    public int vidaMax = 3;
    public float invulnerabilidad = 1.5f;
    [Tooltip("Quad rojo pegado a la camara (shader Backrooms/OverlayTop) que parpadea al recibir danyo.")]
    public Renderer destelloDanio;
    public float duracionDestello = 0.35f;

    public int Vida { get; private set; }

    // Se dispara cada vez que cambia la vida (mordisco, inicio y reinicio de partida). Lo usa el indicador de vidas.
    public event System.Action<int> OnVidaCambiada;

    private GameManager gm;
    private Material matDestello;
    private float invulnerableHasta;
    private float destelloFin;
    private GameManager.Fase ultimaFase = GameManager.Fase.Menu;

    void Start()
    {
        Vida = vidaMax;
        gm = FindObjectOfType<GameManager>();
        IndicadorVidas.Crear(this, gm);   // corazones en pantalla (se crea por codigo, sin tocar la escena)
        if (destelloDanio != null)
        {
            matDestello = destelloDanio.material;
            destelloDanio.enabled = false;
        }
    }

    void Update()
    {
        if (gm != null)
        {
            var f = gm.fase;
            if (f != GameManager.Fase.Pausa)
            {
                if (f == GameManager.Fase.Jugando && ultimaFase != GameManager.Fase.Jugando) FijarVida(vidaMax);
                ultimaFase = f;
            }
        }

        if (destelloFin > 0f && matDestello != null)
        {
            float k = (destelloFin - Time.unscaledTime) / duracionDestello;
            if (k <= 0f)
            {
                destelloFin = 0f;
                destelloDanio.enabled = false;
            }
            else
            {
                var c = matDestello.color;
                c.a = 0.55f * k;
                matDestello.color = c;
            }
        }
    }

    // Partida nueva desde cero (lo llama el GameManager): vidas al maximo, sin invulnerabilidad ni destello rojo.
    public void ReiniciarPartida()
    {
        FijarVida(vidaMax);
        invulnerableHasta = 0f;
        destelloFin = 0f;
        if (destelloDanio != null) destelloDanio.enabled = false;
        if (matDestello != null)
        {
            var c = matDestello.color;
            c.a = 0f;
            matDestello.color = c;
        }
    }

    void FijarVida(int valor)
    {
        Vida = valor;
        OnVidaCambiada?.Invoke(Vida);
    }

    // Devuelve true si el mordisco hizo danyo.
    public bool RecibirMordisco()
    {
        if (gm == null || gm.fase != GameManager.Fase.Jugando) return false;
        if (Time.time < invulnerableHasta) return false;
        invulnerableHasta = Time.time + invulnerabilidad;
        FijarVida(Vida - 1);

        ControllerHaptics.Pulse(1f, 0.25f);
        SonidosPlaceholder.EnPunto(SonidosPlaceholder.Mordisco, Camera.main != null ? Camera.main.transform.position : transform.position, 1f);
        if (destelloDanio != null)
        {
            destelloDanio.enabled = true;
            destelloFin = Time.unscaledTime + duracionDestello;
        }

        Debug.Log("[Salud] Mordisco. Vida " + Vida + "/" + vidaMax);
        if (Vida <= 0) gm.DerrotaPorMonstruo();
        return true;
    }
}
