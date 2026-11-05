using UnityEngine;

// Zumbido espacial de la salida: crece al acercarse. Equivalente visual (RNF-06): la puerta de luz pulsa
// con mas fuerza y rapidez cuanto mas cerca esta el jugador.
public class ExitHum : MonoBehaviour
{
    public Transform jugador;
    public AudioClip zumbido;
    public Renderer puerta;
    public float distanciaMaxima = 40f;
    public Color emisionBase = new Color(1.6f, 2.0f, 1.7f);

    private AudioSource fuente;
    private MaterialPropertyBlock bloque;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.clip = zumbido;
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 2f;
        fuente.maxDistance = distanciaMaxima;
        fuente.dopplerLevel = 0f;
        if (zumbido != null) fuente.Play();
    }

    void Update()
    {
        if (jugador == null) return;
        float cerca = 1f - Mathf.Clamp01(Vector3.Distance(jugador.position, transform.position) / distanciaMaxima);
        fuente.volume = Mathf.Lerp(0.3f, 1f, cerca);

        if (puerta == null) return;
        float pulso = Mathf.Sin(Time.time * Mathf.Lerp(1.5f, 5f, cerca));
        float k = 1f + pulso * Mathf.Lerp(0.05f, 0.45f, cerca);
        puerta.GetPropertyBlock(bloque);
        bloque.SetColor("_EmissionColor", emisionBase * k);
        puerta.SetPropertyBlock(bloque);
    }
}
