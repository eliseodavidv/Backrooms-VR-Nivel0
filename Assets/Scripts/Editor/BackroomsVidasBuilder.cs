using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Genera las texturas del indicador de vidas en Assets/Resources/Vidas (el script IndicadorVidas las carga por Resources):
//   placa_vida      = la misma placa oscura gastada de los botones chicos del menu (mismo generador y semilla),
//   corazon_vivo    = rojo sangre oscuro, manchado, con borde ocre sucio,
//   corazon_perdido = oscuro, agrietado, con una gota de sangre seca que baja,
//   mancha_fila     = mancha de sangre vieja y suave que va detras de la fila de corazones (HUD, 256x112),
//   Mat_Vidas_Hud / Mat_Vidas_Quad = materiales del HUD con OVROverlay (shaders Backrooms/UIHud y UIHudQuad).
// Reutiliza por reflexion los generadores del menu (Placa, Disco, Segmento) para no inventar otro estilo.
// Deterministas (semillas fijas) y pequenas: 128 px, ASTC en Android. Menu: Backrooms/Generar texturas de vidas.
// El corazon llena ~88 % de su celda (antes ~64 %) para que cada uno mida 5-6 grados en el visor.
public static class BackroomsVidasBuilder
{
    const string Dir = "Assets/Resources/Vidas";
    const int N = 128;
    // Geometria del corazon: centro (Cx, Cy) y escala S (px). Ocupa x 9..119, y 8..117 de la celda de 128.
    const float Cx = 64f, Cy = 56f, S = 48f;
    const float K = S / 36f;      // factor respecto a los valores originales (pensados para S = 36)

    static readonly BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Static;
    static Func<int, int, Color, int, Texture2D> placa;
    static Action<Color[], int, int, float, float, float, Color, float> disco;
    static Action<Color[], int, int, Vector2, Vector2, float, Color> segmento;

    [MenuItem("Backrooms/Generar texturas de vidas")]
    public static void Generar()
    {
        Directory.CreateDirectory(Dir);
        var tipo = typeof(BackroomsMenuBuilder);
        placa = (Func<int, int, Color, int, Texture2D>)Delegate.CreateDelegate(typeof(Func<int, int, Color, int, Texture2D>), tipo.GetMethod("Placa", Priv));
        disco = (Action<Color[], int, int, float, float, float, Color, float>)Delegate.CreateDelegate(typeof(Action<Color[], int, int, float, float, float, Color, float>), tipo.GetMethod("Disco", Priv));
        segmento = (Action<Color[], int, int, Vector2, Vector2, float, Color>)Delegate.CreateDelegate(typeof(Action<Color[], int, int, Vector2, Vector2, float, Color>), tipo.GetMethod("Segmento", Priv));

        // Misma placa que "placa_chica_oscura" del menu (128x128, mismo color y semilla 13).
        Guardar("placa_vida", placa(N, N, new Color(0.150f, 0.135f, 0.110f), 13));
        Guardar("corazon_vivo", Corazon(true, 51));
        Guardar("corazon_perdido", Corazon(false, 52));
        Guardar("mancha_fila", ManchaFila(61));
        AssetDatabase.Refresh();
        CrearMaterial("Mat_Vidas_Hud", "Backrooms/UIHud");
        CrearMaterial("Mat_Vidas_Quad", "Backrooms/UIHudQuad");
        AssetDatabase.SaveAssets();
        Debug.Log("[BackroomsVidasBuilder] Texturas de vidas generadas en " + Dir);
    }

    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    // Curva del corazon: dentro si (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0.
    static bool Dentro(float x, float y)
    {
        float u = (x - Cx) / S, v = (y - Cy) / S;
        float a = u * u + v * v - 1f;
        return a * a * a - u * u * v * v * v <= 0f;
    }

