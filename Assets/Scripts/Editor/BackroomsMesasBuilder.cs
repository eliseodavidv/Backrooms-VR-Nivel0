using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Mesas de las armas (BARRA y PISTOLA) mas reales y sucias, baratas para Quest 2:
//  - Texturas procedurales 512x512 con mapa de relieve (madera vieja, metal oxidado, tela y papel sucios),
//  - patas con grosor, pies, faldon, refuerzos, tela colgando, papeles, polvo y manchas (decals) y mancha en el suelo,
//  - rotulo gastado (placa oscura + fuente sucia),
//  - shader Backrooms/ArmaLitTex (un pass, sin luces en tiempo real, sin sombras) y todo BatchingStatic.
// El Tablero conserva su tamano y su collider: las armas se siguen apoyando y agarrando igual.
// Menu: Backrooms/Mejorar mesas (idempotente).
public static class BackroomsMesasBuilder
{
    const string DirTex = "Assets/Texturas/Mesas";
    const string DirMat = "Assets/Materiales/Backrooms";
    const int N = 512;

    // ================= entrada =================
    [MenuItem("Backrooms/Mejorar mesas")]
    public static void Construir()
    {
        Directory.CreateDirectory(DirTex);

        // ---- texturas ----
        Texture2D nMad, nOxi, nTela;
        var cMad = Madera(out nMad);
        var cOxi = Oxido(out nOxi);
        var cTela = Tela(out nTela);
        var cPapel = Papel();
        var cPolvo = PolvoManchas();
        var cSuelo = ManchaSuelo();

        var tMad = Guardar("madera_vieja_color", cMad, false);
        var tMadN = Guardar("madera_vieja_normal", nMad, true);
        var tOxi = Guardar("metal_oxidado_color", cOxi, false);
        var tOxiN = Guardar("metal_oxidado_normal", nOxi, true);
        var tTela = Guardar("tela_sucia_color", cTela, false);
        var tTelaN = Guardar("tela_sucia_normal", nTela, true);
        var tPapel = Guardar("papel_sucio_color", cPapel, false);
        var tPolvo = Guardar("polvo_manchas", cPolvo, false);
        var tSuelo = Guardar("mancha_suelo", cSuelo, false);

        // ---- materiales (todos baratos: ArmaLitTex = un pass, luz fija; decals sin escribir profundidad) ----
        var matMad = MatLit("Mat_MesaVieja_Madera", tMad, tMadN, 1.15f, 0f, 0.12f, new Color(0.95f, 0.92f, 0.88f));
        var matMadOsc = MatLit("Mat_MesaVieja_MaderaOscura", tMad, tMadN, 1.15f, 0f, 0.10f, new Color(0.62f, 0.58f, 0.54f));
        var matOxi = MatLit("Mat_MesaVieja_Oxido", tOxi, tOxiN, 1.0f, 0.30f, 0.22f, Color.white);
        var matTela = MatLit("Mat_MesaVieja_Tela", tTela, tTelaN, 0.8f, 0f, 0.05f, Color.white);
        var matPapel = MatLit("Mat_MesaVieja_Papel", tPapel, null, 0f, 0f, 0.05f, Color.white);
        var matPolvo = MatDecal("Mat_MesaVieja_Polvo", tPolvo);
        var matSuelo = MatDecal("Mat_MesaVieja_ManchaSuelo", tSuelo);

        // ---- mesas ----
        // Primero se recopilan las mesas (reconstruir borra y crea hijos: no se puede recorrer la escena a la vez).
        var mesas = new System.Collections.Generic.List<Transform>();
        var armas = GameObject.Find("Armas");
        if (armas != null)
            foreach (Transform t in armas.transform)
                if (t.name.StartsWith("Mesa_")) mesas.Add(t);
        int n = 0;
        foreach (var t in mesas)
        {
            ReconstruirMesa(t, matMad, matMadOsc, matOxi, matTela, matPapel, matPolvo, matSuelo, n);
            RestilarRotulo(t.Find("Rotulo"));
            n++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[BackroomsMesasBuilder] " + n + " mesas mejoradas (madera vieja, metal oxidado, tela, papel, polvo, manchas, rotulo gastado).");
    }

    // ================= geometria =================
    static void ReconstruirMesa(Transform mesa, Material madera, Material maderaOsc, Material oxido, Material tela, Material papel,
                                Material polvo, Material sueloMancha, int idx)
    {
        // Se conserva solo el Tablero (collider donde se apoyan las armas) y el Rotulo; lo demas se rehace.
        for (int i = mesa.childCount - 1; i >= 0; i--)
        {
            var c = mesa.GetChild(i);
            if (c.name == "Tablero" || c.name == "Rotulo") continue;
            Object.DestroyImmediate(c.gameObject);
        }

        var tablero = mesa.Find("Tablero");
        tablero.GetComponent<MeshRenderer>().sharedMaterial = madera;
        Fijar(tablero.gameObject);

        // Faldon (4 tablas bajo el tablero)
        foreach (var sx in new[] { -1f, 1f })
            Caja("Faldon", mesa, new Vector3(0.215f * sx, 0.675f, 0f), new Vector3(0.02f, 0.09f, 0.80f), maderaOsc);
        foreach (var sz in new[] { -1f, 1f })
            Caja("Faldon", mesa, new Vector3(0f, 0.675f, 0.415f * sz), new Vector3(0.40f, 0.09f, 0.02f), maderaOsc);

        // Patas gruesas de metal oxidado + pies
        foreach (var sx in new[] { -1f, 1f })
            foreach (var sz in new[] { -1f, 1f })
            {
                Caja("Pata", mesa, new Vector3(0.20f * sx, 0.35f, 0.40f * sz), new Vector3(0.08f, 0.70f, 0.08f), oxido);
                Caja("Pie", mesa, new Vector3(0.20f * sx, 0.012f, 0.40f * sz), new Vector3(0.115f, 0.024f, 0.115f), oxido);
            }

        // Refuerzos bajos (listones)
        foreach (var sx in new[] { -1f, 1f })
            Caja("Refuerzo", mesa, new Vector3(0.20f * sx, 0.18f, 0f), new Vector3(0.03f, 0.04f, 0.74f), oxido);
        Caja("Refuerzo", mesa, new Vector3(0f, 0.18f, 0f), new Vector3(0.34f, 0.04f, 0.03f), oxido);

        // Tela sucia sobre el tablero + borde colgando en un extremo
        var tl = Caja("Tela", mesa, new Vector3(0.01f, 0.754f, 0.02f), new Vector3(0.38f, 0.006f, 0.84f), tela);
        tl.transform.localRotation = Quaternion.Euler(0f, 1.5f + idx * 1.2f, 0f);
        Caja("TelaColgando", mesa, new Vector3(0.01f, 0.665f, -0.452f), new Vector3(0.32f, 0.17f, 0.006f), tela);

        // Papeles sucios (uno cerca de cada esquina libre; el arma queda en el centro)
        Plano("Papel", mesa, new Vector3(0.12f, 0.7585f, 0.31f), new Vector2(0.17f, 0.24f), -18f + idx * 9f, papel, false);
        Plano("Papel", mesa, new Vector3(-0.12f, 0.7585f, -0.30f), new Vector2(0.13f, 0.18f), 31f - idx * 7f, papel, false);

        // Polvo + manchas oscuras sobre todo el tablero (decal) y mancha en el suelo bajo la mesa (decal)
        Plano("PolvoManchas", mesa, new Vector3(0f, 0.7605f, 0f), new Vector2(0.50f, 0.90f), 0f, polvo, true);
        Plano("ManchaSuelo", mesa, new Vector3(0f, 0.006f, 0f), new Vector2(1.15f, 1.45f), idx * 37f, sueloMancha, true);
    }

    static GameObject Caja(string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());     // solo el Tablero tiene collider
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        Config(go, mat);
        return go;
    }

