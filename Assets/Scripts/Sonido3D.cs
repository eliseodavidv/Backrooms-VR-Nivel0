using UnityEngine;
using UnityEngine.Audio;

public enum BusAudio { SFX, Ambiente, Musica }

// Punto unico para el audio del juego en Quest 2:
//  - Preparar(): deja una AudioSource con el espacializador de Meta XR Audio (HRTF), sin efectos despues del espacializador,
//    y la manda al grupo del AudioMixer "MezclaBackrooms" (SFX / Ambiente / Musica).
//  - EnPunto() / Reproducir2D(): reproducen un clip con un pequeno grupo de fuentes reutilizadas (sin crear objetos por sonido,
//    sin PlayClipAtPoint). Mantiene las voces bajas para cuidar la CPU del Quest.
// Si el mixer no existe (aun no se creo) todo sigue funcionando, solo sin eco de pasillo.
public static class Sonido3D
{
    static AudioMixer mezcla;
    static readonly AudioMixerGroup[] grupos = new AudioMixerGroup[3];
    static readonly string[] nombres = { "SFX", "Ambiente", "Musica" };

    public static AudioMixerGroup Grupo(BusAudio bus)
    {
        if (mezcla == null) mezcla = Resources.Load<AudioMixer>("MezclaBackrooms");
        if (mezcla == null) return null;
        int i = (int)bus;
        if (grupos[i] == null)
        {
            var g = mezcla.FindMatchingGroups("Master/" + nombres[i]);
            if (g != null && g.Length > 0) grupos[i] = g[0];
        }
        return grupos[i];
    }

    // espacial = true: 3D con espacializador de Meta. false: 2D (pegado a la cabeza), sin espacializar.
    public static void Preparar(AudioSource s, BusAudio bus, bool espacial, float distMin = 0f, float distMax = 0f)
    {
        if (s == null) return;
        s.outputAudioMixerGroup = Grupo(bus);
        s.spatialBlend = espacial ? 1f : 0f;
        s.spatialize = espacial;
        s.spatializePostEffects = false;   // el espacializador va antes del reverb del mixer
        if (espacial)
        {
            if (distMin > 0f) s.minDistance = distMin;
            if (distMax > 0f) s.maxDistance = distMax;
        }
    }

    public static void EnPunto(AudioClip clip, Vector3 punto, float volumen = 1f, BusAudio bus = BusAudio.SFX, float pitch = 1f)
    {
        if (clip == null) return;
        GestorAudio.Punto(clip, punto, volumen, bus, pitch);
    }

    public static void Reproducir2D(AudioClip clip, float volumen = 1f, BusAudio bus = BusAudio.SFX)
    {
        if (clip == null) return;
        GestorAudio.Plano(clip, volumen, bus);
    }
}

// Grupo de fuentes reutilizadas. Se crea solo la primera vez y muere con la escena (nada de DontDestroyOnLoad).
public class GestorAudio : MonoBehaviour
{
    const int Fuentes3D = 10;
    const int Fuentes2D = 3;

    static GestorAudio instancia;
    private AudioSource[] f3D, f2D;
    private int sig3, sig2;

    static GestorAudio Instancia
    {
        get
        {
            if (instancia == null)
            {
                var go = new GameObject("GestorAudio");
                instancia = go.AddComponent<GestorAudio>();
                instancia.Crear();
            }
            return instancia;
        }
    }

    void Crear()
    {
        f3D = new AudioSource[Fuentes3D];
        f2D = new AudioSource[Fuentes2D];
        for (int i = 0; i < Fuentes3D; i++) f3D[i] = Nueva("Fuente3D_" + i, true);
        for (int i = 0; i < Fuentes2D; i++) f2D[i] = Nueva("Fuente2D_" + i, false);
    }

    AudioSource Nueva(string nombre, bool espacial)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.dopplerLevel = 0f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 1f;
        s.maxDistance = 25f;
        s.spatialBlend = espacial ? 1f : 0f;
        s.spatialize = espacial;
        s.spatializePostEffects = false;
        return s;
    }

    // Una fuente libre; si todas suenan, se reutiliza la mas antigua.
    static AudioSource Tomar(AudioSource[] lista, ref int sig)
    {
        foreach (var s in lista) if (!s.isPlaying) return s;
        sig = (sig + 1) % lista.Length;
        return lista[sig];
    }

    public static void Punto(AudioClip clip, Vector3 pos, float vol, BusAudio bus, float pitch)
    {
        var g = Instancia;
        var s = Tomar(g.f3D, ref g.sig3);
        s.Stop();
        s.transform.position = pos;
        s.outputAudioMixerGroup = Sonido3D.Grupo(bus);
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(vol);
        s.clip = clip;
        s.Play();
    }

    public static void Plano(AudioClip clip, float vol, BusAudio bus)
    {
        var g = Instancia;
        var s = Tomar(g.f2D, ref g.sig2);
        s.Stop();
        s.outputAudioMixerGroup = Sonido3D.Grupo(bus);
        s.pitch = 1f;
        s.volume = Mathf.Clamp01(vol);
        s.clip = clip;
        s.Play();
    }
}