    static Texture2D Corazon(bool vivo, int seed)
    {
        var rnd = new System.Random(seed);
        var px = new Color[N * N];
        var mascara = new bool[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++) mascara[y * N + x] = Dentro(x, y);

        float ox = seed * 3.1f, oy = seed * 5.7f;
        Color baseC = vivo ? new Color(0.30f, 0.026f, 0.024f) : new Color(0.095f, 0.075f, 0.07f);
        Color ocre = vivo ? new Color(0.60f, 0.48f, 0.17f) : new Color(0.27f, 0.21f, 0.09f);
        Color contorno = new Color(0.03f, 0.02f, 0.01f);

        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int i = y * N + x;
                // Distancia (hasta 5 px) al borde, a un lado u otro.
                int r = 6;
                for (int k = 1; k <= 5 && r == 6; k++)
                    for (int dy = -k; dy <= k && r == 6; dy++)
                        for (int dx = -k; dx <= k; dx++)
                        {
                            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != k) continue;
                            int xx = x + dx, yy = y + dy;
                            // Fuera de la textura cuenta como "fuera del corazon" (si no, el borde de la imagen sale como un marco oscuro).
                            bool otro = xx >= 0 && yy >= 0 && xx < N && yy < N && mascara[yy * N + xx];
                            if (mascara[i] ? !otro : otro) { r = k; break; }
                        }

