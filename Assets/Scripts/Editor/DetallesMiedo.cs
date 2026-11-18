using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Detalles de ambientacion baratos para los Backrooms:
//   - 1 atlas de 1024 (decals de suelo/pared, huecos de techo, placa caida, escombros, carteles) y 1 material compartido (Backrooms/Decal).
//   - quads con UV por celda del atlas, 1 malla de tuberia y 1 de cable (pocos poligonos), 1 material de tuberia.
//   - 1 material de panel fluorescente muerto.
// Todo cuelga de "Detalles_Miedo", sin colliders, marcado Static (batching). Se distribuye por puntuacion:
// mas cerca de nichos, callejones sin salida y esquinas, nada cerca del inicio, de las mesas, de la salida ni dentro de los nichos.
// Es idempotente: Construir() borra y recrea "Detalles_Miedo". No toca NavMesh, hotspots, IA ni armas.
public static class DetallesMiedo
{
    const string DirTex = "Assets/Texturas/Backrooms/";
    const string DirMalla = "Assets/Modelo 3D/Detalles/";
    const string DirMat = "Assets/Materiales/Backrooms/";
    const int A = 1024, C = 256; // atlas 4x4 de celdas de 256

    // ---------- ruido ----------
    static float Hash(int x, int y, int seed)
    {
        unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041); h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16; return (h & 0xFFFFFF) / (float)0x1000000; }
    }
    static float VNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi, seed), Hash(xi + 1, yi, seed), fx), Mathf.Lerp(Hash(xi, yi + 1, seed), Hash(xi + 1, yi + 1, seed), fx), fy);
    }
    static float Fbm(float u, float v, float f, int oct, int seed)
    {
        float s = 0, a = 0.5f, n = 0;
        for (int o = 0; o < oct; o++) { s += a * VNoise(u * f, v * f, seed + o * 31); n += a; a *= 0.5f; f *= 2f; }
        return s / n;
    }
    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((x - a) / (b - a))); }

    // ---------- fuente 5x7 ----------
    static readonly Dictionary<char, string[]> Glifos = new Dictionary<char, string[]>
    {
        {'A', new[]{" ### ","#   #","#   #","#####","#   #","#   #","#   #"}},
        {'S', new[]{" ####","#    "," ### ","    #","    #","#   #"," ### "}},
        {'L', new[]{"#    ","#    ","#    ","#    ","#    ","#    ","#####"}},
        {'I', new[]{"#####","  #  ","  #  ","  #  ","  #  ","  #  ","#####"}},
        {'D', new[]{"#### ","#   #","#   #","#   #","#   #","#   #","#### "}},
        {'N', new[]{"#   #","##  #","# # #","#  ##","#   #","#   #","#   #"}},
        {'O', new[]{" ### ","#   #","#   #","#   #","#   #","#   #"," ### "}},
        {'C', new[]{" ####","#    ","#    ","#    ","#    ","#    "," ####"}},
        {'R', new[]{"#### ","#   #","#   #","#### ","# #  ","#  # ","#   #"}},
        {'E', new[]{"#####","#    ","#    ","#### ","#    ","#    ","#####"}},
    };
    // Cobertura del texto en (px,py) dentro de un rectangulo; 'esc' = pixeles por punto de la fuente.
    static float Texto(string t, float px, float py, float x0, float y0, float esc, float jit, int seed)
    {
        float cx = (px - x0) / esc; float cy = (py - y0) / esc;       // en celdas de fuente; y hacia arriba
        if (cy < 0 || cy >= 7) return 0f;
        int fila = 6 - Mathf.FloorToInt(cy);
        int pos = Mathf.FloorToInt(cx / 6f);
        if (pos < 0 || pos >= t.Length) return 0f;
        float dentro = cx - pos * 6f; if (dentro >= 5f) return 0f;
        string[] g; if (!Glifos.TryGetValue(t[pos], out g)) return 0f;
        int col = Mathf.FloorToInt(dentro);
        return g[fila][col] == '#' ? 1f : 0f;
    }

    // ---------- atlas ----------
    // Celdas (col,fila desde abajo): 0 derrame, 1 humedad, 2 arrastre, 3 salpicaduras / 4 hueco, 5 placa caida, 6 escombros, 7 grieta /
    // 8 cartel SALIDA, 9 aviso NO CORRAS, 10 garabato SILENCIO / 12 suciedad de esquina, 13 chorreon, 15 oxido
    static Color32 Px(int celda, float u, float v)
    {
        float r = 0, g = 0, b = 0, a = 0;
        float dx = u - 0.5f, dy = v - 0.5f; float d = Mathf.Sqrt(dx * dx + dy * dy);
        float n1 = Fbm(u, v, 5f, 4, 11 + celda), n2 = Fbm(u, v, 14f, 3, 77 + celda);
        switch (celda)
        {
            case 0: // derrame oscuro con borde y gotas
            {
                float f = d + (n1 - 0.5f) * 0.45f;
                a = Suave(0.40f, 0.30f, f) * 0.92f; a = Mathf.Max(a, Suave(0.46f, 0.43f, f + n2 * 0.1f) * 0.5f);
                r = 0.045f; g = 0.03f; b = 0.018f;   // mismo tono oscuro en el nucleo y en el borde (sin halo claro)
                break;
            }
            case 1: // humedad difusa
                a = Suave(0.5f, 0.05f, d + (n1 - 0.5f) * 0.4f) * 0.7f; r = 0.10f; g = 0.09f; b = 0.03f; break;
            case 2: // marcas de arrastre: bandas paralelas a lo largo de u que se adelgazan
            {
                float banda = 0f; for (int k = 0; k < 4; k++) { float cy = 0.30f + k * 0.13f + (Hash(k, 3, 5) - 0.5f) * 0.05f; float anch = 0.035f * (0.6f + Hash(k, 7, 5)); banda = Mathf.Max(banda, Suave(anch, anch * 0.3f, Mathf.Abs(v - cy - (n1 - 0.5f) * 0.05f))); }
                float punta = Suave(0.02f, 0.25f, u) * Suave(1f, 0.55f, u + (n2 - 0.5f) * 0.2f);
                a = banda * punta * 0.85f * (0.7f + 0.3f * n2); r = 0.12f; g = 0.045f; b = 0.035f; break;
            }
            case 3: // salpicaduras
            {
                float m = 0f; for (int k = 0; k < 18; k++) { float px = Hash(k, 1, 91), py = Hash(k, 2, 91), rr = 0.012f + Hash(k, 3, 91) * 0.05f; float dd = Mathf.Sqrt((u - px) * (u - px) + (v - py) * (v - py)); m = Mathf.Max(m, Suave(rr, rr * 0.5f, dd)); }
                a = m * 0.9f; r = 0.09f; g = 0.03f; b = 0.02f; break;
            }
            case 4: // hueco de techo: vacio casi negro, borde roto
            {
                float f = d * (1f + (n1 - 0.5f) * 0.9f) + (Mathf.Abs(dx) * 0.2f);
                a = Suave(0.40f, 0.34f, f) > 0.5f ? 1f : Suave(0.40f, 0.34f, f); r = 0f; g = 0f; b = 0f; // vacio negro puro: ningun filtrado puede aclararlo
                break;
            }
            case 5: // placa caida (opaca, con borde roto)
            {
                float ex = Mathf.Abs(dx), ey = Mathf.Abs(dy); float f = Mathf.Max(ex, ey) + (n1 - 0.5f) * 0.12f;
                a = Suave(0.42f, 0.39f, f);
                float baseC = 0.45f + 0.18f * n2; float hoyo = Suave(0.86f, 0.95f, VNoise(u * 40f, v * 40f, 5)); float mancha = Suave(0.55f, 0.7f, n1);
                r = baseC * 0.95f * (1f - hoyo * 0.35f) * (1f - mancha * 0.35f); g = baseC * 0.9f * (1f - hoyo * 0.35f) * (1f - mancha * 0.4f); b = baseC * 0.7f * (1f - hoyo * 0.35f) * (1f - mancha * 0.5f);
                float grieta = Suave(0.02f, 0f, Mathf.Abs(u - 0.5f + (n2 - 0.5f) * 0.25f)) * 0.8f; r *= 1f - grieta; g *= 1f - grieta; b *= 1f - grieta;
                break;
            }
            case 6: // escombros
            {
                float m = 0f; for (int k = 0; k < 26; k++) { float ang = Hash(k, 1, 33) * 6.283f, rad = 0.08f + Hash(k, 2, 33) * 0.36f; float px = 0.5f + Mathf.Cos(ang) * rad, py = 0.5f + Mathf.Sin(ang) * rad, rr = 0.012f + Hash(k, 3, 33) * 0.03f; float dd = Mathf.Max(Mathf.Abs(u - px), Mathf.Abs(v - py) * 1.3f); m = Mathf.Max(m, Suave(rr, rr * 0.4f, dd)); }
                float polvo = Suave(0.5f, 0.1f, d + (n1 - 0.5f) * 0.3f) * 0.35f;
                a = Mathf.Max(m * 0.95f, polvo); r = 0.42f - polvo * 0.1f; g = 0.38f - polvo * 0.1f; b = 0.28f - polvo * 0.08f; break;
            }
            case 7: // grieta vertical con ramas
            {
                float cx = 0.5f + (Fbm(0f, v, 9f, 3, 5) - 0.5f) * 0.28f; float w = 0.012f + 0.006f * Mathf.Sin(v * 40f);
                float c1 = Suave(w, 0f, Mathf.Abs(u - cx)) * Suave(0f, 0.08f, v) * Suave(1f, 0.9f, v);
                float cx2 = cx + (v - 0.5f) * 0.6f + 0.04f; float c2 = Suave(w * 0.6f, 0f, Mathf.Abs(u - cx2)) * Suave(0.35f, 0.4f, v) * Suave(0.8f, 0.7f, v);
                a = Mathf.Max(c1, c2) * 0.95f; r = 0.03f; g = 0.025f; b = 0.012f; break;
            }
            case 8: // cartel SALIDA gastado
            {
                float ex = Mathf.Abs(dx), ey = Mathf.Abs(dy); float dentro = Mathf.Max(ex / 0.44f, ey / 0.19f);
                a = Suave(1.02f, 0.97f, dentro + (n2 - 0.5f) * 0.05f);
                float desgaste = 0.35f + 0.45f * n1; float marco = Suave(0.88f, 0.93f, dentro);
                r = 0.05f * desgaste; g = 0.20f * desgaste; b = 0.08f * desgaste;
                float letra = Texto("SALIDA", u * C, v * C, 0.105f * C, 0.42f * C, 0.0225f * C, 0f, 3) * Suave(0.25f, 0.5f, n2 + 0.2f);
                r = Mathf.Lerp(r, 0.55f * desgaste, letra); g = Mathf.Lerp(g, 0.62f * desgaste, letra); b = Mathf.Lerp(b, 0.5f * desgaste, letra);
                r = Mathf.Lerp(r, 0.06f, marco); g = Mathf.Lerp(g, 0.07f, marco); b = Mathf.Lerp(b, 0.04f, marco);
                float suc = Suave(0.55f, 0.75f, Fbm(u, v, 7f, 4, 59)); r *= 1f - suc * 0.6f; g *= 1f - suc * 0.6f; b *= 1f - suc * 0.6f;
                break;
            }
            case 9: // aviso en papel amarillento, texto negro y tachon rojo
            {
                float ex = Mathf.Abs(dx), ey = Mathf.Abs(dy); float dentro = Mathf.Max(ex / 0.34f, ey / 0.27f);
                a = Suave(1.02f, 0.98f, dentro + (n2 - 0.5f) * 0.06f);
                float papel = 0.50f + 0.18f * n1; r = papel * 0.9f; g = papel * 0.82f; b = papel * 0.45f;
                float letra = Mathf.Max(Texto("NO", u * C, v * C, 0.30f * C, 0.50f * C, 0.032f * C, 0f, 3), Texto("CORRAS", u * C, v * C, 0.20f * C, 0.28f * C, 0.0175f * C, 0f, 3));
                r = Mathf.Lerp(r, 0.03f, letra); g = Mathf.Lerp(g, 0.025f, letra); b = Mathf.Lerp(b, 0.02f, letra);
                float tach = Suave(0.014f, 0.004f, Mathf.Min(Mathf.Abs((u - 0.5f) - (v - 0.5f) * 1.1f), Mathf.Abs((u - 0.5f) + (v - 0.5f) * 1.1f))) * Suave(0.9f, 0.7f, dentro) * Suave(0.4f, 0.7f, n2);
                r = Mathf.Lerp(r, 0.35f, tach); g = Mathf.Lerp(g, 0.03f, tach); b = Mathf.Lerp(b, 0.02f, tach);
                float suc = Suave(0.6f, 0.8f, Fbm(u, v, 6f, 4, 61)); r *= 1f - suc * 0.55f; g *= 1f - suc * 0.55f; b *= 1f - suc * 0.55f;
                break;
            }
            case 10: // garabato "SILENCIO" en rojo oscuro, solo trazos
            {
                // letras: S I L E N C I O (sin G) con temblor
                float jx = (Fbm(u, v, 18f, 2, 7) - 0.5f) * 0.012f, jy = (Fbm(u, v, 18f, 2, 9) - 0.5f) * 0.012f;
                float t = Mathf.Max(Texto("SILENCIO", (u + jx) * C, (v + jy) * C, 0.04f * C, 0.40f * C, 0.0155f * C, 0f, 3), 0f);
                float chorro = 0f; for (int k = 0; k < 8; k++) { float cxk = 0.04f + (k * 6f + 2.5f) * 0.0155f; if (Hash(k, 4, 13) > 0.45f) chorro = Mathf.Max(chorro, Suave(0.010f, 0.004f, Mathf.Abs(u - cxk)) * Suave(0.40f, 0.40f - 0.1f - Hash(k, 5, 13) * 0.25f, v) * (v < 0.40f ? 1f : 0f)); }
                a = Mathf.Clamp01(t * 0.9f + chorro * 0.8f) * Suave(0.1f, 0.35f, n2 + 0.3f); r = 0.30f; g = 0.03f; b = 0.02f; break;
            }
            case 12: // suciedad de esquina: gradiente desde (0,0)
            {
                float f = Mathf.Sqrt(u * u + v * v) + (n1 - 0.5f) * 0.35f;
                a = Mathf.Pow(Mathf.Clamp01(1f - f / 0.95f), 1.4f) * 0.85f; r = 0.05f; g = 0.04f; b = 0.02f; break;
            }
            case 13: // chorreon vertical que baja desde arriba
            {
                float w = 0.05f + 0.04f * Fbm(0f, v, 6f, 3, 3); float cx = 0.5f + (Fbm(0f, v, 4f, 2, 17) - 0.5f) * 0.12f;
                float largo = Suave(0.05f, 0.25f, v); float bulbo = Suave(0.08f, 0.02f, Mathf.Sqrt((u - cx) * (u - cx) + (v - 0.1f) * (v - 0.1f) * 0.4f));
                a = Mathf.Max(Suave(w, w * 0.4f, Mathf.Abs(u - cx)) * largo * 0.8f, bulbo * 0.9f); r = 0.09f; g = 0.07f; b = 0.02f; break;
            }
            case 15: // oxido
                a = Suave(0.5f, 0.1f, d + (n1 - 0.5f) * 0.5f) * 0.55f * Suave(0.3f, 0.6f, n2 + 0.2f); r = 0.30f; g = 0.14f; b = 0.05f; break;
            default: a = 0f; break;
        }
        // RGB en zonas casi transparentes = gris oscuro neutro (evita halos claros al filtrar entre celdas y mipmaps)
        if (a < 0.03f) { r = 0.03f; g = 0.027f; b = 0.02f; }
        // margen de 10 px en el borde de cada celda: transparente
        bool bordeCelda = u < 0.04f || u > 0.96f || v < 0.04f || v > 0.96f;
        if (celda == 12) bordeCelda = u > 0.96f || v > 0.96f;   // la suciedad de esquina nace pegada al borde (0,0)
        if (bordeCelda) { a = 0f; r = 0.03f; g = 0.027f; b = 0.02f; }
        return new Color32((byte)(Mathf.Clamp01(r) * 255f), (byte)(Mathf.Clamp01(g) * 255f), (byte)(Mathf.Clamp01(b) * 255f), (byte)(Mathf.Clamp01(a) * 255f));
    }

    static void GenerarAtlas()
    {
        var px = new Color32[A * A];
        for (int celda = 0; celda < 16; celda++)
        {
            int cx = (celda % 4) * C, cy = (celda / 4) * C;
            for (int y = 0; y < C; y++) for (int x = 0; x < C; x++) px[(cy + y) * A + cx + x] = Px(celda, (x + 0.5f) / C, (y + 0.5f) / C);
        }
        var t = new Texture2D(A, A, TextureFormat.RGBA32, false, false);
        t.SetPixels32(px); t.Apply(false);
        File.WriteAllBytes(DirTex + "detalles_atlas.png", t.EncodeToPNG());
        Object.DestroyImmediate(t);
        string ruta = DirTex + "detalles_atlas.png";
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(ruta);
        ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.alphaIsTransparency = true;
        // Sin mipmaps: son decals pequenos y los mips mezclaban celdas vecinas del atlas (halo claro visto de lejos).
        ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear; ti.anisoLevel = 0; ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Compressed; ti.compressionQuality = 100;
        var and = ti.GetPlatformTextureSettings("Android"); and.overridden = true; and.maxTextureSize = 1024; and.format = TextureImporterFormat.ASTC_4x4; and.compressionQuality = 100; ti.SetPlatformTextureSettings(and);
        ti.SaveAndReimport();
    }

    // ---------- mallas ----------
    static void Carpeta() { if (!AssetDatabase.IsValidFolder("Assets/Modelo 3D/Detalles")) AssetDatabase.CreateFolder("Assets/Modelo 3D", "Detalles"); }

    static Mesh GuardarMalla(Mesh m, string nombre)
    {
        string ruta = DirMalla + nombre + ".asset";
        AssetDatabase.DeleteAsset(ruta); AssetDatabase.CreateAsset(m, ruta); return m;
    }

    // Quad 1x1 en el plano XY centrado, con UV de la celda 'celda' del atlas (con medio texel de margen).
    static Mesh Quad(int celda)
    {
        float m = 6f / A; float u0 = (celda % 4) * 0.25f + m, v0 = (celda / 4) * 0.25f + m, u1 = u0 + 0.25f - 2 * m, v1 = v0 + 0.25f - 2 * m;
        var q = new Mesh { name = "Quad_" + celda };
        q.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
        q.uv = new[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u0, v1), new Vector2(u1, v1) };
        q.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        q.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        q.RecalculateBounds();
        return GuardarMalla(q, "Quad_" + celda);
    }

    // Tubo a lo largo de +X (de 0 a 'largo'), 'lados' caras, con una caida (sag) en Y en el medio.
    static Mesh Tubo(string nombre, float largo, float radio, int lados, int segmentos, float sag, bool uv2)
    {
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uv = new List<Vector2>(); var tr = new List<int>();
        for (int s = 0; s <= segmentos; s++)
        {
            float t = s / (float)segmentos; float x = t * largo; float y = -sag * 4f * t * (1f - t);
            for (int k = 0; k < lados; k++)
            {
                float ang = k / (float)lados * Mathf.PI * 2f; Vector3 n = new Vector3(0, Mathf.Cos(ang), Mathf.Sin(ang));
                vs.Add(new Vector3(x, y, 0) + n * radio); ns.Add(n); uv.Add(new Vector2(t, k / (float)lados));
            }
        }
        for (int s = 0; s < segmentos; s++) for (int k = 0; k < lados; k++)
        {
            int a = s * lados + k, b = s * lados + (k + 1) % lados, c = (s + 1) * lados + k, d = (s + 1) * lados + (k + 1) % lados;
            tr.Add(a); tr.Add(b); tr.Add(c); tr.Add(b); tr.Add(d); tr.Add(c);
        }
        var m = new Mesh { name = nombre }; m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uv); m.SetTriangles(tr, 0); m.RecalculateBounds();
        if (uv2) Unwrapping.GenerateSecondaryUVSet(m);
        return GuardarMalla(m, nombre);
    }

    static Material Mat(string ruta, Shader sh, System.Action<Material> cfg)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, ruta); }
        m.shader = sh; cfg(m); EditorUtility.SetDirty(m); return m;
    }

    // ---------- escena ----------
    class Celda { public int i, j; public Vector3 c; public bool N, E, S, O; public bool nicho; public int paredes { get { return (N ? 1 : 0) + (E ? 1 : 0) + (S ? 1 : 0) + (O ? 1 : 0); } } }

    static Transform Hijo(Transform padre, string nombre, Mesh malla, Material mat, Vector3 pos, Quaternion rot, Vector3 esc)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = pos; go.transform.rotation = rot; go.transform.localScale = esc;
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
        if (mat.name == "Mat_Tuberia") { flags |= StaticEditorFlags.ContributeGI; r.scaleInLightmap = 0.5f; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; } // tuberias y cables entran al horneado (sombras suaves en el techo)
        GameObjectUtility.SetStaticEditorFlags(go, flags);
        return go.transform;
    }

    // Regenera solo el atlas (no recrea objetos: asi no se pierde el horneado de las tuberias).
    public static string SoloAtlas() { GenerarAtlas(); return "atlas regenerado"; }

    public static string Construir()
    {
        var log = new System.Text.StringBuilder();
        Carpeta();
        GenerarAtlas();

        // materiales
        var shDecal = Shader.Find("Backrooms/Decal");
        if (shDecal == null) return "No se encuentra el shader Backrooms/Decal";
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTex + "detalles_atlas.png");
        var mDecal = Mat(DirMat + "Mat_Decal.mat", shDecal, m => { m.SetTexture("_MainTex", atlas); m.color = Color.white; m.renderQueue = 2990; });
        var mTubo = Mat(DirMat + "Mat_Tuberia.mat", Shader.Find("Standard"), m => { m.color = new Color(0.34f, 0.30f, 0.24f); m.SetFloat("_Glossiness", 0.15f); m.SetFloat("_Metallic", 0.3f); });
        var panelMat = AssetDatabase.LoadAssetAtPath<Material>(DirMat + "Mat_PanelFluorescente.mat");
        var mMuerto = Mat(DirMat + "Mat_PanelMuerto.mat", Shader.Find("Standard"), m => { m.color = new Color(0.30f, 0.29f, 0.25f); m.SetFloat("_Glossiness", 0.1f); m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack; });

        // mallas
        var quads = new Mesh[16]; foreach (var k in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 15 }) quads[k] = Quad(k);
        var tubo = Tubo("Tubo_Techo", 3f, 0.05f, 6, 1, 0f, true);
        var cable = Tubo("Cable_Colgante", 3f, 0.016f, 4, 8, 0.32f, true);
        AssetDatabase.SaveAssets();

        // mapa de celdas desde la escena
        var modulos = GameObject.Find("Laberinto").transform.Find("Modulos");
        var celdas = new Dictionary<long, Celda>(); var lista = new List<Celda>();
        foreach (Transform m in modulos)
        {
            var p = m.name.Split('_'); if (p.Length < 3) continue;
            var ce = new Celda { i = int.Parse(p[1]), j = int.Parse(p[2]), c = m.position, nicho = !m.name.StartsWith("Celda") };
            System.Func<string, bool> pared = n => { var t = m.Find(n); return t != null && t.gameObject.activeSelf && t.childCount > 0 && t.GetChild(0).gameObject.activeSelf; };
            ce.N = pared("Pared_N"); ce.E = pared("Pared_E"); ce.S = pared("Pared_S"); ce.O = pared("Pared_O");
            celdas[ce.i * 100L + ce.j] = ce; lista.Add(ce);
        }
        // zonas prohibidas (xz, radio): inicio, mesas, salida
        var prohib = new List<Vector3>(); // x,z,radio en (x,y,z)=(x,radio,z)
        prohib.Add(new Vector3(0, 6f, 0));
        var armas = GameObject.Find("Armas"); if (armas != null) foreach (Transform t in armas.transform) if (t.name.StartsWith("Mesa")) prohib.Add(new Vector3(t.position.x, 2.4f, t.position.z));
        var sal = GameObject.Find("Laberinto").transform.Find("Salida"); Vector3 posSalida = sal != null ? sal.position : new Vector3(43f, 0, -42f);
        if (sal != null) { var pd = sal.Find("Puerta_Luz"); if (pd != null) posSalida = pd.position; }
        prohib.Add(new Vector3(posSalida.x, 3f, posSalida.z));
        var nichos = new List<Celda>(); foreach (var ce in lista) if (ce.nicho) nichos.Add(ce);
        System.Func<Vector3, float> libre = p => { float r = 1f; foreach (var z in prohib) { float d = Mathf.Sqrt((p.x - z.x) * (p.x - z.x) + (p.z - z.z) * (p.z - z.z)); if (d < z.y) return 0f; if (d < z.y * 2.2f) r = Mathf.Min(r, (d - z.y) / (z.y * 1.2f)); } return r; };
        System.Func<Celda, float> peso = ce =>
        {
            if (ce.nicho) return 0f;
            float w = 1f; int np = ce.paredes;
            if (np >= 3) w += 5f; else if (np == 2 && ((ce.N || ce.S) && (ce.E || ce.O))) w += 2f;
            foreach (var n in nichos) { float d = Vector3.Distance(ce.c, n.c); if (d < 7f) w += 5f * (1f - d / 7f) + 2f; }
            float dI = Mathf.Sqrt(ce.c.x * ce.c.x + ce.c.z * ce.c.z); w *= Mathf.Clamp01((dI - 6f) / 14f);   // menos cerca del inicio
            return w * libre(ce.c);
        };
        var rnd = new System.Random(20261104);
        var usadas = new List<Vector3>(); var porTipo = new Dictionary<string, List<Vector3>>();
        System.Func<string, float, Celda> elegir = (tipo, sep) =>
        {
            List<Vector3> l; if (!porTipo.TryGetValue(tipo, out l)) { l = new List<Vector3>(); porTipo[tipo] = l; }
            float tot = 0; var cand = new List<KeyValuePair<Celda, float>>();
            foreach (var ce in lista) { float w = peso(ce); if (w <= 0f) continue; bool cerca = false; foreach (var q in l) if (Vector3.Distance(q, ce.c) < sep) { cerca = true; break; } if (cerca) continue; cand.Add(new KeyValuePair<Celda, float>(ce, w)); tot += w; }
            if (cand.Count == 0) return null; float x = (float)rnd.NextDouble() * tot;
            foreach (var kv in cand) { x -= kv.Value; if (x <= 0f) { l.Add(kv.Key.c); return kv.Key; } }
            l.Add(cand[cand.Count - 1].Key.c); return cand[cand.Count - 1].Key;
        };
        Func2 R = (a, b) => a + (float)rnd.NextDouble() * (b - a);

        var viejo = GameObject.Find("Detalles_Miedo"); if (viejo != null) Object.DestroyImmediate(viejo);
        var raiz = new GameObject("Detalles_Miedo").transform;
        var gSuelo = new GameObject("Suelo").transform; gSuelo.SetParent(raiz, false);
        var gPared = new GameObject("Paredes").transform; gPared.SetParent(raiz, false);
        var gTecho = new GameObject("Techo").transform; gTecho.SetParent(raiz, false);
        var gCart = new GameObject("Carteles").transform; gCart.SetParent(raiz, false);
        int nSuelo = 0, nPared = 0, nTecho = 0, nCart = 0, nTubo = 0, nCable = 0;
        const float Y0 = 0.004f; // sobre el suelo (el shader ya empuja hacia la camara)
        Quaternion plano(float yaw) { return Quaternion.Euler(0, yaw, 0) * Quaternion.Euler(90, 0, 0); }
        // desde el centro de la celda, lejos del centro (hotspot de teletransporte): fuera de 0.9 m
        Vector3 fueraCentro(Celda ce, float rx, float rz) { float x = Mathf.Clamp(rx, -1.1f, 1.1f), z = Mathf.Clamp(rz, -1.1f, 1.1f); if (Mathf.Sqrt(x * x + z * z) < 0.95f) { float k = 1.0f / Mathf.Max(0.01f, Mathf.Sqrt(x * x + z * z)); x *= k; z *= k; x = Mathf.Clamp(x, -1.15f, 1.15f); z = Mathf.Clamp(z, -1.15f, 1.15f); } return ce.c + new Vector3(x, 0, z); }

        // --- SUELO: derrames, humedad, arrastre, salpicaduras ---
        var planSuelo = new (string tipo, int celda, int n, float tam)[] { ("derrame", 0, 4, 1.3f), ("humedad", 1, 3, 1.5f), ("arrastre", 2, 3, 2.2f), ("salpic", 3, 2, 1.1f) };
        foreach (var pl in planSuelo)
            for (int n = 0; n < pl.n; n++)
            {
                var ce = elegir("suelo_" + pl.tipo, 7f); if (ce == null) continue;
                Vector3 pos = fueraCentro(ce, R(-1.15f, 1.15f), R(-1.15f, 1.15f)); pos.y = Y0;
                float yaw = pl.celda == 2 ? (ce.N || ce.S ? 0f : 90f) + (rnd.Next(2) == 0 ? 0f : 180f) : R(0f, 360f);
                // el arrastre apunta hacia el nicho mas cercano si hay uno cerca
                if (pl.celda == 2 && nichos.Count > 0) { Celda mn = null; float md = 1e9f; foreach (var nn in nichos) { float d = Vector3.Distance(nn.c, ce.c); if (d < md) { md = d; mn = nn; } } if (mn != null && md < 12f) { Vector3 dir = mn.c - ce.c; yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90f; } }
                Hijo(gSuelo, pl.tipo + "_" + ce.i + "_" + ce.j, quads[pl.celda], mDecal, pos, plano(yaw), new Vector3(pl.tam * R(0.85f, 1.2f), pl.tam * R(0.85f, 1.2f), 1f)); nSuelo++;
            }
        // suciedad de esquina (celda 12): en esquinas de dos paredes perpendiculares
        for (int n = 0; n < 7; n++)
        {
            var ce = elegir("esquina", 5f); if (ce == null) break;
            // esquina cuya pareja de paredes exista
            var opts = new List<Vector2>(); if (ce.N && ce.E) opts.Add(new Vector2(1, 1)); if (ce.N && ce.O) opts.Add(new Vector2(-1, 1)); if (ce.S && ce.E) opts.Add(new Vector2(1, -1)); if (ce.S && ce.O) opts.Add(new Vector2(-1, -1));
            if (opts.Count == 0) { n--; if (rnd.Next(30) == 0) break; continue; }
            var o = opts[rnd.Next(opts.Count)]; float tam = R(1.0f, 1.4f);
            Vector3 esq = ce.c + new Vector3(o.x * 1.4f, Y0, o.y * 1.4f);
            // el gradiente nace en el (-.5,-.5) local: mirar hacia la esquina con escala negativa
            Hijo(gSuelo, "esquina_" + ce.i + "_" + ce.j, quads[12], mDecal, esq + new Vector3(-o.x * tam * 0.5f, 0, -o.y * tam * 0.5f), plano(0f), new Vector3(-o.x * tam, o.y * tam, 1f)); nSuelo++;
        }

        // --- PAREDES: humedad, oxido, grietas, chorreones ---
        var planPared = new (string tipo, int celda, int n, float w, float h, float y)[] { ("humedad", 1, 3, 1.6f, 1.5f, 1.0f), ("oxido", 15, 2, 1.0f, 1.2f, 1.2f), ("grieta", 7, 3, 0.7f, 1.9f, 1.3f), ("chorreon", 13, 3, 0.45f, 1.6f, 1.8f) };
        foreach (var pl in planPared)
            for (int n = 0; n < pl.n; n++)
            {
                var ce = elegir("pared_" + pl.tipo, 6f); if (ce == null) continue;
                var lados = new List<char>(); if (ce.N) lados.Add('N'); if (ce.E) lados.Add('E'); if (ce.S) lados.Add('S'); if (ce.O) lados.Add('O'); if (lados.Count == 0) continue;
                char l = lados[rnd.Next(lados.Count)]; float lat = R(-0.9f, 0.9f); const float cara = 1.395f;
                Vector3 pos; float yaw;
                switch (l) { case 'N': pos = ce.c + new Vector3(lat, 0, cara); yaw = 0f; break; case 'S': pos = ce.c + new Vector3(lat, 0, -cara); yaw = 0f; break; case 'E': pos = ce.c + new Vector3(cara, 0, lat); yaw = 90f; break; default: pos = ce.c + new Vector3(-cara, 0, lat); yaw = 90f; break; }
                pos.y = pl.y;
                Hijo(gPared, pl.tipo + "_" + ce.i + "_" + ce.j + l, quads[pl.celda], mDecal, pos, Quaternion.Euler(0, yaw, 0), new Vector3(pl.w * (rnd.Next(2) == 0 ? 1 : -1), pl.h, 1f)); nPared++;
            }

        // --- CARTELES ---
        // SALIDA gastada: pasillo recto (paredes opuestas), lejos del inicio
        Celda cs = null; { var tmp = new List<Celda>(); foreach (var ce in lista) if (!ce.nicho && ((ce.N && ce.S && !ce.E && !ce.O) || (ce.E && ce.O && !ce.N && !ce.S)) && Mathf.Sqrt(ce.c.x * ce.c.x + ce.c.z * ce.c.z) > 16f && libre(ce.c) > 0.99f) tmp.Add(ce); if (tmp.Count > 0) cs = tmp[rnd.Next(tmp.Count)]; }
        if (cs != null) { bool lado = cs.N; Vector3 pos = cs.c + (cs.N ? new Vector3(0, 0, 1.395f) : new Vector3(1.395f, 0, 0)); pos.y = 1.95f; Hijo(gCart, "Cartel_Salida_" + cs.i + "_" + cs.j, quads[8], mDecal, pos, Quaternion.Euler(0, cs.N ? 0f : 90f, 0), new Vector3(0.95f, 0.95f, 1f)); nCart++; log.AppendLine("cartel SALIDA en celda " + cs.i + "," + cs.j); }
        // aviso NO CORRAS: callejon sin salida (3 paredes), cara de la pared del fondo
        { Celda best = null; float bw = 0; foreach (var ce in lista) { if (ce.nicho || ce.paredes < 3) continue; float w = peso(ce); if (w > bw) { bw = w; best = ce; } }
          if (best != null) { Vector3 pos; float yaw; if (!best.N) { pos = best.c + new Vector3(0, 0, -1.395f); yaw = 0f; } else if (!best.S) { pos = best.c + new Vector3(0, 0, 1.395f); yaw = 0f; } else if (!best.E) { pos = best.c + new Vector3(-1.395f, 0, 0); yaw = 90f; } else { pos = best.c + new Vector3(1.395f, 0, 0); yaw = 90f; }
              // pared del fondo = la opuesta a la abertura
              if (!best.N) { pos = best.c + new Vector3(0, 0, -1.395f); } else if (!best.S) { pos = best.c + new Vector3(0, 0, 1.395f); } else if (!best.E) { pos = best.c + new Vector3(-1.395f, 0, 0); } else if (!best.O) { pos = best.c + new Vector3(1.395f, 0, 0); }
              pos.y = 1.55f; Hijo(gCart, "Aviso_" + best.i + "_" + best.j, quads[9], mDecal, pos, Quaternion.Euler(0, yaw, 0), new Vector3(0.75f, 0.75f, 1f)); nCart++; log.AppendLine("aviso en callejon " + best.i + "," + best.j); } }
        // garabato SILENCIO: celda junto a un nicho, en una pared
        { Celda best = null; float bd = 1e9f; foreach (var ce in lista) { if (ce.nicho || ce.paredes == 0) continue; foreach (var nn in nichos) { float d = Vector3.Distance(nn.c, ce.c); if (d > 2.5f && d < bd && libre(ce.c) > 0.99f) { bd = d; best = ce; } } }
          if (best != null) { char lado = best.N ? 'N' : best.S ? 'S' : best.E ? 'E' : 'O'; float c = 1.395f; Vector3 pos = best.c + (lado == 'N' ? new Vector3(0, 0, c) : lado == 'S' ? new Vector3(0, 0, -c) : lado == 'E' ? new Vector3(c, 0, 0) : new Vector3(-c, 0, 0)); pos.y = 1.35f;
              Hijo(gCart, "Garabato_" + best.i + "_" + best.j, quads[10], mDecal, pos, Quaternion.Euler(0, (lado == 'N' || lado == 'S') ? 0f : 90f, 0), new Vector3(1.9f, 1.9f, 1f)); nCart++; log.AppendLine("garabato junto al nicho " + best.i + "," + best.j); } }

        // --- TECHO: huecos con placa caida y escombros debajo ---
        for (int n = 0; n < 4; n++)
        {
            var ce = elegir("hueco", 9f); if (ce == null) continue;
            float dx = (rnd.Next(2) == 0 ? -1 : 1) * R(0.8f, 1.1f), dz = (rnd.Next(2) == 0 ? -1 : 1) * R(0.5f, 1.0f);
            Vector3 techo = ce.c + new Vector3(dx, 2.695f, dz);
            Hijo(gTecho, "hueco_" + ce.i + "_" + ce.j, quads[4], mDecal, techo, Quaternion.Euler(90, R(0, 360), 0), new Vector3(0.85f, 0.85f, 1f)); nTecho++;
            // cable colgando del hueco (pocos): reusa el tubo, fino y vertical
            if (n % 2 == 0) { Hijo(gTecho, "colgante_" + ce.i + "_" + ce.j, tubo, mTubo, techo + new Vector3(R(-0.1f, 0.1f), -0.02f, R(-0.1f, 0.1f)), Quaternion.Euler(0, 0, -90f), new Vector3(0.3f, 0.35f, 0.35f)); nCable++; }
            // placa caida y escombros en el suelo (si cae cerca de una pared, queda ahi)
            Vector3 suelo = new Vector3(techo.x, Y0, techo.z) + new Vector3(R(-0.15f, 0.15f), 0, R(-0.15f, 0.15f));
            if (n != 1) { Hijo(gSuelo, "placa_caida_" + ce.i + "_" + ce.j, quads[5], mDecal, suelo, plano(R(0, 360)), new Vector3(0.6f, 0.6f, 1f)); nSuelo++; }
            Hijo(gSuelo, "escombros_" + ce.i + "_" + ce.j, quads[6], mDecal, suelo + new Vector3(0, 0.001f, 0), plano(R(0, 360)), new Vector3(1.3f, 1.3f, 1f)); nSuelo++;
        }

        // --- TUBERIAS y CABLES en pasillos rectos ---
        var rectos = new List<Celda>(); foreach (var ce in lista) if (!ce.nicho && ((ce.N && ce.S && !ce.E && !ce.O) || (ce.E && ce.O && !ce.N && !ce.S))) rectos.Add(ce);
        int tuberias = 0, intentos = 0;
        while (tuberias < 7 && intentos++ < 400)
        {
            var ce = elegir("tubo", 7f); if (ce == null) break;
            if (!rectos.Contains(ce)) continue;
            bool alX = ce.N && ce.S; float lado = (rnd.Next(2) == 0 ? -1f : 1f) * 1.15f;
            // 2-3 celdas seguidas si tambien son rectas con la misma orientacion
            int largo = 1; var sig = ce;
            for (int s = 1; s < 3; s++) { long key = alX ? (ce.i + s) * 100L + ce.j : ce.i * 100L + (ce.j + s); Celda o; if (celdas.TryGetValue(key, out o) && rectos.Contains(o) && ((alX && o.N && o.S) || (!alX && o.E && o.O))) largo++; else break; }
            for (int s = 0; s < largo; s++)
            {
                long key = alX ? (ce.i + s) * 100L + ce.j : ce.i * 100L + (ce.j + s); var o = celdas[key];
                Vector3 inicio = alX ? o.c + new Vector3(-1.5f, 2.50f, lado) : o.c + new Vector3(lado, 2.50f, 1.5f);
                // en +Z: la celda siguiente (j+1) esta en z mas negativo; el tubo corre de z+1.5 hacia z-1.5
                Quaternion rot = alX ? Quaternion.identity : Quaternion.Euler(0, 90f, 0);
                Hijo(gTecho, "tubo_" + o.i + "_" + o.j, tubo, mTubo, inicio, rot, Vector3.one); nTubo++;
            }
            // cable que cuelga en la primera celda del tramo, del otro lado
            if (tuberias % 2 == 0) { Vector3 inicio = alX ? ce.c + new Vector3(-1.5f, 2.64f, -lado * 0.6f) : ce.c + new Vector3(-lado * 0.6f, 2.64f, 1.5f); Hijo(gTecho, "cable_" + ce.i + "_" + ce.j, cable, mTubo, inicio, alX ? Quaternion.identity : Quaternion.Euler(0, 90f, 0), Vector3.one); nCable++; }
            tuberias++;
        }

        // --- PANELES MUERTOS (+ su luz horneada apagada) y parpadeo ---
        int muertos = 0; var luces = GameObject.Find("Laberinto").transform.Find("Luces");
        var flick = new List<string>(); foreach (var f in Object.FindObjectsOfType<FluorescentFlicker>(true)) flick.Add(f.name);
        for (int n = 0; n < 4; n++)
        {
            var ce = elegir("panel_muerto", 8f); if (ce == null) continue;
            var mod = modulos.Find("Celda_" + ce.i + "_" + ce.j); if (mod == null) { n--; if (rnd.Next(40) == 0) break; continue; }
            var luz = luces != null ? luces.Find("LuzBakeada_" + ce.i + "_" + ce.j) : null; if (luz == null) { n--; if (rnd.Next(80) == 0) break; continue; }
            var panel = mod.Find("Panel_Fluorescente"); if (panel == null) continue;
            panel.GetComponent<MeshRenderer>().sharedMaterial = mMuerto; luz.gameObject.SetActive(false); muertos++;
            log.AppendLine("panel muerto " + ce.i + "," + ce.j + " (luz apagada)");
        }

        raiz.gameObject.layer = 0;
        EditorUtility.SetDirty(mDecal); AssetDatabase.SaveAssets();
        log.Insert(0, "Detalles: suelo=" + nSuelo + " paredes=" + nPared + " techo(huecos)=" + nTecho + " carteles=" + nCart + " tubos=" + nTubo + " cables=" + nCable + " paneles muertos=" + muertos + " | luces parpadeantes ya existentes=" + string.Join(",", flick) + "\n");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(raiz.gameObject.scene);
        return log.ToString();
    }

    delegate float Func2(float a, float b);
}
