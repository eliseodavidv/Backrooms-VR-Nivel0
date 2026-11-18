using System.Collections.Generic;
using UnityEngine;

// Arma cuerpo a cuerpo. Daña a lo que toque entre la empunadura y la punta cuando la punta se mueve a suficiente velocidad.
// Se mide el movimiento de la punta fotograma a fotograma (el arma es kinematica mientras se sostiene, asi que no
// dependemos de OnCollision) y se barre la barra con OverlapCapsule.
public class BarraMetal : ArmaAgarrable
{
    [Header("Referencias")]
    public Transform baseGolpe;
    public Transform puntaGolpe;
    public AudioSource audioFuente;

    [Header("Combate")]
    public int danio = 1;
    public float velocidadMinima = 2.2f;   // m/s de la punta para contar como golpe
    public float velocidadMaxima = 15f;    // por encima es un salto (teletransporte), no un golpe
    public float radio = 0.06f;
    public float enfriamientoPorBlanco = 0.45f;

    private Vector3 puntaPrev;
    private bool hayPrev;
    private readonly Collider[] buffer = new Collider[24];
    private readonly Dictionary<Object, float> ultimoGolpe = new Dictionary<Object, float>();

    // Ultima velocidad medida de la punta (util para depurar y probar).
    public float VelocidadPunta { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        Sonido3D.Preparar(audioFuente, BusAudio.SFX, true, 1f, 25f);   // espacializador de Meta + eco de pasillo
    }

    protected override void AlAgarrar() { hayPrev = false; }

    protected override void Update()
    {
        base.Update();
        if (!Sostenida || puntaGolpe == null) { hayPrev = false; return; }

        Vector3 punta = puntaGolpe.position;
        float dt = Time.deltaTime;
        if (hayPrev && dt > 0f)
        {
            VelocidadPunta = (punta - puntaPrev).magnitude / dt;
            if (EnPartida && VelocidadPunta >= velocidadMinima && VelocidadPunta <= velocidadMaxima)
                AplicarGolpe(VelocidadPunta);
        }
        puntaPrev = punta;
        hayPrev = true;
    }

    // Barre la barra y danya lo que haya. Devuelve cuantos blancos recibieron danyo.
    public int AplicarGolpe(float velocidad)
    {
        int n = Physics.OverlapCapsuleNonAlloc(baseGolpe.position, puntaGolpe.position, radio, buffer, ~0, QueryTriggerInteraction.Ignore);
        int golpes = 0;
        bool tocoEntidad = false;
        Vector3 donde = puntaGolpe.position;

        for (int i = 0; i < n; i++)
        {
            var c = buffer[i];
            if (EsPropio(c)) continue;
            if (EsEntidad(c)) { tocoEntidad = true; donde = c.ClosestPoint(puntaGolpe.position); continue; }

            var d = c.GetComponentInParent<IDanable>();
            if (d == null) continue;
            var clave = d as Object;
            float t;
            if (clave != null && ultimoGolpe.TryGetValue(clave, out t) && Time.time - t < enfriamientoPorBlanco) continue;
            if (clave != null) ultimoGolpe[clave] = Time.time;

            Vector3 p = c.ClosestPoint(puntaGolpe.position);
            d.RecibirDanio(danio, p);
            golpes++;
            donde = p;
        }

        if (golpes > 0)
        {
            Sonar(SonidosPlaceholder.GolpeMetal, donde);
            // Mas fuerte cuanto mas rapido el golpe.
            VibracionMano.Pulso(Mano, Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(velocidadMinima, 8f, velocidad)), 0.15f);
        }
        else if (tocoEntidad)
        {
            // La entidad no se puede danar: rebote sordo y vibracion leve, sin efecto.
            Sonar(SonidosPlaceholder.Clic, donde);
            VibracionMano.Pulso(Mano, 0.25f, 0.05f);
        }
        return golpes;
    }

    void Sonar(AudioClip clip, Vector3 punto)
    {
        if (audioFuente != null) audioFuente.PlayOneShot(clip);
        else SonidosPlaceholder.EnPunto(clip, punto);
    }

    protected override void ReiniciarEstado() { ultimoGolpe.Clear(); }
}