    // Plano horizontal (Quad girado para mirar hacia arriba).
    static GameObject Plano(string nombre, Transform padre, Vector3 pos, Vector2 tam, float giroY, Material mat, bool decal)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = nombre;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(90f, giroY, 0f);
        go.transform.localScale = new Vector3(tam.x, tam.y, 1f);
        Config(go, mat);
        return go;
    }

    static void Config(GameObject go, Material mat)
    {
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        Fijar(go);
    }

    // Decoracion fija: batching estatico (junta las piezas con el mismo material en pocos draw calls). Sin ContributeGI: no exige rehornear.
    static void Fijar(GameObject go)
    {
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
    }

    // ================= rotulo gastado =================
    static void RestilarRotulo(Transform rot)
    {
        if (rot == null) return;

        // Fondo: placa oscura (la misma de los menus) + marco mas oscuro detras.
        var placa = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Texturas/Menu/placa_oscura.png");
        var matPlaca = MatUnlitTex("Mat_RotuloGastado", placa);
        var matMarco = MatUnlitColor("Mat_RotuloMarco", new Color(0.03f, 0.025f, 0.015f));
        var fondo = rot.Find("Fondo");
        if (fondo != null)
        {
            fondo.GetComponent<MeshRenderer>().sharedMaterial = matPlaca;
            var viejoMarco = rot.Find("Marco");
            if (viejoMarco != null) Object.DestroyImmediate(viejoMarco.gameObject);
            var marco = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marco.name = "Marco";
            Object.DestroyImmediate(marco.GetComponent<Collider>());
            marco.transform.SetParent(rot, false);
            marco.transform.localPosition = fondo.localPosition + new Vector3(0f, 0f, 0.004f);
            marco.transform.localRotation = fondo.localRotation;
            marco.transform.localScale = new Vector3(fondo.localScale.x + 0.04f, fondo.localScale.y + 0.04f, 1f);
            var mr = marco.GetComponent<MeshRenderer>();
            mr.sharedMaterial = matMarco;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // Titulo: fuente sucia, amarillo apagado. Se achica si no cabe en la placa.
        var titulo = rot.Find("Titulo");
        if (titulo != null)
        {
            var tm = titulo.GetComponent<TextMesh>();
            var rubik = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Menu/RubikDirt-Regular.ttf");
            if (tm != null && rubik != null)
            {
                tm.font = rubik;
                titulo.GetComponent<MeshRenderer>().sharedMaterial = rubik.material;
                tm.color = new Color(0.80f, 0.68f, 0.22f);
                float limite = fondo != null ? fondo.localScale.x - 0.12f : 0.70f;
                var b = titulo.GetComponent<MeshRenderer>().bounds;
                if (b.size.x > limite && b.size.x > 0.001f) titulo.localScale *= limite / b.size.x;
            }
        }
        var detalle = rot.Find("Detalle");
        if (detalle != null)
        {
            var tm = detalle.GetComponent<TextMesh>();
            if (tm != null) tm.color = new Color(0.72f, 0.68f, 0.56f);
        }
        Fijar2(rot.gameObject);
    }

    static void Fijar2(GameObject go) { EditorUtility.SetDirty(go); }

    // ================= materiales =================
    static Material Cargar(string nombre, string shader)
    {
        string mp = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, mp);
        }
        m.shader = Shader.Find(shader);
        return m;
    }

    static Material MatLit(string nombre, Texture2D color, Texture2D normal, float fuerzaNormal, float metalico, float brillo, Color tinte)
    {
        var m = Cargar(nombre, "Backrooms/ArmaLitTex");
        m.SetTexture("_MainTex", color);
        m.SetColor("_Color", tinte);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", fuerzaNormal); }
        m.SetFloat("_Metallic", metalico);
        m.SetFloat("_Glossiness", brillo);
        // Mas oscuro que las armas: encaja con el pasillo sucio.
        m.SetFloat("_Ambient", 0.95f);
        m.SetFloat("_Key", 0.60f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatDecal(string nombre, Texture2D tex)
    {
        var m = Cargar(nombre, "Backrooms/Decal");
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatUnlitTex(string nombre, Texture2D tex)
    {
        var m = Cargar(nombre, "Unlit/Texture");
        m.SetTexture("_MainTex", tex);
        m.color = new Color(0.9f, 0.9f, 0.9f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MatUnlitColor(string nombre, Color c)
    {
        var m = Cargar(nombre, "Unlit/Color");
        m.color = c;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ================= guardado de texturas =================
    static Texture2D Guardar(string nombre, Texture2D t, bool normal)
    {
        string path = DirTex + "/" + nombre + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.filterMode = FilterMode.Bilinear;
        imp.anisoLevel = 2;
        imp.alphaIsTransparency = !normal;
        imp.maxTextureSize = 512;
        imp.textureCompression = TextureImporterCompression.Compressed;   // ASTC en Quest
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ================= procedural =================

    // Smoothstep de shader: 0 por debajo de a, 1 por encima de b (admite a > b para invertir). NO es Mathf.SmoothStep (que interpola).
    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    static float Fbm(float x, float y, int octavas, int seed)
    {
        float s = 0f, a = 0.5f, f = 1f, norm = 0f;
        for (int i = 0; i < octavas; i++)
        {
            s += a * Mathf.PerlinNoise(x * f + seed * 17.13f, y * f + seed * 9.71f);
            norm += a;
            a *= 0.5f;
            f *= 2f;
        }
        return s / norm;
    }

    static Texture2D Normal(float[] h, float fuerza)
    {
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float hl = h[y * N + (x + N - 1) % N], hr = h[y * N + (x + 1) % N];
                float hd = h[((y + N - 1) % N) * N + x], hu = h[((y + 1) % N) * N + x];
                var n = new Vector3((hl - hr) * fuerza, (hd - hu) * fuerza, 1f).normalized;
                px[y * N + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    static void Raya(Color[] col, float[] h, Vector2 a, Vector2 b, float radio, float aclarar, float hondo)
    {
        float len = Vector2.Distance(a, b);
        int pasos = Mathf.Max(1, Mathf.CeilToInt(len));
        for (int i = 0; i <= pasos; i++)
        {
            var p = Vector2.Lerp(a, b, i / (float)pasos);
            int x0 = Mathf.FloorToInt(p.x - radio), x1 = Mathf.CeilToInt(p.x + radio);
            int y0 = Mathf.FloorToInt(p.y - radio), y1 = Mathf.CeilToInt(p.y + radio);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    int xx = (x + N) % N, yy = (y + N) % N;
                    col[yy * N + xx] = col[yy * N + xx] + new Color(aclarar, aclarar, aclarar, 0f);
                    h[yy * N + xx] -= hondo;
                }
        }
    }

    // Madera vieja: 4 tablas con veta, juntas, manchas oscuras, polvo, rayones, nudos y bordes desgastados.
    static Texture2D Madera(out Texture2D normal)
    {
        var col = new Color[N * N];
        var h = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                int plank = Mathf.Min(3, (int)(u * 4f));
                float lu = u * 4f - plank;
                float pb = 0.82f + 0.30f * Hash(plank, 3, 5);
                float v2 = v + plank * 0.37f;
                float grain = Fbm(lu * 14f + plank * 5f, v2 * 1.2f, 4, 3);
                float fine = Mathf.PerlinNoise(lu * 90f + plank * 13f, v2 * 3f);
                float g = Mathf.Lerp(0.55f, 1.10f, grain * 0.6f + fine * 0.4f);
                float vein = Mathf.Pow(Mathf.Abs(Mathf.Sin((lu * 22f + grain * 3f) * Mathf.PI)), 6f) * 0.35f;
                var c = new Color(0.45f, 0.31f, 0.17f) * (pb * g * (1f - vein));
                float st = Suave(0.55f, 0.80f, Fbm(u * 3f + 9f, v * 3f + 4f, 4, 11));
                c *= 1f - st * 0.6f;
                float seam = Suave(0.05f, 0f, Mathf.Min(lu, 1f - lu));
                c *= 1f - seam * 0.75f;
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float desgaste = 1f - Suave(0f, 0.05f, e);
                c = Color.Lerp(c, new Color(0.13f, 0.095f, 0.06f), desgaste * 0.7f);
                float dust = Suave(0.62f, 0.86f, Fbm(u * 5f + 3f, v * 5f + 8f, 3, 21));
                c = Color.Lerp(c, new Color(0.40f, 0.37f, 0.31f), dust * 0.40f);
                c.a = 1f;
                col[y * N + x] = c;
                h[y * N + x] = g * 0.45f - seam * 0.9f - vein * 0.6f + fine * 0.12f - desgaste * 0.35f;
            }
        var rnd = new System.Random(77);
        for (int i = 0; i < 16; i++)       // rayones (finos y poco brillantes: en la cara fina se estiran)
        {
            var a = new Vector2((float)rnd.NextDouble() * N, (float)rnd.NextDouble() * N);
            float ang = ((float)rnd.NextDouble() - 0.5f) * 0.7f + Mathf.PI * 0.5f * ((float)rnd.NextDouble() < 0.3f ? 0f : 1f);
            var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (20f + (float)rnd.NextDouble() * 90f);
            Raya(col, h, a, b, 0.6f, 0.025f, 0.35f);
        }
        for (int i = 0; i < 3; i++)        // nudos
        {
            float cx = (float)rnd.NextDouble() * N, cy = (float)rnd.NextDouble() * N;
            for (int y = (int)cy - 18; y <= (int)cy + 18; y++)
                for (int x = (int)cx - 18; x <= (int)cx + 18; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d > 16f) continue;
                    int xx = (x + N) % N, yy = (y + N) % N;
                    float ring = 0.5f + 0.5f * Mathf.Sin(d * 1.1f);
                    col[yy * N + xx] = col[yy * N + xx] * (0.45f + 0.25f * ring) + new Color(0f, 0f, 0f, 1f);
                    h[yy * N + xx] -= (1f - d / 16f) * 0.5f;
                }
        }
        normal = Normal(h, 2.2f);
        return Hacer(col);
    }

    // Metal oxidado: acero oscuro con costras de oxido, chorreados, picaduras.
    static Texture2D Oxido(out Texture2D normal)
    {
        var col = new Color[N * N];
        var h = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float acero = 0.75f + 0.45f * Fbm(u * 8f, v * 8f, 4, 31);
                var steel = new Color(0.17f, 0.165f, 0.15f) * acero;
                float m = Suave(0.42f, 0.68f, Fbm(u * 5f, v * 5f, 5, 33));
                float tono = Fbm(u * 12f + 5f, v * 12f, 3, 35);
                var rust = Color.Lerp(new Color(0.33f, 0.16f, 0.07f), new Color(0.58f, 0.28f, 0.10f), tono);
                var c = Color.Lerp(steel, rust, m);
                float chorro = Mathf.PerlinNoise(u * 45f, v * 2.5f);
                c *= 1f - Suave(0.55f, 0.85f, chorro) * 0.45f;
                float pit = Mathf.PerlinNoise(u * 120f + 7f, v * 120f);
                float hoyo = Suave(0.72f, 0.9f, pit);
                c *= 1f - hoyo * 0.55f;
                c.a = 1f;
                col[y * N + x] = c;
                h[y * N + x] = m * 0.7f + Fbm(u * 60f, v * 60f, 3, 37) * 0.35f - hoyo * 0.8f;
            }
        normal = Normal(h, 2.6f);
        return Hacer(col);
    }

    // Tela sucia: trama, manchas pardas, pliegues y suciedad en los bordes.
    static Texture2D Tela(out Texture2D normal)
    {
        var col = new Color[N * N];
        var h = new float[N * N];
        var rnd = new System.Random(91);
        var pliegues = new Vector2[4];
        for (int i = 0; i < pliegues.Length; i++) pliegues[i] = new Vector2((float)rnd.NextDouble(), (float)rnd.NextDouble());
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float weave = 0.5f + 0.5f * Mathf.Sin(x * 1.1f) * Mathf.Sin(y * 1.1f);
                var c = new Color(0.60f, 0.53f, 0.40f) * (0.80f + 0.20f * weave) * (0.8f + 0.35f * Fbm(u * 6f, v * 6f, 4, 41));
                float st = Suave(0.5f, 0.78f, Fbm(u * 4f + 2f, v * 4f + 7f, 4, 43));
                c = Color.Lerp(c, new Color(0.26f, 0.17f, 0.09f), st * 0.75f);
                float fold = 0f;
                for (int i = 0; i < pliegues.Length; i++)
                {
                    float d = Mathf.Abs((u - pliegues[i].x) * Mathf.Cos(i * 0.9f) + (v - pliegues[i].y) * Mathf.Sin(i * 0.9f));
                    fold += Suave(0.025f, 0f, d);
                }
                c *= 1f - Mathf.Clamp01(fold) * 0.35f;
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c = Color.Lerp(c, new Color(0.12f, 0.09f, 0.06f), (1f - Suave(0f, 0.07f, e)) * 0.55f);
                c.a = 1f;
                col[y * N + x] = c;
                h[y * N + x] = weave * 0.5f - Mathf.Clamp01(fold) * 0.9f + st * 0.1f;
            }
        normal = Normal(h, 1.6f);
        return Hacer(col);
    }

    // Papel amarillento con renglones, garabatos, un cerco de cafe y dobleces.
    static Texture2D Papel()
    {
        var col = new Color[N * N];
        var h = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                var c = new Color(0.76f, 0.70f, 0.52f) * (0.82f + 0.25f * Fbm(u * 7f, v * 7f, 4, 51));
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                c = Color.Lerp(c, new Color(0.36f, 0.27f, 0.14f), (1f - Suave(0f, 0.10f, e)) * 0.65f);
                float st = Suave(0.55f, 0.8f, Fbm(u * 3f, v * 3f + 5f, 4, 53));
                c = Color.Lerp(c, new Color(0.40f, 0.28f, 0.14f), st * 0.55f);
                // renglones azulados tenues
                if ((y % 26) == 0 && u > 0.08f && u < 0.92f) c = Color.Lerp(c, new Color(0.35f, 0.45f, 0.55f), 0.18f);
                // cerco de cafe
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.68f, 0.32f));
                float ring = Suave(0.012f, 0f, Mathf.Abs(d - 0.15f)) * Suave(0.35f, 0.7f, Fbm(u * 20f, v * 20f, 2, 55));
                c = Color.Lerp(c, new Color(0.30f, 0.18f, 0.08f), ring * 0.8f);
                // doblez en cruz
                float dob = Suave(0.004f, 0f, Mathf.Abs(u - 0.5f)) + Suave(0.004f, 0f, Mathf.Abs(v - 0.5f));
                c *= 1f - Mathf.Clamp01(dob) * 0.18f;
                c.a = 1f;
                col[y * N + x] = c;
                h[y * N + x] = 0f;
            }
        // garabatos: trazos ondulados oscuros por renglon
        var rnd = new System.Random(63);
        for (int fila = 1; fila < 18; fila++)
        {
            float yy = fila * 26f - 4f;
            float x = N * (0.10f + 0.04f * (float)rnd.NextDouble());
            float fin = N * (0.55f + 0.38f * (float)rnd.NextDouble());
            while (x < fin)
            {
                float palabra = 14f + (float)rnd.NextDouble() * 34f;
                for (float t = 0; t < palabra; t += 1f)
                {
                    float py = yy + Mathf.Sin(t * 0.9f + fila) * 3.2f + Mathf.Sin(t * 2.3f) * 1.4f;
                    int px = Mathf.Clamp((int)(x + t), 0, N - 1), pyi = Mathf.Clamp((int)py, 0, N - 1);
                    col[pyi * N + px] = new Color(0.10f, 0.08f, 0.07f, 1f);
                    col[Mathf.Clamp(pyi + 1, 0, N - 1) * N + px] = new Color(0.14f, 0.11f, 0.09f, 1f);
                }
                x += palabra + 8f + (float)rnd.NextDouble() * 10f;
            }
        }
        return Hacer(col);
    }

    // Decal sobre el tablero: polvo gris y manchas oscuras (alfa).
    static Texture2D PolvoManchas()
    {
        var col = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N, v = y / (float)N;
                float polvo = Suave(0.45f, 0.85f, Fbm(u * 6f, v * 6f, 4, 61)) * 0.20f;
                float e = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                polvo += (1f - Suave(0f, 0.16f, e)) * 0.10f;            // mas polvo hacia las esquinas
                float mancha = Suave(0.60f, 0.80f, Fbm(u * 3.5f + 4f, v * 3.5f + 1f, 4, 63));
                var c = Color.Lerp(new Color(0.50f, 0.47f, 0.40f), new Color(0.05f, 0.035f, 0.02f), mancha);
                float a = Mathf.Max(polvo, mancha * 0.55f);
                col[y * N + x] = new Color(c.r, c.g, c.b, Mathf.Clamp01(a));
            }
        return Hacer(col);
    }

    // Decal bajo la mesa: mancha oscura de suciedad con borde irregular.
    static Texture2D ManchaSuelo()
    {
        var col = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = x / (float)N * 2f - 1f, v = y / (float)N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v) + (Fbm(u * 3f + 5f, v * 3f + 2f, 4, 71) - 0.5f) * 0.7f;
                float a = (1f - Suave(0.25f, 0.95f, r)) * 0.60f;
                float vet = 0.8f + 0.4f * Fbm(u * 8f, v * 8f, 3, 73);
                col[y * N + x] = new Color(0.035f * vet, 0.027f * vet, 0.018f * vet, a);
            }
        return Hacer(col);
    }

    static Texture2D Hacer(Color[] col)
    {
        for (int i = 0; i < col.Length; i++)
            col[i] = new Color(Mathf.Clamp01(col[i].r), Mathf.Clamp01(col[i].g), Mathf.Clamp01(col[i].b), Mathf.Clamp01(col[i].a));
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.SetPixels(col);
        return t;
    }
}
