using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Interfaz de terror de Backrooms (una sola familia visual): menu de inicio, OPCIONES, CREDITOS y PAUSA.
// Paneles gastados, fuente sucia en los titulos, colores apagados, resaltado amarillo apagado al apuntar.
// Atmosfera barata para Quest 2: penumbra (niebla + luz ambiente), cono de luz fluorescente que parpadea,
// silueta de la entidad solo en los apagones, grano, viñeta y respiracion. Sin postproceso ni luces realtime.
// Menu: Backrooms/Construir Menu de inicio. Es idempotente: reemplaza lo anterior y reconecta el GameManager.
public static class BackroomsMenuBuilder
{
    const string DirFuente = "Assets/Fonts/Menu";
    const string DirAudio = "Assets/Audio/Menu";
    const string DirTex = "Assets/Texturas/Menu";
    const string DirMat = "Assets/Materiales/Backrooms";

    const float Escala = 0.002f;        // 1000 px -> 2 m de ancho a 2 m de distancia (~53 grados)
    const float AnchoPanel = 1000f;
    const float AltoPanel = 900f;
    const float AnchoMedidor = 440f;

    static readonly Color AmarilloTitulo = new Color(0.80f, 0.68f, 0.22f);   // amarillo desgastado
    static readonly Color TextoBoton = new Color(0.72f, 0.65f, 0.40f);       // apagado
    static readonly Color TextoBotonHover = new Color(0.04f, 0.03f, 0.01f);  // sobre la placa amarilla apagada
    static readonly Color TextoClaro = new Color(0.76f, 0.73f, 0.62f);       // textos largos (legibles)

    static TMP_FontAsset fuente, fuenteLimpia;
    static Material matTexto, matTitulo, matLimpio;
    static AudioClip clipClick, clipHover;
    static Sprite spPlaca, spPlacaHover, spChica, spChicaHover, spFondo, spSangre, spMarcas;
    static Texture2D texVineta, texCono, texGrano;

