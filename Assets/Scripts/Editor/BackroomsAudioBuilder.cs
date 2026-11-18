using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Audio;

// Audio del juego, parte del editor:
//  1) Backrooms/Crear mezcla de audio: crea Assets/Resources/MezclaBackrooms.mixer con los grupos SFX, Ambiente y Musica.
//     SFX y Ambiente llevan un "SFX Reverb" barato de pasillo largo; Musica va seca (latido, sonidos de interfaz, stings).
//  2) Biblioteca de audios reales: al poner / cambiar / borrar archivos en Assets/Audio/Reales/ se reconstruye sola
//     Assets/Resources/BibliotecaSonidos.asset (por nombre de archivo, ver Nombres). Tambien antes de compilar el APK.
//  3) Importacion: los sonidos 3D se fuerzan a mono, y para Quest se comprimen en Vorbis (bucles largos en memoria comprimida).
public static class BackroomsAudioBuilder
{
    const string RutaMezcla = "Assets/Resources/MezclaBackrooms.mixer";
    const string RutaBiblioteca = "Assets/Resources/BibliotecaSonidos.asset";
    public const string DirReales = "Assets/Audio/Reales";

    // nombre de archivo (sin extension, en minusculas) -> campo de BibliotecaSonidos
    public static readonly Dictionary<string, string> Nombres = new Dictionary<string, string>
    {
        { "zumbido_salida", "zumbidoSalida" },
        { "pasos_entidad", "pasosEntidad" },
        { "respiracion_entidad", "respiracionEntidad" },
        { "latido", "latido" },
        { "rugido_entidad", "rugidoEntidad" },
        { "disparo_pistola", "disparo" },
        { "mini_monstruo", "miniMonstruo" },
        { "grito_muerte", "gritoMuerte" },
        { "golpe_barra", "golpeBarra" },
        { "clic_arma", "clicArma" },
        { "impacto_mini", "impactoMini" },
        { "mordisco", "mordisco" },
        { "muerte_mini", "muerteMini" },
        { "teletransporte", "teletransporte" },
        { "drone_fondo", "droneFondo" },
        { "victoria", "victoria" },
        { "derrota", "derrota" },
        { "zumbido_luz", "zumbidoLuz" },
        { "chasquido_luz", "chasquidoLuz" },
        { "musica_persecucion", "musicaPersecucion" },
        { "ui_click", "uiClick" },
        { "ui_hover", "uiHover" },
    };

