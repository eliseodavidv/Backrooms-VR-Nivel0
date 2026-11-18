using UnityEngine;

// Atmosfera de terror del menu (todo barato para Quest 2: nada de postproceso ni luces realtime):
//  - Maneja el parpadeo del tubo fluorescente (Luz 0..1) que leen los titulos, el cono de luz y los paneles.
//  - Silueta de la entidad al fondo del pasillo: solo se ve durante los apagones.
//  - Grano leve (quad con textura que salta 15 veces por segundo) y respiracion minima de la viñeta.
//  - Susurro o sonido grave cada 20 a 40 s.
// Lo activa y apaga el GameManager (Activar). Fuera del menu no hace nada.
public class AmbienteMenuTerror : MonoBehaviour
{
    public static float Luz = 1f;        // 1 = encendido, ~0.05 = apagon
    public static bool Apagado;

    [Header("Referencias de escena")]
    public Transform cabeza;
    public Transform rig;
    public Transform vinetaTf;           // viñeta pegada a la camara (respira)
    public MeshFilter granoMesh;         // quad del grano (UV que saltan)
    public MeshRenderer conoRend;        // cono de luz fluorescente, fijo en el mundo detras del panel
    public MeshRenderer siluetaRend;     // silueta de la entidad
    public AudioSource zumbido;          // zumbido ambiente (lo reproduce el GameManager)
    public AudioSource susurro;
    public AudioClip[] susurros;
    public CanvasGroup[] grupos;         // paneles del menu: se atenuan con los apagones
    public Transform[] panelesSeguidos;  // el cono sigue al panel visible

    [Header("Parpadeo")]
    public Vector2 pausaEntreRafagas = new Vector2(2.5f, 6f);
    [Range(0f, 1f)] public float probApagonLargo = 0.65f;

    [Header("Susurros")]
    public Vector2 intervaloSusurro = new Vector2(20f, 40f);

    [Header("Silueta")]
    [Tooltip("Alta a proposito (3 m): asoma por encima de la columna de botones, que queda delante.")]
    public float alturaSilueta = 3f;
    public float distanciaMinSilueta = 6f;
    public float distanciaMaxSilueta = 8.5f;

    private bool activo;
    private bool on = true, largoHecho;
    private int destellos;
    private float tCambio, proxSusurro, proxGrano;
    private float volZumbido = -1f;
    private float luzPrevia = -1f;
    private Material matCono;
    private Mesh meshGrano;
    private Vector3 escalaVineta;
    private Mesh meshSilueta;

    void Awake()
    {
        // Audio: zumbido 2D en el grupo Ambiente y susurro espacial con el espacializador de Meta.
        var realZumbido = SonidosPlaceholder.ZumbidoSalida;
        if (zumbido != null && realZumbido != null) zumbido.clip = realZumbido;
        Sonido3D.Preparar(zumbido, BusAudio.Ambiente, false);
        Sonido3D.Preparar(susurro, BusAudio.Ambiente, true, 1f, 8f);
        if (conoRend != null) matCono = conoRend.material;   // instancia: no ensucia el asset
        if (granoMesh != null) meshGrano = granoMesh.mesh;
        if (vinetaTf != null) escalaVineta = vinetaTf.localScale;
        if (siluetaRend != null)
        {
            var mf = siluetaRend.GetComponent<MeshFilter>();
            if (mf != null) meshSilueta = mf.sharedMesh;
            siluetaRend.enabled = false;
        }
        Luz = 1f;
        Apagado = false;
    }

    void OnDisable()
    {
        Luz = 1f;
        Apagado = false;
    }

    public void Activar(bool a)
    {
        activo = a;
        on = true;
        largoHecho = false;
        destellos = 0;
        luzPrevia = -1f;
        float t = Time.unscaledTime;
        tCambio = t + Random.Range(pausaEntreRafagas.x, pausaEntreRafagas.y);
        proxSusurro = t + Random.Range(intervaloSusurro.x, intervaloSusurro.y);
        if (conoRend != null) conoRend.gameObject.SetActive(a);
        if (siluetaRend != null) siluetaRend.enabled = false;
        if (!a)
        {
            Luz = 1f;
            Apagado = false;
            AplicarLuz(1f);
            if (vinetaTf != null) { vinetaTf.localScale = escalaVineta; vinetaTf.localRotation = Quaternion.identity; }
            return;
        }
        Colocar();
    }