    [MenuItem("Backrooms/Construir Menu de inicio")]
    public static void Construir()
    {
        var gm = Object.FindObjectOfType<GameManager>();
        var ui = GameObject.Find("UI_E4");
        if (gm == null || ui == null)
        {
            Debug.LogError("[BackroomsMenuBuilder] No hay GameManager / UI_E4 en la escena. Ejecuta primero Backrooms/Construir E4.");
            return;
        }

        PrepararRecursos();

        // ---------- limpiar lo anterior (idempotente) ----------
        foreach (var n in new[] { "Panel_Menu", "Panel_Opciones", "Panel_Creditos", "Panel_Pausa", "Panel_Fin" })
        {
            var t = ui.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        var viejoAmb = gm.transform.Find("AmbienteMenu");
        if (viejoAmb != null) Object.DestroyImmediate(viejoAmb.gameObject);
        if (gm.cabeza != null)
            foreach (var n in new[] { "Vineta_Menu", "Capas_Menu" })
            {
                var v = gm.cabeza.Find(n);
                if (v != null) Object.DestroyImmediate(v.gameObject);
            }

        // ---------- ambiente: zumbido + terror (parpadeo, silueta, grano, susurros) ----------
        var ambGo = new GameObject("AmbienteMenu");
        ambGo.transform.SetParent(gm.transform, false);
        var amb = ambGo.AddComponent<AudioSource>();
        amb.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Placeholders/zumbido_salida.wav");
        amb.loop = true;
        amb.playOnAwake = false;
        amb.spatialBlend = 0f;
        amb.volume = 0.5f;
        var terror = ambGo.AddComponent<AmbienteMenuTerror>();
        terror.cabeza = gm.cabeza;
        terror.rig = gm.rig;
        terror.zumbido = amb;

        var susGo = new GameObject("Susurro");
        susGo.transform.SetParent(ambGo.transform, false);
        var sus = susGo.AddComponent<AudioSource>();
        sus.playOnAwake = false;
        sus.loop = false;
        sus.spatialBlend = 1f;
        sus.rolloffMode = AudioRolloffMode.Linear;
        sus.minDistance = 1f;
        sus.maxDistance = 8f;
        terror.susurro = sus;
        terror.susurros = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Placeholders/respiracion_entidad.wav"),
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Placeholders/latido.wav"),
        };

        terror.conoRend = CrearCono(ambGo.transform);
        terror.siluetaRend = CrearSilueta(ambGo.transform);

        // ---------- capas pegadas a la camara: viñeta + grano ----------
        var capas = new GameObject("Capas_Menu");
        capas.transform.SetParent(gm.cabeza, false);
        var vineta = CrearQuad("Vineta_Menu", capas.transform, texVineta, 2990, Color.black);
        // Opacidad baja a proposito: la mezcla es lineal y sobre un fondo casi negro el grano claro se amplifica mucho.
        var grano = CrearQuad("Grano_Menu", capas.transform, texGrano, 2992, new Color(1f, 1f, 1f, 0.10f));
        terror.vinetaTf = vineta.transform;
        terror.granoMesh = grano.GetComponent<MeshFilter>();
        capas.SetActive(false);

        // ---------- Panel_Menu ----------
        var menu = Panel("Panel_Menu", ui.transform);
        Imagen(menu.transform, "Tablon", new Vector2(0, -105), new Vector2(660, 600), Color.white, spFondo);
        // Titulo enorme con sangre seca, grietas y glitch raro.
        TituloGrande(menu.transform, new Vector2(0, 410), "BACKROOMS");
        string[] etiquetas = { "COMENZAR", "TUTORIAL", "OPCIONES", "CRÉDITOS", "SALIR" };
        UnityAction[] acciones = { gm.Comenzar, gm.AbrirTutorial, gm.AbrirOpciones, gm.AbrirCreditos, gm.Salir };
        for (int i = 0; i < etiquetas.Length; i++)
        {
            var b = Boton("Boton_" + etiquetas[i], menu.transform, new Vector2(0, 95 - i * 105), new Vector2(560, 92), etiquetas[i], 54, false);
            UnityEventTools.AddPersistentListener(b.alHacerClick, acciones[i]);
        }

        // ---------- Panel_Opciones ----------
        var opc = Panel("Panel_Opciones", ui.transform);
        Imagen(opc.transform, "Fondo_Panel", new Vector2(0, -70), new Vector2(880, 720), Color.white, spFondo);
        TituloSimple(opc.transform, "OPCIONES", new Vector2(0, 390), 170);
        var med = Medidor(opc.transform, gm, true);
        Texto(opc.transform, "Nota", new Vector2(0, -105), new Vector2(800, 90), "El volumen se guarda y también se usa en la pausa.", 36, TextoClaro, matLimpio, false, 0f);
        var bVolverO = Boton("Boton_Volver", opc.transform, new Vector2(0, -250), new Vector2(560, 100), "VOLVER", 56, false);
        UnityEventTools.AddPersistentListener(bVolverO.alHacerClick, gm.MostrarMenu);

        // ---------- Panel_Creditos ----------
        var cre = Panel("Panel_Creditos", ui.transform);
        Imagen(cre.transform, "Fondo_Panel", new Vector2(0, -40), new Vector2(880, 800), Color.white, spFondo);
        TituloSimple(cre.transform, "CRÉDITOS", new Vector2(0, 390), 170);
        string h = "#C8AE45";
        string cuerpo =
            "<color=" + h + ">EQUIPO</color>\n" +
            "Alejandro Vargas Rios\n" +
            "Eliseo David Velásquez Díaz\n" +
            "Paolo Centeno\n" +
            "<color=" + h + ">CURSO</color>\n" +
            "CS2H01  ·  UTEC\n" +
            "<color=" + h + ">ASSETS</color>\n" +
            "Pistol 92 (StarGames studio)\n" +
            "Crowbar Free (Undertaker3D)\n" +
            "Creep Horror Creature (AC Game Assets)\n" +
            "Book Head Monster (h3llowner)\n" +
            "Meta XR SDK  ·  Rubik Dirt (Google Fonts)\n" +
            "<color=" + h + ">SONIDOS</color>\n" +
            "Pixabay (Pixabay Content License)\n" +
            "low-monster-roar · single-pistol-gunshot · incesante-metalic-hit · tiny-vicious-creature · " +
            "intense-cinematic-heartbeat-sound-effect · faulty-fluorescent-light-starter-amp-hum · " +
            "drone-dark-mechanical-grinding-ambient-background · male-death-scream";
        var cuerpoTxt = Texto(cre.transform, "Cuerpo", new Vector2(0, 8), new Vector2(840, 600), cuerpo, 42, TextoClaro, matLimpio, false, 0f);
        cuerpoTxt.enableAutoSizing = true;
        cuerpoTxt.fontSizeMin = 24;      // con los sonidos la lista es mas larga: el texto se achica solo para caber
        cuerpoTxt.fontSizeMax = 44;
        cuerpoTxt.lineSpacing = -8f;
        var bVolverC = Boton("Boton_Volver", cre.transform, new Vector2(0, -330), new Vector2(560, 100), "VOLVER", 56, false);
        UnityEventTools.AddPersistentListener(bVolverC.alHacerClick, gm.MostrarMenu);

        // ---------- Panel_Pausa ----------
        var pau = Panel("Panel_Pausa", ui.transform);
        Imagen(pau.transform, "Fondo_Panel", new Vector2(0, -70), new Vector2(880, 720), Color.white, spFondo);
        TituloSimple(pau.transform, "PAUSA", new Vector2(0, 390), 190);
        var medP = Medidor(pau.transform, gm, false);
        var bReanudar = Boton("Boton_Reanudar", pau.transform, new Vector2(0, -135), new Vector2(560, 100), "REANUDAR", 56, false);
        var bMenuP = Boton("Boton_MenuPrincipal", pau.transform, new Vector2(0, -255), new Vector2(560, 100), "MENÚ PRINCIPAL", 50, false);
        UnityEventTools.AddPersistentListener(bReanudar.alHacerClick, gm.Reanudar);
        UnityEventTools.AddPersistentListener(bMenuP.alHacerClick, gm.MostrarMenu);

        // ---------- Panel_Fin (derrota / victoria): mismo estilo; INICIAR DE NUEVO reinicia todo desde cero ----------
        var fin = Panel("Panel_Fin", ui.transform);
        Imagen(fin.transform, "Fondo_Panel", new Vector2(0, -10), new Vector2(920, 840), Color.white, spFondo);
        var finTitulo = Texto(fin.transform, "Titulo", new Vector2(0, 410), new Vector2(880, 180), "TE ENCONTRÓ", 130, new Color(0.80f, 0.13f, 0.09f), matTitulo, true, 60f);
        // Las dos lineas de estadisticas se achican solas para caber en UNA linea (sin partirse).
        var finResumen = Texto(fin.transform, "Resumen", new Vector2(0, 270), new Vector2(860, 70), "Tiempo: 00:00   ·   Detectado: 0 veces", 40, TextoClaro, matLimpio, true, 22f);
        var finDetalle = Texto(fin.transform, "Detalle", new Vector2(0, 205), new Vector2(860, 60), "Teletransportes: 0   ·   Escondites: 0", 34, new Color(0.66f, 0.63f, 0.53f), matLimpio, true, 20f);
        Texto(fin.transform, "VotoEtiqueta", new Vector2(0, 125), new Vector2(880, 56), "¿Te gustó la experiencia? (opcional)", 34, new Color(0.66f, 0.63f, 0.53f), matLimpio, false, 0f);
        var bSi = Boton("Boton_MeGusta", fin.transform, new Vector2(-215, 35), new Vector2(380, 90), "ME GUSTA", 44, false);
        var bNo = Boton("Boton_NoMeGusta", fin.transform, new Vector2(215, 35), new Vector2(380, 90), "NO ME GUSTA", 40, false);
        var bDeNuevo = Boton("Boton_IniciarDeNuevo", fin.transform, new Vector2(0, -110), new Vector2(640, 100), "INICIAR DE NUEVO", 52, false);
        var bMenuFin = Boton("Boton_Menu", fin.transform, new Vector2(0, -230), new Vector2(640, 100), "MENÚ", 56, false);
        UnityEventTools.AddPersistentListener(bSi.alHacerClick, gm.VotoMeGusta);
        UnityEventTools.AddPersistentListener(bNo.alHacerClick, gm.VotoNoMeGusta);
        UnityEventTools.AddPersistentListener(bDeNuevo.alHacerClick, gm.IniciarDeNuevo);
        UnityEventTools.AddPersistentListener(bMenuFin.alHacerClick, gm.MostrarMenu);

        foreach (var p in new[] { menu, opc, cre, pau, fin }) p.SetActive(false);

        // ---------- terror: paneles que sigue / atenua ----------
        terror.grupos = new[] { menu.GetComponent<CanvasGroup>(), opc.GetComponent<CanvasGroup>(), cre.GetComponent<CanvasGroup>(), pau.GetComponent<CanvasGroup>() };
        terror.panelesSeguidos = new[] { menu.transform, opc.transform, cre.transform, pau.transform };
        EditorUtility.SetDirty(terror);

        // ---------- conectar el GameManager ----------
        gm.panelMenu = menu;
        gm.panelOpciones = opc;
        gm.panelCreditos = cre;
        gm.panelPausa = pau;
        gm.panelFin = fin;
        gm.finTituloTMP = finTitulo;
        gm.finResumenTMP = finResumen;
        gm.finDetalleTMP = finDetalle;
        gm.finReintentarTMP = bDeNuevo.textoTMP;
        gm.finTitulo = null; gm.finResumen = null; gm.finDetalle = null; gm.finReintentarTexto = null;   // los Text antiguos ya no existen
        gm.botonVotoMeGusta = bSi;
        gm.botonVotoNoMeGusta = bNo;
        gm.colorVictoria = new Color(0.45f, 0.62f, 0.38f);
        gm.colorDerrota = new Color(0.80f, 0.13f, 0.09f);
        gm.opcionesVolumenTexto = med.pct;
        gm.opcionesBarraRelleno = med.relleno;
        gm.opcionesBarraAncho = AnchoMedidor;
        gm.pausaVolumenTMP = medP.pct;
        gm.pausaBarraRelleno = medP.relleno;
        gm.pausaBarraAncho = AnchoMedidor;
        gm.pausaVolumenTexto = null;           // el Text antiguo de la pausa ya no existe
        gm.distanciaMenu = 2f;
        gm.ambienteMenu = amb;
        gm.vineta = capas;
        gm.terror = terror;
        gm.nieblaMenuColor = new Color(0.05f, 0.04f, 0.015f);
        gm.nieblaMenuDensidad = 0.16f;
        gm.luzAmbienteMenu = new Color(0.02f, 0.017f, 0.01f);
        var armas = GameObject.Find("Armas");
        gm.ocultosEnMenu = armas != null ? new[] { armas } : new GameObject[0];   // mesas + armas + rotulos (BARRA, PISTOLA)
        EditorUtility.SetDirty(gm);

        BackroomsFixesBuilder.AplicarMaterialPaneles(); // las Image pasan a ZTest Always (los TMP ya lo traen)
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BackroomsMenuBuilder] Interfaz de terror construida (menu, opciones, creditos, pausa, silueta, grano, cono de luz).");
    }

    // ================= recursos =================
    static void PrepararRecursos()
    {
        AssetDatabase.Refresh();
        Directory.CreateDirectory(DirTex);
        fuente = CrearFuente();
        matTexto = fuente.material;
        ConfigurarMaterial(matTexto, 0.22f);

        string pt = DirFuente + "/RubikDirt SDF Titulo.mat";
        matTitulo = AssetDatabase.LoadAssetAtPath<Material>(pt);
        if (matTitulo == null)
        {
            matTitulo = new Material(matTexto) { name = "RubikDirt SDF Titulo" };
            AssetDatabase.CreateAsset(matTitulo, pt);
        }
        ConfigurarMaterial(matTitulo, 0.20f);

        fuenteLimpia = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        string pl = DirFuente + "/LiberationSans SDF Menu.mat";
        matLimpio = AssetDatabase.LoadAssetAtPath<Material>(pl);
        if (matLimpio == null)
        {
            matLimpio = new Material(fuenteLimpia.material) { name = "LiberationSans SDF Menu" };
            AssetDatabase.CreateAsset(matLimpio, pl);
        }
        ConfigurarMaterial(matLimpio, 0.18f);

        clipClick = CrearClip("ui_click", 0.075f, (t, r) => (Ruido(t) * 0.45f + Mathf.Sin(2f * Mathf.PI * 1700f * t) * 0.55f) * Mathf.Exp(-t * 75f) * 0.8f);
        clipHover = CrearClip("ui_hover", 0.04f, (t, r) => Mathf.Sin(2f * Mathf.PI * 1100f * t) * Mathf.Exp(-t * 95f) * 0.35f);

        // Texturas procedurales (deterministas: siempre iguales).
        spPlaca = GuardarSprite("placa_oscura", Placa(512, 112, new Color(0.150f, 0.135f, 0.110f), 11), true);
        spPlacaHover = GuardarSprite("placa_hover", Placa(512, 112, new Color(0.62f, 0.52f, 0.17f), 12), true);
        spChica = GuardarSprite("placa_chica_oscura", Placa(128, 128, new Color(0.150f, 0.135f, 0.110f), 13), true);
        spChicaHover = GuardarSprite("placa_chica_hover", Placa(128, 128, new Color(0.62f, 0.52f, 0.17f), 14), true);
        spFondo = GuardarSprite("fondo_panel", Fondo(512, 512, 21), true);
        spSangre = GuardarSprite("titulo_sangre", Sangre(1024, 256, 31), true);
        spMarcas = GuardarSprite("medidor_marcas", Marcas(512, 64), true);
        texVineta = GuardarTextura("vineta_menu", Vineta(256), false, false);
        texCono = GuardarTextura("cono_luz", Cono(256), false, false);
        texGrano = GuardarTextura("grano_menu", Grano(128, 41), true, true);
    }

    static void ConfigurarMaterial(Material m, float grosorContorno)
    {
        // Shader de TextMeshPro con ZTest Always: el texto nunca queda tapado por una pared (igual que los otros paneles).
        m.shader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
        m.EnableKeyword("OUTLINE_ON");
        m.SetColor("_OutlineColor", new Color(0.02f, 0.012f, 0.005f, 1f));
        m.SetFloat("_OutlineWidth", grosorContorno);
        m.SetColor("_FaceColor", Color.white);
        EditorUtility.SetDirty(m);
    }

    static TMP_FontAsset CrearFuente()
    {
        string path = DirFuente + "/RubikDirt SDF.asset";
        var existente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existente != null) return existente;

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(DirFuente + "/RubikDirt-Regular.ttf");
        if (ttf == null)
        {
            Debug.LogWarning("[BackroomsMenuBuilder] Falta " + DirFuente + "/RubikDirt-Regular.ttf: se usa LiberationSans SDF.");
            return TMP_Settings.defaultFontAsset;
        }

        var fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        fa.name = "RubikDirt SDF";
        AssetDatabase.CreateAsset(fa, path);
        fa.material.name = "RubikDirt SDF Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);

        var sb = new System.Text.StringBuilder();
        for (char c = ' '; c <= '~'; c++) sb.Append(c);
        sb.Append("ÁÉÍÓÚÜÑáéíóúüñ¿¡·");
        fa.TryAddCharacters(sb.ToString(), out string faltan);
        if (!string.IsNullOrEmpty(faltan)) Debug.LogWarning("[BackroomsMenuBuilder] La fuente no tiene: " + faltan);

        foreach (var tex in fa.atlasTextures)
        {
            tex.name = "RubikDirt SDF Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
        }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
    }

    static float Ruido(float t) { return Mathf.Sin(t * 91237.3f) * Mathf.Cos(t * 40123.7f); }

    static AudioClip CrearClip(string nombre, float segundos, System.Func<float, int, float> onda)
    {
        Directory.CreateDirectory(DirAudio);
        string path = DirAudio + "/" + nombre + ".wav";
        if (!File.Exists(path))
        {
            const int tasa = 22050;
            int n = Mathf.RoundToInt(segundos * tasa);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + n * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(tasa); w.Write(tasa * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    float v = Mathf.Clamp(onda(i / (float)tasa, tasa), -1f, 1f);
                    w.Write((short)(v * 32767f));
                }
            }
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    // ================= texturas procedurales =================

    // Smoothstep de shader: 0 por debajo de a, 1 por encima de b (admite a > b para invertir). NO es Mathf.SmoothStep (que interpola).
    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    static void Mezclar(Color[] px, int i, Color s)
    {
        Color d = px[i];
        float a = s.a + d.a * (1f - s.a);
        if (a <= 0.0001f) return;
        px[i] = new Color((s.r * s.a + d.r * d.a * (1f - s.a)) / a, (s.g * s.a + d.g * d.a * (1f - s.a)) / a, (s.b * s.a + d.b * d.a * (1f - s.a)) / a, a);
    }

    static void Disco(Color[] px, int w, int h, float cx, float cy, float r, Color c, float suave)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + r + 1));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + r + 1));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > r) continue;
                float k = suave <= 0f ? 1f : Mathf.Clamp01((r - d) / (r * suave));
                Mezclar(px, y * w + x, new Color(c.r, c.g, c.b, c.a * k));
            }
    }

    static void Segmento(Color[] px, int w, int h, Vector2 a, Vector2 b, float radio, Color c)
    {
        float len = Vector2.Distance(a, b);
        int pasos = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, radio * 0.5f)));
        for (int i = 0; i <= pasos; i++) Disco(px, w, h, Mathf.Lerp(a.x, b.x, i / (float)pasos), Mathf.Lerp(a.y, b.y, i / (float)pasos), radio, c, 0f);
    }

    // Placa de metal / madera sucia: grano, manchas oscuras, rayones, borde gastado y remaches.
    static Texture2D Placa(int w, int h, Color baseC, int seed)
    {
        var rnd = new System.Random(seed);
        var px = new Color[w * h];
        float ox = seed * 13.7f, oy = seed * 7.3f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.020f + ox, y * 0.060f + oy) * 0.50f
                        + Mathf.PerlinNoise(x * 0.110f + ox, y * 0.110f + oy) * 0.30f
                        + (float)rnd.NextDouble() * 0.20f;
                float mancha = Suave(0.55f, 0.88f, Mathf.PerlinNoise(x * 0.009f + oy, y * 0.022f + ox));
                float v = Mathf.Lerp(0.55f, 1.15f, n) * (1f - mancha * 0.55f);
                int e = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                v *= Mathf.Lerp(0.40f, 1f, Mathf.Clamp01(e / 9f));
                px[y * w + x] = new Color(baseC.r * v, baseC.g * v, baseC.b * v, 1f);
            }
        for (int i = 0; i < 8; i++)
        {
            var a = new Vector2((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h);
            float ang = ((float)rnd.NextDouble() - 0.5f) * 0.5f;
            float len = 20f + (float)rnd.NextDouble() * 70f;
            var b = a + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
            Segmento(px, w, h, a, b, 0.6f, new Color(1f, 0.95f, 0.8f, 0.04f + (float)rnd.NextDouble() * 0.04f));
        }
        float rx = Mathf.Min(14f, w * 0.12f);
        foreach (var c in new[] { new Vector2(rx, h - rx), new Vector2(w - rx, h - rx), new Vector2(rx, rx), new Vector2(w - rx, rx) })
        {
            Disco(px, w, h, c.x, c.y, 5.5f, new Color(0.02f, 0.015f, 0.01f, 0.85f), 0.3f);
            Disco(px, w, h, c.x - 1f, c.y + 1f, 2.2f, new Color(0.55f, 0.5f, 0.4f, 0.55f), 0.8f);
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    // Fondo de los paneles: casi negro, sucio, con manchas y bordes oxidados. Un poco translucido.
    static Texture2D Fondo(int w, int h, int seed)
    {
        var rnd = new System.Random(seed);
        var px = new Color[w * h];
        float ox = seed * 5.1f, oy = seed * 9.9f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.012f + ox, y * 0.012f + oy) * 0.6f + (float)rnd.NextDouble() * 0.25f + Mathf.PerlinNoise(x * 0.07f + oy, y * 0.07f + ox) * 0.15f;
                float mancha = Suave(0.5f, 0.85f, Mathf.PerlinNoise(x * 0.006f + oy, y * 0.01f + ox));
                float v = Mathf.Lerp(0.045f, 0.13f, n) * (1f - mancha * 0.6f);
                int e = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y));
                float borde = 1f - Mathf.Clamp01(e / 16f);       // 1 en el borde
                Color c = new Color(v + borde * 0.09f, v * 0.9f + borde * 0.045f, v * 0.7f + borde * 0.015f, 0.93f);
                px[y * w + x] = c;
            }
        for (int i = 0; i < 9; i++)
        {
            var a = new Vector2((float)rnd.NextDouble() * w, (float)rnd.NextDouble() * h);
            var b = a + new Vector2(((float)rnd.NextDouble() - 0.5f) * 160f, ((float)rnd.NextDouble() - 0.5f) * 30f);
            Segmento(px, w, h, a, b, 0.8f, new Color(0.9f, 0.85f, 0.7f, 0.05f));
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    // Sangre seca: manchas, goteos que cuelgan de la linea base de las letras, y grietas oscuras.
    static Texture2D Sangre(int w, int h, int seed)
    {
        var rnd = new System.Random(seed);
        var px = new Color[w * h];
        int baseY = 112;                    // linea base de las letras (px desde abajo); las letras ocupan de 112 a 256
        Color rojo = new Color(0.30f, 0.025f, 0.025f);
        // manchas sobre las letras
        for (int i = 0; i < 16; i++)
            Disco(px, w, h, 30f + (float)rnd.NextDouble() * (w - 60f), baseY + 15f + (float)rnd.NextDouble() * 115f, 9f + (float)rnd.NextDouble() * 34f,
                  new Color(rojo.r, rojo.g, rojo.b, 0.30f + (float)rnd.NextDouble() * 0.40f), 0.7f);
        // goteos
        for (int i = 0; i < 14; i++)
        {
            float x = 40f + (float)rnd.NextDouble() * (w - 80f);
            float y0 = baseY + ((float)rnd.NextDouble() < 0.5f ? 0f : 20f + (float)rnd.NextDouble() * 90f);   // algunos nacen dentro de la letra
            float largo = 22f + (float)rnd.NextDouble() * 88f;
            float ancho = 2.2f + (float)rnd.NextDouble() * 3.6f;
            float yFin = Mathf.Max(6f, baseY - largo);
            Segmento(px, w, h, new Vector2(x, y0), new Vector2(x + ((float)rnd.NextDouble() - 0.5f) * 3f, yFin), ancho, new Color(rojo.r, rojo.g, rojo.b, 0.92f));
            Disco(px, w, h, x, yFin - ancho * 0.3f, ancho * 1.5f, new Color(rojo.r * 0.9f, rojo.g, rojo.b, 0.95f), 0.25f);
            Segmento(px, w, h, new Vector2(x - ancho * 0.35f, y0), new Vector2(x - ancho * 0.35f, yFin + 6f), ancho * 0.25f, new Color(0.5f, 0.08f, 0.06f, 0.55f)); // brillo
        }
        // grietas oscuras sobre las letras
        for (int i = 0; i < 11; i++)
        {
            var p = new Vector2(20f + (float)rnd.NextDouble() * (w - 40f), baseY + 20f + (float)rnd.NextDouble() * 110f);
            float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
            for (int s = 0; s < 12; s++)
            {
                ang += ((float)rnd.NextDouble() - 0.5f) * 1.3f;
                var q = p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (7f + (float)rnd.NextDouble() * 10f);
                Segmento(px, w, h, p, q, 1.1f, new Color(0.02f, 0.008f, 0.005f, 0.9f));
                p = q;
            }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    // Marcas del medidor: 11 rayas (0 a 100 %), mas largas en 0, 50 y 100.
    static Texture2D Marcas(int w, int h)
    {
        var px = new Color[w * h];
        var rnd = new System.Random(51);
        for (int i = 0; i < w * h; i++)
            if (rnd.NextDouble() < 0.03) px[i] = new Color(0f, 0f, 0f, 0.18f);
        for (int k = 0; k <= 10; k++)
        {
            int cx = Mathf.RoundToInt(3 + k * (w - 6) / 10f);
            bool mayor = k % 5 == 0;
            int ancho = mayor ? 3 : 2;
            int alto = mayor ? h : (int)(h * 0.55f);
            for (int y = h - alto; y < h; y++)
                for (int x = cx - ancho; x <= cx + ancho; x++)
                    if (x >= 0 && x < w) Mezclar(px, y * w + x, new Color(0.02f, 0.015f, 0.01f, 0.85f));
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        t.SetPixels(px);
        return t;
    }

    // Viñeta: casi transparente al centro, negra en los bordes.
    static Texture2D Vineta(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float k = Suave(0f, 1f, Mathf.InverseLerp(0.20f, 1.10f, d));
                t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.Lerp(0.50f, 0.985f, k)));
            }
        return t;
    }

    // Cono de luz de un fluorescente del techo: apice arriba, se ensancha hacia abajo y se desvanece. Cubre 6 x 6 m.
    static Texture2D Cono(int s)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = (1f - (y + 0.5f) / s) * 6f;                  // metros bajo el apice
                float u = ((x + 0.5f) / s - 0.5f) * 6f;
                float mitad = 0.45f + 0.85f * d;
                float ax = Mathf.Abs(u) / mitad;
                float a = 1f - Suave(0.10f, 1f, ax);
                a *= a;
                float fade = 1f - Suave(2.4f, 5.6f, d);
                float arriba = Suave(0f, 0.4f, d);
                t.SetPixel(x, y, new Color(1f, 0.88f, 0.46f, 0.26f * a * fade * arriba));
            }
        return t;
    }

    static Texture2D Grano(int s, int seed)
    {
        var rnd = new System.Random(seed);
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float g = (float)rnd.NextDouble();
                g *= g;                                    // sesgado a oscuro: el grano no levanta los negros
                t.SetPixel(x, y, new Color(g, g, g * 0.92f, 0.045f));
            }
        return t;
    }

    static Sprite GuardarSprite(string nombre, Texture2D t, bool comprimir)
    {
        string path = GuardarPng(nombre, t);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = comprimir ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Texture2D GuardarTextura(string nombre, Texture2D t, bool repetir, bool puntos)
    {
        string path = GuardarPng(nombre, t);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = repetir ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        imp.filterMode = puntos ? FilterMode.Point : FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;   // degradados sin bandas
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string GuardarPng(string nombre, Texture2D t)
    {
        string path = DirTex + "/" + nombre + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }

    // ================= objetos 3D baratos =================
    static Material MaterialUIAlways(string nombre, Texture2D tex, int cola, Color color)
    {
        string mp = DirMat + "/" + nombre + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Backrooms/UIAlways"));
            AssetDatabase.CreateAsset(mat, mp);
        }
        mat.shader = Shader.Find("Backrooms/UIAlways");
        mat.mainTexture = tex;
        mat.color = color;
        // Entre el mundo y la UI: antes del laser (cola 3000) y de los paneles (Overlay), con ZTest Always.
        mat.renderQueue = cola;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject CrearQuad(string nombre, Transform padre, Texture2D tex, int cola, Color tinte)
    {
        var mat = MaterialUIAlways("Mat_" + nombre, tex, cola, tinte);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = nombre;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(padre, false);
        q.transform.localPosition = new Vector3(0f, 0f, 2f);
        q.transform.localScale = new Vector3(8f, 8f, 1f);
        var mr = q.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return q;
    }

    static MeshRenderer CrearCono(Transform padre)
    {
        var q = CrearQuad("Cono_Luz", padre, texCono, 2991, Color.white);
        q.transform.localScale = new Vector3(6f, 6f, 1f);
        q.SetActive(false);
        return q.GetComponent<MeshRenderer>();
    }

    static MeshRenderer CrearSilueta(Transform padre)
    {
        string mp = DirMat + "/Mat_Silueta.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Backrooms/Silueta"));
            AssetDatabase.CreateAsset(mat, mp);
        }
        mat.shader = Shader.Find("Backrooms/Silueta");
        mat.color = Color.black;
        EditorUtility.SetDirty(mat);

        var go = new GameObject("Silueta_Entidad");
        go.transform.SetParent(padre, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Modelo 3D/Combate/BookHead_Low.asset");   // modelo que ya teniamos
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        mr.enabled = false;
        return mr;
    }

    // ================= helpers de UI =================
    static GameObject Panel(string nombre, Transform padre)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        go.transform.localScale = Vector3.one * Escala;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(AnchoPanel, AltoPanel);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        go.AddComponent<CanvasGroup>();      // los apagones lo atenuan
        return go;
    }

    static Image Imagen(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color, Sprite sprite)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Texto(Transform padre, string nombre, Vector2 pos, Vector2 tam, string contenido, float tamFuente,
                                 Color color, Material material, bool autoTamano, float tamMin)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = material == matLimpio ? fuenteLimpia : fuente;
        t.fontSharedMaterial = material;
        t.text = contenido;
        t.fontSize = tamFuente;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        if (autoTamano)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = tamMin;
            t.fontSizeMax = tamFuente;
        }
        return t;
    }

    // Titulo de panel: fuente sucia, contorno oscuro, parpadea con la luz del menu.
    static void TituloSimple(Transform padre, string texto, Vector2 pos, float tam)
    {
        var t = Texto(padre, "Titulo", pos, new Vector2(900, tam * 1.3f), texto, tam, AmarilloTitulo, matTitulo, true, 80f);
        var f = t.gameObject.AddComponent<TituloFluorescente>();
        f.colorBase = AmarilloTitulo;
    }

    // BACKROOMS enorme (1680 px = 3,4 m a 2 m), con sangre seca, grietas y glitch raro.
    static void TituloGrande(Transform padre, Vector2 pos, string texto)
    {
        var grupo = new GameObject("TituloGrupo", typeof(RectTransform));
        grupo.layer = LayerMask.NameToLayer("UI");
        grupo.transform.SetParent(padre, false);
        var grt = grupo.GetComponent<RectTransform>();
        grt.anchoredPosition = pos;
        grt.sizeDelta = new Vector2(1680, 300);

        // fantasma rojizo del glitch (apagado casi siempre)
        var fantasma = Texto(grupo.transform, "Titulo_Glitch", Vector2.zero, new Vector2(1680, 300), texto, 270, new Color(0.55f, 0.04f, 0.04f), matTitulo, true, 120f);
        fantasma.gameObject.SetActive(false);

        var titulo = Texto(grupo.transform, "Titulo", Vector2.zero, new Vector2(1680, 300), texto, 270, AmarilloTitulo, matTitulo, true, 120f);
        titulo.ForceMeshUpdate();
        Bounds b = titulo.textBounds;

        // La capa de sangre se alinea con las letras: la linea base de la textura (112 de 256 px) queda en la base del texto.
        float altoTex = b.size.y * 256f / 144f;
        float abajo = b.min.y - b.size.y * (112f / 144f);
        var sangre = Imagen(grupo.transform, "Sangre", new Vector2(b.center.x, abajo + altoTex * 0.5f), new Vector2(b.size.x * 1.03f, altoTex), Color.white, spSangre);

        var f = titulo.gameObject.AddComponent<TituloFluorescente>();
        f.colorBase = AmarilloTitulo;
        f.acompanantes = new Graphic[] { sangre };
        f.permitirGlitch = true;
        f.grupo = grt;
        f.fantasma = fantasma;
    }

    static VRButton Boton(string nombre, Transform padre, Vector2 pos, Vector2 tam, string etiqueta, float tamFuente, bool chico)
    {
        var img = Imagen(padre, nombre, pos, tam, Color.white, chico ? spChica : spPlaca);
        var col = img.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(tam.x, tam.y, 20f);

        var t = Texto(img.transform, "Texto", Vector2.zero, tam, etiqueta, tamFuente, TextoBoton, matTexto, false, 0f);
        var b = img.gameObject.AddComponent<VRButton>();
        b.fondo = img;
        b.textoTMP = t;
        b.estiloPropio = true;
        b.fondoHover = Color.white;
        b.spriteHover = chico ? spChicaHover : spPlacaHover;
        b.textoHover = TextoBotonHover;
        b.escalaHover = 1.05f;
        b.sonidoClick = clipClick;
        b.sonidoHover = clipHover;
        return b;
    }

    struct MedidorUI { public TMP_Text pct; public RectTransform relleno; }

    // Medidor de volumen viejo y sucio: titulo VOLUMEN en la fuente sucia, boton - y +, barra oscura con relleno amarillo apagado y rayas.
    static MedidorUI Medidor(Transform panel, GameManager gm, bool conNota)
    {
        Texto(panel, "Etiqueta_Volumen", new Vector2(0, 225), new Vector2(900, 130), "VOLUMEN", 100, AmarilloTitulo, matTitulo, false, 0f)
            .gameObject.AddComponent<TituloFluorescente>().colorBase = AmarilloTitulo;

        var bMenos = Boton("Boton_VolMenos", panel, new Vector2(-330, 100), new Vector2(140, 110), "-", 90, true);
        var bMas = Boton("Boton_VolMas", panel, new Vector2(330, 100), new Vector2(140, 110), "+", 90, true);
        UnityEventTools.AddPersistentListener(bMenos.alHacerClick, gm.VolumenMenos);
        UnityEventTools.AddPersistentListener(bMas.alHacerClick, gm.VolumenMas);

        var barra = Imagen(panel, "Barra_Fondo", new Vector2(0, 100), new Vector2(AnchoMedidor, 60), new Color(0.45f, 0.42f, 0.38f), spPlaca);
        var relleno = Imagen(barra.transform, "Barra_Relleno", new Vector2(-AnchoMedidor * 0.5f, 0), new Vector2(AnchoMedidor * 0.8f, 60), new Color(0.92f, 0.88f, 0.78f), spPlacaHover);
        var rr = relleno.rectTransform;
        rr.pivot = new Vector2(0f, 0.5f);
        rr.anchoredPosition = new Vector2(-AnchoMedidor * 0.5f, 0);
        Imagen(barra.transform, "Barra_Marcas", Vector2.zero, new Vector2(AnchoMedidor, 60), Color.white, spMarcas);

        var pct = Texto(panel, "Porcentaje", new Vector2(0, 10), new Vector2(500, 90), "80%", 70, TextoClaro, matLimpio, false, 0f);
        return new MedidorUI { pct = pct, relleno = rr };
    }
}
