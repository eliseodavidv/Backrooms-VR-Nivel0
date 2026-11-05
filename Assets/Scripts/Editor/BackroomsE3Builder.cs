using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Construye el bloque E3 en Backrooms_Main: clips placeholder, zonas de nicho, hotspots en nichos,
// y cableado de EntityAI / audio / feedback de persecucion. Menu: Backrooms/Construir E3.
public static class BackroomsE3Builder
{
    const string DirAudio = "Assets/Audio/Placeholders";
    const int Fs = 22050;

    [MenuItem("Backrooms/Construir E3")]
    public static void Construir()
    {
        var clips = GenerarClips();
        CrearZonaEnPrefabNicho();
        CrearHotspotsEnNichos();
        CablearEscena(clips);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BackroomsE3Builder] E3 construido.");
    }

    // ---------- audio placeholder ----------
    struct Clips { public AudioClip zumbido, pasos, respiracion, latido; }

    static Clips GenerarClips()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
        if (!AssetDatabase.IsValidFolder(DirAudio)) AssetDatabase.CreateFolder("Assets/Audio", "Placeholders");

        var rnd = new System.Random(7);
        float Ruido() { return (float)(rnd.NextDouble() * 2.0 - 1.0); }

        // Zumbido de la salida: 120 Hz + armonicos, 2 s con ciclos enteros (loop sin clic).
        var zum = new float[Fs * 2];
        for (int i = 0; i < zum.Length; i++)
        {
            float t = i / (float)Fs;
            float am = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 2f * t);
            zum[i] = 0.5f * am * (Mathf.Sin(2f * Mathf.PI * 120f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 240f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 360f * t)) / 1.75f;
        }

        // Pasos: dos golpes sordos por segundo (a 0 y 0,5 s).
        var pas = new float[Fs];
        foreach (float inicio in new[] { 0f, 0.5f })
        {
            int i0 = (int)(inicio * Fs);
            float lp = 0f;
            for (int i = 0; i < (int)(0.3f * Fs) && i0 + i < pas.Length; i++)
            {
                float t = i / (float)Fs;
                lp += 0.15f * (Ruido() - lp);
                float env = Mathf.Exp(-t * 18f);
                pas[i0 + i] += env * (0.7f * Mathf.Sin(2f * Mathf.PI * 70f * t) + 0.6f * lp * 3f);
            }
        }

        // Respiracion: ruido filtrado con envolvente de inhalar (2 s) y exhalar (2 s).
        var res = new float[Fs * 4];
        float f = 0f;
        for (int i = 0; i < res.Length; i++)
        {
            float t = i / (float)Fs;
            f += 0.08f * (Ruido() - f);
            float fase = (t % 2f) / 2f;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * fase), 2f) * (t < 2f ? 0.7f : 1f);
            res[i] = f * 2.2f * env;
        }

        // Latido: doble golpe grave (0 y 0,22 s), 0,8 s.
        var lat = new float[(int)(0.8f * Fs)];
        for (int g = 0; g < 2; g++)
        {
            int i0 = (int)((g == 0 ? 0f : 0.22f) * Fs);
            float amp = g == 0 ? 1f : 0.7f;
            for (int i = 0; i < (int)(0.3f * Fs) && i0 + i < lat.Length; i++)
            {
                float t = i / (float)Fs;
                lat[i0 + i] += amp * Mathf.Exp(-t * 22f) * Mathf.Sin(2f * Mathf.PI * 55f * t);
            }
        }

        return new Clips
        {
            zumbido = Guardar("zumbido_salida", zum),
            pasos = Guardar("pasos_entidad", pas),
            respiracion = Guardar("respiracion_entidad", res),
            latido = Guardar("latido", lat),
        };
    }

    static AudioClip Guardar(string nombre, float[] muestras)
    {
        string path = DirAudio + "/" + nombre + ".wav";
        float pico = 0f;
        foreach (var m in muestras) pico = Mathf.Max(pico, Mathf.Abs(m));
        float norm = pico > 0f ? 0.8f / pico : 1f;

        using (var fs = new FileStream(path, FileMode.Create))
        using (var w = new BinaryWriter(fs))
        {
            int datos = muestras.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + datos);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16);
            w.Write((short)1); w.Write((short)1); w.Write(Fs); w.Write(Fs * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(datos);
            foreach (var m in muestras) w.Write((short)(Mathf.Clamp(m * norm, -1f, 1f) * 32767f));
        }
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // ---------- nichos ----------
    static void CrearZonaEnPrefabNicho()
    {
        const string path = "Assets/Prefabs/Pasillo_Nicho.prefab";
        var raiz = PrefabUtility.LoadPrefabContents(path);
        var nicho = raiz.transform.Find("Nicho");
        if (nicho != null && nicho.Find("ZonaEscondite") == null)
        {
            var z = new GameObject("ZonaEscondite");
            z.transform.SetParent(nicho, false);
            var bc = z.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(2.2f, 1.35f, 0f);
            bc.size = new Vector3(1.3f, 2.7f, 1.5f);
            z.AddComponent<NichoZona>();
        }
        PrefabUtility.SaveAsPrefabAsset(raiz, path);
        PrefabUtility.UnloadPrefabContents(raiz);
    }

    static void CrearHotspotsEnNichos()
    {
        var hsRaiz = GameObject.Find("Hotspots");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TeleportHotspot.prefab");
        foreach (Transform celda in GameObject.Find("Laberinto").transform.Find("Modulos"))
        {
            if (!celda.name.StartsWith("Nicho_")) continue;
            string nombre = "TeleportHotspot_" + celda.name;
            if (hsRaiz.transform.Find(nombre) != null) continue;
            var h = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hsRaiz.transform);
            h.name = nombre;
            h.transform.position = celda.TransformPoint(new Vector3(2.2f, 0.01f, 0f));
            h.transform.rotation = Quaternion.identity;
            var ring = h.transform.Find("Ring");
            if (ring != null && ring.GetComponent<MeshRenderer>() != null) ring.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    // ---------- escena ----------
    static void CablearEscena(Clips clips)
    {
        var cabeza = GameObject.Find("CenterEyeAnchor");
        var entidad = GameObject.Find("Entidad");
        var rig = GameObject.Find("OVRCameraRigInteraction");

        // EntityAI
        var ia = entidad.GetComponent<EntityAI>();
        var so = new SerializedObject(ia);
        so.FindProperty("jugador").objectReferenceValue = cabeza.transform;
        so.FindProperty("radioDeteccion").floatValue = 8f;
        so.FindProperty("radioSospecha").floatValue = 16f;
        so.FindProperty("anguloVision").floatValue = 100f;
        so.FindProperty("capasObstaculo").FindPropertyRelative("m_Bits").intValue = 1; // Default (muros, piso, techo)
        so.ApplyModifiedProperties();

        // Audio de la entidad
        var ea = entidad.GetComponent<EntityAudio>() ?? entidad.AddComponent<EntityAudio>();
        ea.pasos = clips.pasos; ea.respiracion = clips.respiracion;
        EditorUtility.SetDirty(ea);

        var pc = entidad.GetComponent<EntityProximityCue>() ?? entidad.AddComponent<EntityProximityCue>();
        pc.jugador = cabeza.transform;
        EditorUtility.SetDirty(pc);

        // Ruido del jugador (teletransportes)
        var ne = rig.GetComponent<PlayerNoiseEmitter>() ?? rig.AddComponent<PlayerNoiseEmitter>();
        ne.cabeza = cabeza.transform;
        EditorUtility.SetDirty(ne);

        // Zumbido de la salida
        var salida = GameObject.Find("Salida");
        var zumbidoGo = salida.transform.Find("Zumbido");
        if (zumbidoGo == null)
        {
            zumbidoGo = new GameObject("Zumbido").transform;
            zumbidoGo.SetParent(salida.transform, false);
        }
        var puerta = salida.transform.Find("Puerta_Luz");
        zumbidoGo.position = puerta.position + new Vector3(-0.5f, 0f, 0f);
        var hum = zumbidoGo.GetComponent<ExitHum>() ?? zumbidoGo.gameObject.AddComponent<ExitHum>();
        hum.jugador = cabeza.transform;
        hum.zumbido = clips.zumbido;
        hum.puerta = puerta.GetComponent<Renderer>();
        EditorUtility.SetDirty(hum);
        // La puerta cambia su emision en runtime: fuera de static batching.
        GameObjectUtility.SetStaticEditorFlags(puerta.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.OccludeeStatic);

        // Borde rojo + latido + vibracion
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materiales/Backrooms/Mat_BordeRojo.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Backrooms/BordeRojo"));
            AssetDatabase.CreateAsset(mat, "Assets/Materiales/Backrooms/Mat_BordeRojo.mat");
        }
        var bordeT = cabeza.transform.Find("BordeRojo");
        if (bordeT == null)
        {
            var q = new GameObject("BordeRojo");
            q.transform.SetParent(cabeza.transform, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            q.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            q.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            bordeT = q.transform;
        }
        var fbGo = GameObject.Find("Feedback_Persecucion") ?? new GameObject("Feedback_Persecucion");
        var fb = fbGo.GetComponent<PursuitFeedback>() ?? fbGo.AddComponent<PursuitFeedback>();
        fb.entidad = ia;
        fb.jugador = cabeza.transform;
        fb.borde = bordeT.GetComponent<Renderer>();
        fb.latido = clips.latido;
        EditorUtility.SetDirty(fb);

        AssetDatabase.SaveAssets();
    }
}