    // Silueta al fondo del pasillo, a la distancia que permita la pared que tenga enfrente (entre 5 y 14 m).
    void Colocar()
    {
        if (siluetaRend == null || cabeza == null) return;
        Vector3 frente = cabeza.forward; frente.y = 0f;
        if (frente.sqrMagnitude < 0.01f && rig != null) { frente = rig.forward; frente.y = 0f; }
        frente.Normalize();

        float dist = 20f;
        Vector3 origen = cabeza.position;
        int mascara = ~(1 << LayerMask.NameToLayer("UI"));
        if (Physics.Raycast(origen, frente, out RaycastHit hit, 30f, mascara, QueryTriggerInteraction.Ignore)) dist = hit.distance;
        float d = Mathf.Clamp(dist - 1.2f, distanciaMinSilueta, distanciaMaxSilueta);

        float suelo = rig != null ? rig.position.y : 0f;
        float escala = 1f, pie = 0f;
        if (meshSilueta != null)
        {
            escala = alturaSilueta / Mathf.Max(0.01f, meshSilueta.bounds.size.y);
            pie = -meshSilueta.bounds.min.y * escala;
        }
        var tf = siluetaRend.transform;
        tf.position = new Vector3(origen.x + frente.x * d, suelo + pie, origen.z + frente.z * d);
        tf.rotation = Quaternion.LookRotation(-frente, Vector3.up);
        tf.localScale = Vector3.one * escala;
    }

    void Update()
    {
        if (!activo) return;
        float t = Time.unscaledTime;

        // ---- parpadeo ----
        if (t >= tCambio) Avanzar(t);
        Luz = on ? 0.9f + 0.1f * Mathf.PerlinNoise(t * 9f, 0.37f) : 0.05f;
        Apagado = !on;
        if (!Mathf.Approximately(Luz, luzPrevia)) AplicarLuz(Luz);

        if (siluetaRend != null) siluetaRend.enabled = Apagado;

        // ---- el cono sigue al panel visible ----
        if (conoRend != null && panelesSeguidos != null)
        {
            Transform p = null;
            foreach (var x in panelesSeguidos) if (x != null && x.gameObject.activeInHierarchy) { p = x; break; }
            conoRend.enabled = p != null;
            if (p != null)
            {
                // El panel mira al jugador con su +z hacia adentro: "detras" del panel es +z. El vertice (apice) del cono queda ~1,2 m sobre los ojos.
                conoRend.transform.position = p.position + p.forward * 0.2f + Vector3.down * 1.8f;
                conoRend.transform.rotation = Quaternion.LookRotation(p.forward, Vector3.up);
            }
        }

        // ---- respiracion minima de la viñeta ----
        if (vinetaTf != null)
        {
            float r = Mathf.Sin(t * 2f * Mathf.PI / 4.8f);
            vinetaTf.localScale = escalaVineta * (1f + 0.035f * r);
            vinetaTf.localRotation = Quaternion.Euler(0f, 0f, 0.5f * Mathf.Sin(t * 2f * Mathf.PI / 7.3f));
        }

        // ---- grano ----
        if (meshGrano != null && t >= proxGrano)
        {
            proxGrano = t + 0.066f;
            Vector2 o = new Vector2(Random.value, Random.value);
            const float n = 8f;
            meshGrano.uv = new[] { o, o + new Vector2(n, 0), o + new Vector2(0, n), o + new Vector2(n, n) };
        }

        // ---- susurro ----
        if (t >= proxSusurro)
        {
            proxSusurro = t + Random.Range(intervaloSusurro.x, intervaloSusurro.y);
            Susurrar();
        }
    }

    void Avanzar(float t)
    {
        if (on && destellos <= 0)
        {
            destellos = Random.Range(2, 6);
            on = false;
            tCambio = t + Random.Range(0.04f, 0.16f);
            return;
        }
        if (!on)
        {
            if (destellos > 0) destellos--;
            if (destellos <= 0)
            {
                if (!largoHecho && Random.value < probApagonLargo)
                {
                    largoHecho = true;                       // apagon largo: aqui se ve la silueta
                    tCambio = t + Random.Range(0.45f, 1.0f);
                    return;
                }
                largoHecho = false;
                on = true;
                tCambio = t + Random.Range(pausaEntreRafagas.x, pausaEntreRafagas.y);
                return;
            }
            on = true;
            tCambio = t + Random.Range(0.03f, 0.12f);
            return;
        }
        on = false;
        tCambio = t + Random.Range(0.04f, 0.16f);
    }

    void AplicarLuz(float l)
    {
        luzPrevia = l;
        if (matCono != null) matCono.color = new Color(1f, 1f, 1f, l);
        float g = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(l));
        if (grupos != null)
            foreach (var c in grupos) if (c != null) c.alpha = activo ? g : 1f;
        if (zumbido != null)
        {
            if (volZumbido < 0f) volZumbido = zumbido.volume;
            zumbido.volume = volZumbido * Mathf.Lerp(0.3f, 1f, l);
        }
    }

    void Susurrar()
    {
        if (susurro == null || cabeza == null) return;
        var clips = SonidosPlaceholder.Susurros;   // susurros generados (o reales si hay archivos susurro_*)
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;
        // Detras y a un lado del jugador, grave y bajito.
        Vector3 atras = -cabeza.forward; atras.y = 0f; atras.Normalize();
        Vector3 lado = Vector3.Cross(Vector3.up, atras) * (Random.value < 0.5f ? -1f : 1f);
        susurro.transform.position = cabeza.position + atras * 1.5f + lado * 0.9f;
        susurro.pitch = Random.Range(0.85f, 1f);
        susurro.PlayOneShot(clip, 0.5f);
    }
}
