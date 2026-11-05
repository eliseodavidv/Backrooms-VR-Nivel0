using UnityEngine;

// Pistola: dispara con el gatillo (indice) de la mano que la sostiene. 6 balas por partida, sin recarga.
// Hitscan con trazador visible; sonido y vibracion en la mano que dispara.
public class Pistola : ArmaAgarrable
{
    [Header("Referencias")]
    public Transform boquilla;
    public LineRenderer trazador;
    public GameObject destello;
    public TextMesh textoBalas;
    public AudioSource audioFuente;

    [Header("Combate")]
    public int balasMax = 6;
    public int danio = 2;
    public float alcance = 40f;
    public float cadencia = 0.25f;
    public float duracionEfecto = 0.06f;

    public int Balas { get; private set; }

    private float proximoDisparo;
    private float apagarEfectos;
    private readonly RaycastHit[] impactos = new RaycastHit[16];

    protected override void Awake()
    {
        base.Awake();
        Balas = balasMax;
        if (trazador != null) trazador.enabled = false;
        if (destello != null) destello.SetActive(false);
        ActualizarTexto();
    }

    protected override void Update()
    {
        base.Update();

        if (apagarEfectos > 0f && Time.unscaledTime >= apagarEfectos)
        {
            apagarEfectos = 0f;
            if (trazador != null) trazador.enabled = false;
            if (destello != null) destello.SetActive(false);
        }

        if (Sostenida && EnPartida && Time.timeScale > 0f && GatilloPulsado())
            Disparar();
    }

    // Devuelve true si salio una bala.
    public bool Disparar()
    {
        if (Time.time < proximoDisparo) return false;
        proximoDisparo = Time.time + cadencia;

        if (Balas <= 0)
        {
            Sonar(SonidosPlaceholder.Clic);
            VibracionMano.Pulso(Mano, 0.2f, 0.04f);
            return false;
        }
        Balas--;
        ActualizarTexto();

        Vector3 origen = boquilla.position;
        Vector3 dir = boquilla.forward;
        Vector3 fin = origen + dir * alcance;

        int n = Physics.RaycastNonAlloc(origen, dir, impactos, alcance, ~0, QueryTriggerInteraction.Ignore);
        float mejor = float.MaxValue;
        int idx = -1;
        for (int i = 0; i < n; i++)
        {
            if (EsPropio(impactos[i].collider)) continue;
            if (impactos[i].distance < mejor) { mejor = impactos[i].distance; idx = i; }
        }
        if (idx >= 0)
        {
            var h = impactos[idx];
            fin = h.point;
            if (!EsEntidad(h.collider))
            {
                var d = h.collider.GetComponentInParent<IDanable>();
                if (d != null) d.RecibirDanio(danio, h.point);
            }
        }

        if (trazador != null)
        {
            trazador.positionCount = 2;
            trazador.SetPosition(0, origen);
            trazador.SetPosition(1, fin);
            trazador.enabled = true;
        }
        if (destello != null) destello.SetActive(true);
        apagarEfectos = Time.unscaledTime + duracionEfecto;

        Sonar(SonidosPlaceholder.Disparo);
        VibracionMano.Pulso(Mano, 0.9f, 0.09f);
        return true;
    }

    void Sonar(AudioClip clip)
    {
        if (audioFuente != null) audioFuente.PlayOneShot(clip);
        else SonidosPlaceholder.EnPunto(clip, transform.position);
    }

    void ActualizarTexto()
    {
        if (textoBalas != null) textoBalas.text = Balas.ToString();
    }

    protected override void ReiniciarEstado()
    {
        Balas = balasMax;
        ActualizarTexto();
    }
}
