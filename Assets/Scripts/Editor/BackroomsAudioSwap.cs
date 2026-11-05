using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Reemplaza los clips placeholder por audios reales puestos en Assets/Audio/Reales.
// Reconoce cada audio por su nombre de archivo (zumbido/hum, pasos/step, respiracion/breath, latido/heart) y:
//   1) lo importa para audio 3D en Quest (mono, Vorbis; los bucles se descomprimen en memoria comprimida),
//   2) lo asigna a EntityAudio (pasos, respiracion), ExitHum (zumbido) y PursuitFeedback (latido).
// El loop y el 3D no dependen del clip: los fijan los scripts (AudioSource.loop = true, spatialBlend = 1 en pasos,
// respiracion y zumbido; el latido es 2D y se dispara con PlayOneShot).
// Menu: Backrooms/Aplicar audios reales  y  Backrooms/Restaurar audios placeholder.
public static class BackroomsAudioSwap
{
    const string DirReales = "Assets/Audio/Reales";
    const string DirPlaceholders = "Assets/Audio/Placeholders";

    enum Ranura { Zumbido, Pasos, Respiracion, Latido }

    // Orden de evaluacion: el primero que coincida gana (evita que "heartbeat" caiga en otra ranura).
    static readonly KeyValuePair<Ranura, string[]>[] Palabras =
    {
        new KeyValuePair<Ranura, string[]>(Ranura.Latido, new[] { "latido", "heart", "beat", "pulse" }),
        new KeyValuePair<Ranura, string[]>(Ranura.Respiracion, new[] { "respir", "breath", "breathing", "aliento" }),
        new KeyValuePair<Ranura, string[]>(Ranura.Pasos, new[] { "paso", "step", "foot", "walk" }),
        new KeyValuePair<Ranura, string[]>(Ranura.Zumbido, new[] { "zumbido", "hum", "buzz", "fluoresc", "salida", "exit", "drone" }),
    };

    static readonly Dictionary<Ranura, string> Placeholder = new Dictionary<Ranura, string>
    {
        { Ranura.Zumbido, DirPlaceholders + "/zumbido_salida.wav" },
        { Ranura.Pasos, DirPlaceholders + "/pasos_entidad.wav" },
        { Ranura.Respiracion, DirPlaceholders + "/respiracion_entidad.wav" },
        { Ranura.Latido, DirPlaceholders + "/latido.wav" },
    };

    [MenuItem("Backrooms/Aplicar audios reales")]
    public static void Aplicar()
    {
        if (!AssetDatabase.IsValidFolder(DirReales))
        {
            AssetDatabase.CreateFolder("Assets/Audio", "Reales");
        }

        var elegidos = new Dictionary<Ranura, AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { DirReales }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string nombre = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            Ranura? r = Clasificar(nombre);
            if (r == null)
            {
                Debug.LogWarning("[AudioSwap] '" + path + "' no coincide con zumbido / pasos / respiracion / latido: se ignora.");
                continue;
            }
            if (elegidos.ContainsKey(r.Value))
            {
                Debug.LogWarning("[AudioSwap] Hay mas de un audio para '" + r.Value + "'; se usa " + AssetDatabase.GetAssetPath(elegidos[r.Value]) + " y se ignora " + path);
                continue;
            }
            ConfigurarImportacion(path, r.Value);
            elegidos[r.Value] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        if (elegidos.Count == 0)
        {
            Debug.LogWarning("[AudioSwap] No hay audios en " + DirReales + ". Pon ahi zumbido, pasos, respiracion y latido (wav/mp3/ogg) y vuelve a ejecutar.");
            return;
        }

        Asignar(elegidos);
        foreach (Ranura r in System.Enum.GetValues(typeof(Ranura)))
            if (!elegidos.ContainsKey(r)) Debug.LogWarning("[AudioSwap] Falta el audio real de '" + r + "': se mantiene el placeholder.");
    }

    [MenuItem("Backrooms/Restaurar audios placeholder")]
    public static void Restaurar()
    {
        var clips = new Dictionary<Ranura, AudioClip>();
        foreach (var kv in Placeholder) clips[kv.Key] = AssetDatabase.LoadAssetAtPath<AudioClip>(kv.Value);
        Asignar(clips);
    }

    static Ranura? Clasificar(string nombre)
    {
        foreach (var kv in Palabras)
            foreach (string p in kv.Value)
                if (nombre.Contains(p)) return kv.Key;
        return null;
    }

    static void ConfigurarImportacion(string path, Ranura ranura)
    {
        var imp = (AudioImporter)AssetImporter.GetAtPath(path);
        if (imp == null) return;

        bool espacial = ranura != Ranura.Latido; // el latido es 2D
        bool bucleLargo = ranura == Ranura.Zumbido || ranura == Ranura.Respiracion;

        imp.forceToMono = espacial;             // el audio 3D necesita fuentes mono
        imp.loadInBackground = true;
        var s = imp.defaultSampleSettings;
        s.preloadAudioData = true;
        s.loadType = bucleLargo ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.6f;
        s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
        imp.defaultSampleSettings = s;
        imp.SaveAndReimport();
    }

    static void Asignar(Dictionary<Ranura, AudioClip> clips)
    {
        var entidad = Object.FindObjectOfType<EntityAudio>(true);
        var salida = Object.FindObjectOfType<ExitHum>(true);
        var persecucion = Object.FindObjectOfType<PursuitFeedback>(true);

        foreach (var kv in clips)
        {
            AudioClip c = kv.Value;
            if (c == null) continue;
            switch (kv.Key)
            {
                case Ranura.Pasos:
                    if (entidad != null) { entidad.pasos = c; EditorUtility.SetDirty(entidad); }
                    if (c.length < 0.4f) Debug.LogWarning("[AudioSwap] 'pasos' dura " + c.length.ToString("F2") + " s: EntityAudio lo repite en bucle, conviene un ciclo de 1 s con 1-2 pasos.");
                    break;
                case Ranura.Respiracion:
                    if (entidad != null) { entidad.respiracion = c; EditorUtility.SetDirty(entidad); }
                    break;
                case Ranura.Zumbido:
                    if (salida != null) { salida.zumbido = c; EditorUtility.SetDirty(salida); }
                    break;
                case Ranura.Latido:
                    if (persecucion != null) { persecucion.latido = c; EditorUtility.SetDirty(persecucion); }
                    break;
            }
            Debug.Log("[AudioSwap] " + kv.Key + " -> " + AssetDatabase.GetAssetPath(c) + " (" + c.length.ToString("F2") + " s, " + c.channels + " ch)");
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
    }
}