                if (!mascara[i])
                {
                    // Contorno oscuro de 2 px; fuera de eso transparente (con el mismo color para no dejar halo).
                    px[i] = new Color(contorno.r, contorno.g, contorno.b, r <= 2 ? 0.9f : 0f);
                    continue;
                }
                float n = Mathf.PerlinNoise(x * 0.07f + ox, y * 0.07f + oy) * 0.55f + Mathf.PerlinNoise(x * 0.22f + oy, y * 0.22f + ox) * 0.45f;
                float mancha = Suave(0.55f, 0.85f, Mathf.PerlinNoise(x * 0.045f + oy, y * 0.045f + ox));
                float v = Mathf.Lerp(0.55f, 1.18f, n) * (1f - mancha * 0.5f);
                Color c = new Color(baseC.r * v, baseC.g * v, baseC.b * v, 1f);
                if (r <= 4)
                {
                    float w = Mathf.Pow(1f - (r - 1) / 4f, 0.7f);
                    float suc = Mathf.Lerp(0.55f, 1f, Mathf.PerlinNoise(x * 0.3f + ox, y * 0.3f + oy));
                    c = Color.Lerp(c, new Color(ocre.r * suc, ocre.g * suc, ocre.b * suc, 1f), w);
                }
                px[i] = c;
            }

        if (vivo)
        {
            // Brillo apagado arriba a la izquierda, manchas oscuras y alguna grieta fina.
            disco(px, N, N, Cx - 0.44f * S, Cy + 0.62f * S, 9f * K, new Color(0.78f, 0.22f, 0.17f, 0.32f), 0.8f);
            for (int i = 0; i < 14; i++)
            {
                float x = Cx + ((float)rnd.NextDouble() - 0.5f) * 1.67f * S, y = Cy + 0.1f * S + ((float)rnd.NextDouble() - 0.5f) * 1.75f * S;
                if (!Dentro(x, y)) continue;
                disco(px, N, N, x, y, (3f + (float)rnd.NextDouble() * 6f) * K, new Color(0.15f, 0.01f, 0.01f, 0.5f), 0.7f);
            }
            // Costras y salpicaduras pequenas mas claras (sangre coagulada), para que no quede un rojo liso.
            for (int i = 0; i < 7; i++)
            {
                float x = Cx + ((float)rnd.NextDouble() - 0.5f) * 1.5f * S, y = Cy + 0.1f * S + ((float)rnd.NextDouble() - 0.5f) * 1.6f * S;
                if (!Dentro(x, y)) continue;
                disco(px, N, N, x, y, (1.5f + (float)rnd.NextDouble() * 2.5f) * K, new Color(0.62f, 0.10f, 0.07f, 0.35f), 0.6f);
            }
            Grietas(px, rnd, 5, 8, 0.6f);
            // Un chorreon oscuro que baja por el lado izquierdo (sangre que escurre).
            segmento(px, N, N, new Vector2(Cx - 0.45f * S, Cy - 0.1f * S), new Vector2(Cx - 0.42f * S, Cy - 0.62f * S), 1.8f * K, new Color(0.10f, 0.008f, 0.008f, 0.55f));
        }
        else
        {
            Grietas(px, rnd, 11, 10, 0.9f);
            // Gota de sangre seca que baja desde dentro del corazon y sale por debajo (apenas visible).
            Color seca = new Color(0.25f, 0.025f, 0.02f, 0.65f);
            segmento(px, N, N, new Vector2(Cx + 0.39f * S, Cy - 0.26f * S), new Vector2(Cx + 0.40f * S, 4f), 1.9f * K, seca);
            disco(px, N, N, Cx + 0.40f * S, 4f, 3.6f * K, new Color(0.23f, 0.02f, 0.02f, 0.7f), 0.25f);
        }

        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    // Grietas oscuras (caminos quebrados) que solo se pintan sobre el corazon.
    static void Grietas(Color[] px, System.Random rnd, int cantidad, int tramos, float alfa)
    {
        for (int i = 0; i < cantidad; i++)
        {
            Vector2 p = new Vector2(Cx + ((float)rnd.NextDouble() - 0.5f) * 1.67f * S, Cy + 0.05f * S + ((float)rnd.NextDouble() - 0.5f) * 1.7f * S);
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int s = 0; s < tramos; s++)
            {
                ang += ((float)rnd.NextDouble() - 0.5f) * 1.3f;
                Vector2 q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (5f + (float)rnd.NextDouble() * 8f) * K;
                if (!Dentro(q.x, q.y)) break;
                segmento(px, N, N, p, q, 1.1f, new Color(0.02f, 0.008f, 0.005f, alfa));
                p = q;
            }
        }
    }

    // Mancha de sangre vieja y suave, detras de la fila de corazones. Transparente en los bordes (no marca el cuadro del overlay)
    // y de alfa bajo (maximo ~0,6) para no tapar la vista. 256x112 = misma proporcion que la RenderTexture 512x224.
    static Texture2D ManchaFila(int seed)
    {
        const int W = 256, H = 112;
        var px = new Color[W * H];
        float ox = seed * 2.3f, oy = seed * 4.1f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f - W * 0.5f) / (W * 0.5f), v = (y + 0.5f - H * 0.5f) / (H * 0.5f);
                float d = Mathf.Sqrt(u * u * 0.85f + v * v * 1.1f);
                float n = Mathf.PerlinNoise(x * 0.035f + ox, y * 0.05f + oy) * 0.6f + Mathf.PerlinNoise(x * 0.13f + oy, y * 0.17f + ox) * 0.4f;
                float forma = 1f - Suave(0.40f, 0.98f, d + (n - 0.5f) * 0.6f);
                float costra = Suave(0.5f, 0.8f, Mathf.PerlinNoise(x * 0.08f + oy, y * 0.08f + ox));
                float a = forma * Mathf.Lerp(0.28f, 0.62f, n);
                float borde = Mathf.Min(Mathf.Min(x, W - 1 - x) / 10f, Mathf.Min(y, H - 1 - y) / 8f);
                a *= Mathf.Clamp01(borde);
                Color c = Color.Lerp(new Color(0.21f, 0.014f, 0.011f), new Color(0.07f, 0.005f, 0.004f), costra);
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
        // Chorreones que cuelgan de la parte baja de la mancha.
        var rnd = new System.Random(seed);
        for (int i = 0; i < 5; i++)
        {
            float x = 40f + (float)rnd.NextDouble() * 176f;
            float largo = 10f + (float)rnd.NextDouble() * 16f;
            segmento(px, W, H, new Vector2(x, 44f), new Vector2(x + ((float)rnd.NextDouble() - 0.5f) * 3f, 44f - largo), 1.3f, new Color(0.14f, 0.01f, 0.008f, 0.5f));
        }
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    static void CrearMaterial(string nombre, string shaderNombre)
    {
        var shader = Shader.Find(shaderNombre);
        if (shader == null) { Debug.LogError("[BackroomsVidasBuilder] No se encontro el shader " + shaderNombre); return; }
        string path = Dir + "/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader) { name = nombre };
            AssetDatabase.CreateAsset(mat, path);
        }
        else mat.shader = shader;
        EditorUtility.SetDirty(mat);
    }

    static void Guardar(string nombre, Texture2D t)
    {
        string path = Dir + "/" + nombre + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 256;
        imp.textureCompression = TextureImporterCompression.Compressed;
        // Android (Quest 2): ASTC 6x6, calidad media.
        imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 256,
            format = TextureImporterFormat.ASTC_6x6,
            compressionQuality = 50,
        });
        imp.SaveAndReimport();
    }
}