    // ================= mezcla =================
    [MenuItem("Backrooms/Crear mezcla de audio")]
    public static void CrearMezcla()
    {
        Directory.CreateDirectory("Assets/Resources");
        if (AssetDatabase.LoadAssetAtPath<AudioMixer>(RutaMezcla) != null)
        {
            Debug.Log("[Audio] La mezcla ya existe: " + RutaMezcla + " (si quieres rehacerla, borrala y vuelve a ejecutar).");
            return;
        }

        var asm = typeof(AudioImporter).Assembly;
        var tCtl = asm.GetType("UnityEditor.Audio.AudioMixerController");
        var tEff = asm.GetType("UnityEditor.Audio.AudioMixerEffectController");
        const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        if (tCtl == null || tEff == null) { Debug.LogError("[Audio] Esta version de Unity no expone el AudioMixerController: crea el mixer a mano (ver LEEME)."); return; }

        object ctl = tCtl.GetMethod("CreateMixerControllerAtPath", bf).Invoke(null, new object[] { RutaMezcla });
        object master = tCtl.GetProperty("masterGroup", bf).GetValue(ctl);
        object snapshot = tCtl.GetProperty("TargetSnapshot", bf).GetValue(ctl);

        object Grupo(string nombre)
        {
            object g = tCtl.GetMethod("CreateNewGroup", bf).Invoke(ctl, new object[] { nombre, false });
            tCtl.GetMethod("AddChildToParent", bf).Invoke(ctl, new[] { g, master });
            return g;
        }

        void Reverb(object grupo, Dictionary<string, float> p)
        {
            object eff = System.Activator.CreateInstance(tEff, new object[] { "SFX Reverb" });
            tEff.GetMethod("PreallocateGUIDs", bf).Invoke(eff, null);
            AssetDatabase.AddObjectToAsset((Object)eff, (Object)ctl);
            // indice 1: despues de la atenuacion (volumen) del grupo
            grupo.GetType().GetMethod("InsertEffect", bf).Invoke(grupo, new object[] { eff, 1 });
            foreach (var kv in p)
                tEff.GetMethod("SetValueForParameter", bf).Invoke(eff, new object[] { ctl, snapshot, kv.Key, kv.Value });
        }

        var sfx = Grupo("SFX");
        var amb = Grupo("Ambiente");
        Grupo("Musica");

        // Pasillo largo y cerrado (SFX): cola de ~2,2 s apagada en agudos; poca densidad = barato.
        Reverb(sfx, new Dictionary<string, float>
        {
            { "Dry Level", 0f }, { "Room", -1700f }, { "Room HF", -1600f }, { "Decay Time", 2.2f }, { "Decay HF Ratio", 0.55f },
            { "Reflections", -1100f }, { "Reflect Delay", 0.02f }, { "Reverb", -500f }, { "Reverb Delay", 0.04f },
            { "Diffusion", 70f }, { "Density", 60f },
        });
        // Ambiente: misma sala pero mas lejana y corta, para que el drone y los zumbidos "esten en el lugar".
        Reverb(amb, new Dictionary<string, float>
        {
            { "Dry Level", 0f }, { "Room", -2700f }, { "Room HF", -1800f }, { "Decay Time", 1.5f }, { "Decay HF Ratio", 0.5f },
            { "Reflections", -1800f }, { "Reflect Delay", 0.015f }, { "Reverb", -1100f }, { "Reverb Delay", 0.03f },
            { "Diffusion", 60f }, { "Density", 50f },
        });

        tCtl.GetMethod("OnSubAssetChanged", bf).Invoke(ctl, null);
        EditorUtility.SetDirty((Object)ctl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(RutaMezcla, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Audio] Mezcla creada: " + RutaMezcla + " (grupos SFX y Ambiente con reverb de pasillo; Musica sin efectos).");
    }

    // ================= biblioteca de audios reales =================
    [MenuItem("Backrooms/Reconstruir biblioteca de audios reales")]
    public static void ReconstruirBiblioteca()
    {
        Directory.CreateDirectory("Assets/Resources");
        Directory.CreateDirectory(DirReales);
        var b = AssetDatabase.LoadAssetAtPath<BibliotecaSonidos>(RutaBiblioteca);
        if (b == null)
        {
            b = ScriptableObject.CreateInstance<BibliotecaSonidos>();
            AssetDatabase.CreateAsset(b, RutaBiblioteca);
        }

        var porNombre = new Dictionary<string, AudioClip>();
        var susurros = new List<KeyValuePair<string, AudioClip>>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { DirReales }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            if (n.StartsWith("susurro")) susurros.Add(new KeyValuePair<string, AudioClip>(n, clip));
            else porNombre[n] = clip;
        }

        int asignados = 0;
        var faltan = new List<string>();
        foreach (var kv in Nombres)
        {
            FieldInfo f = typeof(BibliotecaSonidos).GetField(kv.Value);
            porNombre.TryGetValue(kv.Key, out AudioClip c);
            f.SetValue(b, c);
            if (c != null) asignados++; else faltan.Add(kv.Key);
        }
        susurros.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
        b.susurros = susurros.ConvertAll(x => x.Value).ToArray();
        if (b.susurros.Length > 0) asignados++;
        else faltan.Add("susurro_1 (y susurro_2, ...)");

        // Nombres que no coinciden con ninguna casilla: se avisa para que no se pierdan en silencio.
        foreach (var kv in porNombre)
            if (!Nombres.ContainsKey(kv.Key)) Debug.LogWarning("[Audio] '" + kv.Key + "' en " + DirReales + " no coincide con ningun nombre de la lista: se ignora.");

        EditorUtility.SetDirty(b);
        AssetDatabase.SaveAssets();
        Debug.Log("[Audio] Biblioteca: " + asignados + " sonidos reales asignados. Sin archivo (suena el generado): " + string.Join(", ", faltan));
    }

    // ================= antes de compilar el APK =================
    class AntesDeCompilar : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPreprocessBuild(BuildReport informe) { ReconstruirBiblioteca(); }
    }
}

// Reconstruye la biblioteca cuando cambia algo en Assets/Audio/Reales y ajusta la importacion de esos archivos.
public class AudioRealesPostprocessor : AssetPostprocessor
{
    // Solo para medir el volumen de un bucle largo: descomprime todo durante el analisis (no usar en la configuracion normal).
    public static bool ForzarDescomprimir;

    static bool EnReales(string ruta) { return ruta.Replace('\\', '/').StartsWith(BackroomsAudioBuilder.DirReales + "/"); }

    void OnPreprocessAudio()
    {
        if (!EnReales(assetPath)) return;
        var imp = (AudioImporter)assetImporter;
        string n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

        // Mono para todo (ahorra la mitad del peso y lo 3D lo exige); estereo solo para musica y stings.
        bool estereo = n.StartsWith("musica") || n == "victoria" || n == "derrota";
        imp.forceToMono = !estereo;

        // Quest: Vorbis calidad media. Los bucles LARGOS (drone de 112 s, musica, pasos, respiracion) quedan comprimidos en memoria;
        // todo lo demas (efectos cortos y el zumbido de luz) se descomprime al cargar: no gasta CPU al sonar y se puede recortar y
        // normalizar al arrancar (SonidosPlaceholder). Los cortos cargan sincronos para estar listos antes de jugar.
        bool largo = n.StartsWith("drone") || n.StartsWith("musica") || n.StartsWith("respiracion") || n.StartsWith("pasos");
        imp.loadInBackground = largo;
        var s = imp.defaultSampleSettings;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.5f;
        s.loadType = (largo && !ForzarDescomprimir) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        imp.defaultSampleSettings = s;
        imp.SetOverrideSampleSettings("Android", s);
    }

    static void OnPostprocessAllAssets(string[] importados, string[] borrados, string[] movidos, string[] movidosDesde)
    {
        bool cambio = false;
        foreach (var lista in new[] { importados, borrados, movidos, movidosDesde })
            foreach (var p in lista) if (EnReales(p)) cambio = true;
        if (cambio) EditorApplication.delayCall += BackroomsAudioBuilder.ReconstruirBiblioteca;
    }
}
