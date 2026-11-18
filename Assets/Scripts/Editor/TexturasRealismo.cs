using System.IO;
using UnityEditor;
using UnityEngine;

// Genera texturas procedurales baratas (ruido repetible, sin dependencias) para dar relieve y suciedad a los Backrooms:
//   papel tapiz gastado (color + normal), alfombra (color + normal), techo (color + normal) y rodapie (color).
// El alfa del color guarda la suavidad (el Standard la lee con "Smoothness Source = Albedo Alpha": cero muestras extra).
// Importa todo a <=1024, con mipmaps y ASTC en Android. No toca los PNG originales: crea archivos nuevos y reasigna los materiales.
public static class TexturasRealismo
{
    const string Dir = "Assets/Texturas/Backrooms/";
    const int N = 1024;

    // ---------- ruido repetible ----------
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }
    }
    static int Mod(int a, int p) { int r = a % p; return r < 0 ? r + p : r; }
    static float VNoise(float x, float y, int periodo, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int x0 = Mod(xi, periodo), x1 = Mod(xi + 1, periodo), y0 = Mod(yi, periodo), y1 = Mod(yi + 1, periodo);
        float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }
    // u,v en [0,1) (la textura repite); 'frec' celdas en la octava base; cada octava duplica la frecuencia.
    static float Fbm(float u, float v, int frec, int octavas, int seed)
    {
        float suma = 0f, amp = 0.5f, norm = 0f; int f = frec;
        for (int o = 0; o < octavas; o++) { suma += amp * VNoise(u * f, v * f, f, seed + o * 17); norm += amp; amp *= 0.5f; f *= 2; }
        return suma / norm;
    }

    // ---------- utilidades de imagen ----------
    static float[] Altura(System.Func<float, float, float> f)
    {
        var h = new float[N * N];
        for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) h[y * N + x] = f((x + 0.5f) / N, (y + 0.5f) / N);
        return h;
    }
    // Normal tangente (RGB 0..255) desde una altura con borde repetido. 'fuerza' en unidades de pendiente.
    static Color32[] NormalDeAltura(float[] h, float fuerza, Color32[] baseNormal = null)
    {
        var px = new Color32[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = h[y * N + (x + 1) % N] - h[y * N + (x + N - 1) % N];
                float dy = h[((y + 1) % N) * N + x] - h[((y + N - 1) % N) * N + x];
                Vector3 n;
                if (baseNormal != null)
                {
                    var b = baseNormal[y * N + x];
                    n = new Vector3(b.r / 127.5f - 1f, b.g / 127.5f - 1f, b.b / 127.5f - 1f);
                }
                else n = new Vector3(0f, 0f, 1f);
                n.x -= dx * fuerza; n.y -= dy * fuerza;
                if (n.z < 0.2f) n.z = 0.2f;
                n.Normalize();
                px[y * N + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        return px;
    }
    static Color32 C(float r, float g, float b, float a) { return new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f)); }
    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a))); }

    static void Guardar(string nombre, Color32[] px, int tam)
    {
        var t = new Texture2D(tam, tam, TextureFormat.RGBA32, false, true);
        t.SetPixels32(px); t.Apply(false);
        File.WriteAllBytes(Dir + nombre, t.EncodeToPNG());
        Object.DestroyImmediate(t);
    }

    static Color32[] LeerPng(string ruta)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        t.LoadImage(File.ReadAllBytes(ruta));
        if (t.width != N || t.height != N) { Object.DestroyImmediate(t); return null; }
        var px = t.GetPixels32(); Object.DestroyImmediate(t); return px;
    }

    static void Importar(string nombre, bool normal, int maxTam)
    {
        string ruta = Dir + nombre;
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = false;      // el alfa es suavidad, no transparencia
        ti.mipmapEnabled = true;
        ti.mipMapsPreserveCoverage = false;
        ti.streamingMipmaps = false;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.filterMode = FilterMode.Trilinear;
        ti.anisoLevel = 2;
        ti.maxTextureSize = maxTam;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.compressionQuality = 50;
        var def = ti.GetDefaultPlatformTextureSettings(); def.maxTextureSize = maxTam; def.format = TextureImporterFormat.Automatic; def.overridden = false;
        ti.SetPlatformTextureSettings(def);
        var and = ti.GetPlatformTextureSettings("Android");
        and.overridden = true; and.maxTextureSize = maxTam; and.format = TextureImporterFormat.ASTC_6x6; and.compressionQuality = 50;
        ti.SetPlatformTextureSettings(and);
        ti.SaveAndReimport();
    }

    // ---------- papel tapiz ----------
    // La cara de un muro usa tiling (0.5, 0.45): solo se ve u 0..0.5 y v 0..0.45. v=0 es el suelo y v~0.45 el techo.
    static void Pared()
    {
        var orig = LeerPng(Dir + "wallpaper_color.png");
        var origN = LeerPng(Dir + "wallpaper_normal.png");
        if (orig == null) { Debug.LogError("[TexturasRealismo] wallpaper_color.png no es de " + N + "x" + N); return; }
        var col = new Color32[N * N];
        var alt = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                var o = orig[y * N + x];
                Vector3 c = new Vector3(o.r, o.g, o.b) / 255f;
                float grande = Fbm(u, v, 3, 4, 11);               // manchas grandes
                float medio = Fbm(u, v, 12, 3, 23);
                float grano = VNoise(u * 256f, v * 256f, 256, 5);
                // desgaste general: oscurece y amarillea
                c *= 0.72f + 0.30f * medio;
                c = Vector3.Lerp(c, new Vector3(0.62f, 0.52f, 0.20f), 0.18f);
                // manchas de humedad marron
                float mancha = Suave(0.52f, 0.72f, grande + (medio - 0.5f) * 0.25f);
                float borde = Mathf.Clamp01(1f - Mathf.Abs(grande - 0.62f) * 14f) * 0.35f; // aro mas oscuro en el borde de la mancha
                c = Vector3.Lerp(c, new Vector3(0.36f, 0.25f, 0.08f), mancha * 0.62f);
                c *= 1f - borde * 0.5f;
                // humedad subiendo desde el rodapie (v pequeno) y mancha de gotera junto al techo (v ~ 0.45)
                float ruidoB = (Fbm(u, 0.5f, 16, 3, 41) - 0.5f) * 0.07f;
                float suelo = 1f - Suave(0.02f, 0.13f, v + ruidoB);
                c = Vector3.Lerp(c, new Vector3(0.20f, 0.19f, 0.07f), suelo * 0.7f);
                float techoM = Suave(0.33f, 0.46f, v + (Fbm(u, 0.2f, 12, 3, 53) - 0.5f) * 0.09f);
                c = Vector3.Lerp(c, new Vector3(0.28f, 0.19f, 0.06f), techoM * 0.65f);
                // chorreones verticales desde el techo
                float chorro = VNoise(u * 90f, 0.5f, 90, 67);
                float chorroM = Suave(0.70f, 0.9f, chorro) * Suave(0.12f, 0.46f, v) * (1f - Suave(0.46f, 0.52f, v) * 0.0f);
                c *= 1f - chorroM * 0.30f;
                // grano de papel
                c *= 0.94f + 0.12f * grano;
                float suav = Mathf.Lerp(0.10f, 0.05f, mancha) + suelo * 0.25f; // humedo = mas brillante
                col[y * N + x] = C(c.x, c.y, c.z, suav);
                alt[y * N + x] = Fbm(u, v, 48, 3, 79) * 0.6f + grano * 0.25f - mancha * 0.15f;
            }
        Guardar("pared_gastada_color.png", col, N);
        Guardar("pared_gastada_normal.png", NormalDeAltura(alt, 6f, origN), N);
        Importar("pared_gastada_color.png", false, 1024);
        Importar("pared_gastada_normal.png", true, 1024);
    }

    // ---------- alfombra ----------
    static void Alfombra()
    {
        var col = new Color32[N * N];
        var alt = Altura((u, v) => VNoise(u * 256f, v * 256f, 256, 3) * 0.55f + VNoise(u * 512f, v * 512f, 512, 9) * 0.30f + Fbm(u, v, 64, 2, 31) * 0.15f);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                float bajo = Fbm(u, v, 4, 4, 101);
                float fibra = alt[y * N + x];
                Vector3 b = new Vector3(0.52f, 0.47f, 0.19f);
                Vector3 c = b * Mathf.Lerp(0.55f, 1.05f, bajo) * (0.82f + 0.36f * fibra);
                // manchas oscuras (derrames) y zonas muy usadas, mas claras y gastadas
                float m1 = Suave(0.60f, 0.70f, Fbm(u, v, 5, 4, 113));
                float m2 = Suave(0.66f, 0.74f, Fbm(u, v, 9, 3, 127));
                c = Vector3.Lerp(c, new Vector3(0.30f, 0.24f, 0.10f), m1 * 0.42f);
                c = Vector3.Lerp(c, new Vector3(0.34f, 0.26f, 0.10f), m2 * 0.32f);
                float gastada = Suave(0.62f, 0.75f, Fbm(u, v, 3, 3, 139));
                c = Vector3.Lerp(c, new Vector3(0.62f, 0.56f, 0.30f), gastada * 0.25f);
                col[y * N + x] = C(c.x, c.y, c.z, 0.02f + m2 * 0.10f); // casi mate; lo manchado, algo humedo
            }
        Guardar("alfombra_gastada_color.png", col, N);
        Guardar("alfombra_gastada_normal.png", NormalDeAltura(alt, 5f), N);
        Importar("alfombra_gastada_color.png", false, 1024);
        Importar("alfombra_gastada_normal.png", true, 1024);
    }

    // ---------- techo ----------
    static void Techo()
    {
        var col = new Color32[N * N];
        var alt = new float[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N, v = (y + 0.5f) / N;
                // placas de 512 px (2x2 por textura)
                float pu = (x % 512), pv = (y % 512);
                float junta = Mathf.Min(Mathf.Min(pu, 511f - pu), Mathf.Min(pv, 511f - pv));
                float ranura = 1f - Suave(0f, 5f, junta);
                float punto = VNoise(u * 128f, v * 128f, 128, 201);
                float hoyo = Suave(0.86f, 0.95f, punto);               // perforaciones de la placa
                float gran = Fbm(u, v, 3, 4, 211);
                Vector3 c = Vector3.Lerp(new Vector3(0.74f, 0.72f, 0.60f), new Vector3(0.84f, 0.82f, 0.70f), Fbm(u, v, 8, 3, 223));
                // manchas de agua: relleno + aro marron
                float f = gran + (Fbm(u, v, 14, 3, 227) - 0.5f) * 0.2f;
                float relleno = Suave(0.56f, 0.64f, f);
                float aro = Suave(0.60f, 0.64f, f) * (1f - Suave(0.64f, 0.70f, f));
                c = Vector3.Lerp(c, new Vector3(0.58f, 0.46f, 0.24f), relleno * 0.30f);
                c = Vector3.Lerp(c, new Vector3(0.42f, 0.32f, 0.14f), aro * 0.30f);
                c *= 1f - hoyo * 0.35f; c *= 1f - ranura * 0.55f;
                c *= 0.95f + 0.1f * VNoise(u * 256f, v * 256f, 256, 233);
                col[y * N + x] = C(c.x, c.y, c.z, 0.06f);
                alt[y * N + x] = -ranura * 0.8f - hoyo * 0.35f + Fbm(u, v, 32, 2, 239) * 0.1f;
            }
        Guardar("techo_gastado_color.png", col, N);
        Guardar("techo_gastado_normal.png", NormalDeAltura(alt, 7f), N);
        Importar("techo_gastado_color.png", false, 1024);
        Importar("techo_gastado_normal.png", true, 1024);
    }

    // ---------- rodapie ----------
    // Textura casi neutra (el color del material la multiplica): base sucia abajo, polvo arriba, roces horizontales.
    static void Rodapie()
    {
        const int T = 512;
        var px = new Color32[T * T];
        for (int y = 0; y < T; y++)
            for (int x = 0; x < T; x++)
            {
                float u = (x + 0.5f) / T, v = (y + 0.5f) / T;
                float g = 0.62f + 0.30f * Fbm(u, v, 6, 4, 301);
                float suelo = 1f - Suave(0f, 0.35f, v + (Fbm(u, 0.5f, 24, 2, 313) - 0.5f) * 0.12f);
                g *= 1f - suelo * 0.45f;                                   // mugre pegada al suelo
                g *= 1f + Suave(0.82f, 1f, v) * 0.15f;                     // polvo en el canto superior
                float roce = VNoise(u * 24f, v * 160f, 160, 317);
                g *= 1f - Suave(0.88f, 0.97f, roce) * 0.35f;              // roces horizontales
                g *= 0.95f + 0.1f * VNoise(u * 128f, v * 128f, 128, 331);
                px[y * T + x] = C(g, g * 0.97f, g * 0.92f, 0.15f);
            }
        Guardar("rodapie_gastado_color.png", px, T);
        Importar("rodapie_gastado_color.png", false, 512);
    }

    static void PonerMaterial(string guidNombre, string rutaMat, string color, string normal, Vector2 tiling, Color? tinte, float? gloss)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
        if (m == null) { Debug.LogError("[TexturasRealismo] No existe " + rutaMat); return; }
        var tc = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + color);
        m.SetTexture("_MainTex", tc);
        m.mainTextureScale = tiling;
        if (normal != null)
        {
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + normal));
            m.SetTextureScale("_BumpMap", tiling);
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
        }
        // Suavidad leida del alfa del albedo (sin textura extra).
        m.SetFloat("_SmoothnessTextureChannel", 1f);
        m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
        m.SetFloat("_GlossMapScale", 1f);
        m.SetFloat("_Metallic", 0f);
        if (tinte.HasValue) m.color = tinte.Value;
        EditorUtility.SetDirty(m);
        Debug.Log("[TexturasRealismo] " + m.name + " <- " + color + (normal != null ? " + " + normal : ""));
    }

    public static string Generar()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Pared(); Alfombra(); Techo(); Rodapie();
        AssetDatabase.Refresh();
        PonerMaterial("pared", "Assets/Modelo 3D/Materiales/Mat_Backrooms_Wall.mat", "pared_gastada_color.png", "pared_gastada_normal.png", new Vector2(0.5f, 0.45f), new Color(1f, 0.93f, 0.6f), null);
        PonerMaterial("alfombra", "Assets/Materiales/Backrooms/Mat_Alfombra.mat", "alfombra_gastada_color.png", "alfombra_gastada_normal.png", new Vector2(1.5f, 1.5f), Color.white, null);
        PonerMaterial("techo", "Assets/Materiales/Backrooms/Mat_Techo.mat", "techo_gastado_color.png", "techo_gastado_normal.png", new Vector2(1.5f, 1.5f), Color.white, null);
        PonerMaterial("rodapie", "Assets/Materiales/Backrooms/Mat_Rodapie.mat", "rodapie_gastado_color.png", null, Vector2.one, new Color(0.34f, 0.20f, 0.10f), null);
        AssetDatabase.SaveAssets();
        return "Texturas generadas y materiales reasignados en " + sw.Elapsed.TotalSeconds.ToString("F1") + " s";
    }
}
