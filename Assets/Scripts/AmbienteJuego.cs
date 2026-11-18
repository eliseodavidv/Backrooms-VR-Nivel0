using UnityEngine;

// Ambiente sonoro de la PARTIDA (el del menu esta en AmbienteMenuTerror):
//  - drone grave de fondo en bucle (entra y sale con fundido),
//  - un susurro leve y espacial a 1,6-3,2 m del jugador cada 20 a 40 s,
//  - whoosh suave con pisada al teletransportarse,
//  - sonido breve de victoria o de derrota al terminar.
// Se crea solo (AmbienteJuego.Asegurar) y muere con la escena. Todo pasa por Sonido3D (espacializador Meta + mixer).
public class AmbienteJuego : MonoBehaviour
{
    public static AmbienteJuego Instancia;

    public float volumenDrone = 0.30f;
    public float volumenDroneMenu = 0.20f;     // el drone tambien suena en el menu, mas bajo
    public Vector2 intervaloSusurro = new Vector2(20f, 40f);

    private GameManager gm;
    private AudioSource drone;
    private bool activo;
    private float proximoSusurro;

    public static void Asegurar()
    {
        if (FindObjectOfType<AmbienteJuego>() == null) new GameObject("AmbienteJuego").AddComponent<AmbienteJuego>();
    }

    void Awake()
    {
        Instancia = this;
        gm = FindObjectOfType<GameManager>();
        drone = gameObject.AddComponent<AudioSource>();
        drone.playOnAwake = false;
        drone.loop = true;
        drone.volume = 0f;
        Sonido3D.Preparar(drone, BusAudio.Ambiente, false);   // 2D: ocupa toda la cabeza, no viene de un sitio
    }

    void OnEnable() { PlayerNoiseEmitter.Teletransporte += AlTeletransportarse; }
    void OnDisable() { PlayerNoiseEmitter.Teletransporte -= AlTeletransportarse; }

    void Update()
    {
        if (gm == null) return;
        var f = gm.fase;
        if (f != GameManager.Fase.Pausa)
        {
            // El drone suena en el menu, el tutorial y la partida; se va en la pantalla de fin.
            bool deseado = f == GameManager.Fase.Jugando || f == GameManager.Fase.Menu || f == GameManager.Fase.Tutorial;
            if (deseado && !activo) Iniciar();
            else if (!deseado && activo) activo = false;
        }

        // Fundido del drone (en tiempo real: sigue funcionando aunque el tiempo del juego este quieto).
        float volBase = gm.fase == GameManager.Fase.Jugando ? volumenDrone : volumenDroneMenu;
        float objetivo = activo ? volBase * SonidosPlaceholder.GananciaDrone : 0f;
        drone.volume = Mathf.MoveTowards(drone.volume, objetivo, Time.unscaledDeltaTime * 0.15f);
        if (!activo && drone.isPlaying && drone.volume <= 0.001f) drone.Stop();

        // Los susurros solo en la partida (el menu tiene los suyos).
        if (activo && gm.fase == GameManager.Fase.Jugando && Time.time >= proximoSusurro)
        {
            proximoSusurro = Time.time + Random.Range(intervaloSusurro.x, intervaloSusurro.y);
            Susurrar();
        }
    }

    void Iniciar()
    {
        activo = true;
        drone.clip = SonidosPlaceholder.Drone;
        if (!drone.isPlaying) drone.Play();
        proximoSusurro = Time.time + Random.Range(intervaloSusurro.x, intervaloSusurro.y);
    }

    void Susurrar()
    {
        if (gm == null || gm.cabeza == null) return;
        var clips = SonidosPlaceholder.Susurros;
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        float ang = Random.value * 360f;
        float radio = Random.Range(1.6f, 3.2f);
        Vector3 p = gm.cabeza.position + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * radio + Vector3.up * Random.Range(-0.4f, 0.3f);
        Sonido3D.EnPunto(clip, p, Random.Range(0.25f, 0.40f), BusAudio.Ambiente, Random.Range(0.92f, 1.05f));
    }

    void AlTeletransportarse(Vector3 destino)
    {
        if (gm == null || gm.fase != GameManager.Fase.Jugando) return;
        Sonido3D.EnPunto(SonidosPlaceholder.Teletransporte, destino + Vector3.up * 0.15f, 0.55f, BusAudio.SFX);
    }

    // Lo llama el GameManager al terminar la partida.
    // causa: "salida", "tiempo", "captura" (la entidad te atrapa) o "monstruo" (te devoran los mini monstruos).
    public void Final(bool victoria, string causa)
    {
        activo = false;
        AudioClip clip;
        if (victoria) clip = SonidosPlaceholder.Victoria;
        else
        {
            // Si te devoran: grito (real, grito_muerte). Si se acabo el tiempo, o no hay archivo, el sonido de derrota de siempre.
            var grito = (causa == "captura" || causa == "monstruo") ? SonidosPlaceholder.GritoMuerte : null;
            clip = grito != null ? grito : SonidosPlaceholder.Derrota;
        }
        Sonido3D.Reproducir2D(clip, 0.85f, BusAudio.Musica);
    }
}
