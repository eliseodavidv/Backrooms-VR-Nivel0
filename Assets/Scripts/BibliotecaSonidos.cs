using UnityEngine;

// Biblioteca de sonidos REALES. Se llena sola: cuando pones archivos en Assets/Audio/Reales/ con los nombres de la
// tabla (ver BackroomsAudioBuilder), un postprocesador de importacion asigna cada archivo a su casilla.
// Si una casilla esta vacia, el juego usa el sonido generado por codigo (SonidosPlaceholder).
// Vive en Assets/Resources/BibliotecaSonidos.asset para que tambien funcione en el APK del Quest.
public class BibliotecaSonidos : ScriptableObject
{
    // nombre de archivo (sin extension) -> casilla
    public AudioClip zumbidoSalida;      // zumbido_salida      : puerta de salida y ambiente del menu (bucle)
    public AudioClip pasosEntidad;       // pasos_entidad       : pasos de la entidad (bucle)
    public AudioClip respiracionEntidad; // respiracion_entidad : respiracion de la entidad (bucle)
    public AudioClip latido;             // latido              : latido de persecucion (un "lub-dub", ~0,4 s)
    public AudioClip rugidoEntidad;      // rugido_entidad      : rugido de la entidad al detectar al jugador (una vez al pasar a persecucion)
    public AudioClip disparo;            // disparo_pistola     : pistola
    public AudioClip miniMonstruo;       // mini_monstruo       : grito / mordisco de los mini monstruos (ataque y muerte)
    public AudioClip gritoMuerte;        // grito_muerte        : grito del jugador al ser devorado (pantalla de derrota)
    public AudioClip golpeBarra;         // golpe_barra         : barra contra un enemigo
    public AudioClip clicArma;           // clic_arma           : barra contra pared o sin efecto
    public AudioClip impactoMini;        // impacto_mini        : mini monstruo herido
    public AudioClip mordisco;           // mordisco            : dano al jugador
    public AudioClip muerteMini;         // muerte_mini         : mini monstruo muere
    public AudioClip teletransporte;     // teletransporte      : pasos / whoosh al teletransportarse
    public AudioClip droneFondo;         // drone_fondo         : drone grave de la partida (bucle)
    public AudioClip victoria;           // victoria            : sonido breve al escapar
    public AudioClip derrota;            // derrota             : sonido breve al morir
    public AudioClip zumbidoLuz;         // zumbido_luz         : zumbido de cada luz parpadeante (bucle)
    public AudioClip chasquidoLuz;       // chasquido_luz       : chasquido corto del parpadeo
    public AudioClip musicaPersecucion;  // musica_persecucion  : tension al ser perseguido (bucle). Sin archivo, no hay musica.
    public AudioClip uiClick;            // ui_click            : clic de botones
    public AudioClip uiHover;            // ui_hover            : al apuntar un boton
    public AudioClip[] susurros;         // susurro_1, susurro_2, ... (cualquier nombre que empiece con "susurro")

    static BibliotecaSonidos instancia;

    public static BibliotecaSonidos Instancia
    {
        get
        {
            if (instancia == null) instancia = Resources.Load<BibliotecaSonidos>("BibliotecaSonidos");
            return instancia;
        }
    }
}
