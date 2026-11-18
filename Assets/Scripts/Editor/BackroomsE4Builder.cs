using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Construye la UI de E4 en Backrooms_Main (paneles en espacio mundo segun los wireframes del informe,
// reloj de muñeca, puntero laser) y cablea el GameManager. Menu: Backrooms/Construir E4.
public static class BackroomsE4Builder
{
    // Paleta del informe: fondo oscuro, amarillo para titulos/accion principal, rojo solo derrota, verde solo victoria.
    static readonly Color FondoPanel = new Color(0.115f, 0.11f, 0.10f, 1f); // opaco: nada se transparenta detras del texto
    static readonly Color Amarillo = new Color(0.88f, 0.78f, 0.38f);
    static readonly Color GrisBoton = new Color(0.25f, 0.24f, 0.22f);
    static readonly Color TextoClaro = new Color(0.93f, 0.91f, 0.84f);
    static readonly Color TextoGris = new Color(0.74f, 0.72f, 0.65f);
    static readonly Color TextoOscuro = new Color(0.10f, 0.09f, 0.06f);

    const float EscalaCanvas = 0.0015f; // 1000 px -> 1,5 m (a 1,5 m ocupa ~53 grados; el texto de 28 px queda en ~1,6 grados)
    static Font fuente;
    static Sprite sprite;

    [MenuItem("Backrooms/Construir E4")]
    public static void Construir()
    {
        fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var viejo = GameObject.Find("UI_E4");
        if (viejo != null) Object.DestroyImmediate(viejo);
        var viejoGm = GameObject.Find("GameManager");
        if (viejoGm != null) Object.DestroyImmediate(viejoGm);

        var raiz = new GameObject("UI_E4");
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();

        // ---------- Menu (wireframe 1) ----------
        var menu = Panel("Panel_Menu", raiz.transform, new Vector2(1000, 640));
        Texto(menu.transform, "Titulo", new Vector2(0, 205), new Vector2(940, 100), "BACKROOMS VR · NIVEL 0", 60, Amarillo, TextAnchor.MiddleCenter, true);
        Texto(menu.transform, "Subtitulo", new Vector2(0, 120), new Vector2(900, 60), "Encuentra la salida antes de que te encuentre", 32, TextoGris, TextAnchor.MiddleCenter, false);
        var bComenzar = Boton("Boton_Comenzar", menu.transform, new Vector2(0, 10), new Vector2(560, 100), "Comenzar", 44, Amarillo, TextoOscuro, true);
        var bTutorial = Boton("Boton_Tutorial", menu.transform, new Vector2(0, -110), new Vector2(560, 90), "Tutorial", 38, GrisBoton, TextoClaro, false);
        var bSalir = Boton("Boton_Salir", menu.transform, new Vector2(0, -215), new Vector2(560, 90), "Salir", 38, GrisBoton, TextoClaro, false);
        Enlazar(bComenzar, gm.Comenzar);
        Enlazar(bTutorial, gm.AbrirTutorial);
        Enlazar(bSalir, gm.Salir);

        // ---------- Tutorial (wireframe 2) ----------
        var tut = Panel("Panel_Tutorial", raiz.transform, new Vector2(1000, 640));
        Texto(tut.transform, "Titulo", new Vector2(0, 270), new Vector2(900, 70), "CÓMO JUGAR", 48, Amarillo, TextAnchor.MiddleCenter, true);
        string[,] pasos =
        {
            { "Stick + gatillo", "Apunta y suelta para teletransportarte" },
            { "Agáchate", "Entra a un nicho para esconderte" },
            { "Mira tu muñeca", "El reloj muestra el tiempo restante" },
            { "Escucha", "El zumbido más fuerte guía a la salida" },
        };
        for (int i = 0; i < 4; i++)
        {
            float y = 150 - i * 95;
            var num = Imagen(tut.transform, "Num_" + (i + 1), new Vector2(-410, y), new Vector2(64, 64), Amarillo);
            Texto(num.transform, "N", Vector2.zero, new Vector2(64, 64), (i + 1).ToString(), 38, TextoOscuro, TextAnchor.MiddleCenter, true);
            Texto(tut.transform, "Paso_" + (i + 1), new Vector2(60, y + 18), new Vector2(760, 50), pasos[i, 0], 38, TextoClaro, TextAnchor.MiddleLeft, true);
            Texto(tut.transform, "Detalle_" + (i + 1), new Vector2(60, y - 22), new Vector2(760, 40), pasos[i, 1], 28, TextoGris, TextAnchor.MiddleLeft, false);
        }
        var bEntendido = Boton("Boton_Entendido", tut.transform, new Vector2(270, -265), new Vector2(400, 85), "Entendido", 40, Amarillo, TextoOscuro, true);
        var bVolverTut = Boton("Boton_VolverMenu", tut.transform, new Vector2(-270, -265), new Vector2(400, 85), "Omitir", 36, GrisBoton, TextoClaro, false);
        Enlazar(bEntendido, gm.TutorialEntendido);
        Enlazar(bVolverTut, gm.MostrarMenu);

        // ---------- Pausa / volumen (RNF-08) ----------
        var pausa = Panel("Panel_Pausa", raiz.transform, new Vector2(1000, 640));
        Texto(pausa.transform, "Titulo", new Vector2(0, 235), new Vector2(900, 90), "PAUSA", 62, Amarillo, TextAnchor.MiddleCenter, true);
        var volTexto = Texto(pausa.transform, "Volumen", new Vector2(0, 130), new Vector2(700, 60), "Volumen: 80%", 40, TextoClaro, TextAnchor.MiddleCenter, false);
        var bMenos = Boton("Boton_VolMenos", pausa.transform, new Vector2(-300, 40), new Vector2(130, 100), "−", 60, GrisBoton, TextoClaro, false);
        var bMas = Boton("Boton_VolMas", pausa.transform, new Vector2(300, 40), new Vector2(130, 100), "+", 60, GrisBoton, TextoClaro, false);
        var barraFondo = Imagen(pausa.transform, "Barra_Fondo", new Vector2(0, 40), new Vector2(300, 36), GrisBoton);
        var relleno = Imagen(barraFondo.transform, "Barra_Relleno", new Vector2(-150, 0), new Vector2(240, 36), Amarillo);
        var rr = relleno.rectTransform;
        rr.pivot = new Vector2(0f, 0.5f);
        rr.anchoredPosition = new Vector2(-150, 0);
        var bReanudar = Boton("Boton_Reanudar", pausa.transform, new Vector2(0, -75), new Vector2(560, 95), "Reanudar", 42, Amarillo, TextoOscuro, true);
        var bMenuPausa = Boton("Boton_MenuPrincipal", pausa.transform, new Vector2(0, -185), new Vector2(560, 90), "Menú principal", 38, GrisBoton, TextoClaro, false);
        Enlazar(bMenos, gm.VolumenMenos);
        Enlazar(bMas, gm.VolumenMas);
        Enlazar(bReanudar, gm.Reanudar);
        Enlazar(bMenuPausa, gm.MostrarMenu);

        // ---------- Fin (wireframes 5 y 6) ----------
        var fin = Panel("Panel_Fin", raiz.transform, new Vector2(1000, 640));
        var finTitulo = Texto(fin.transform, "Titulo", new Vector2(0, 235), new Vector2(940, 100), "TE ENCONTRÓ", 72, Amarillo, TextAnchor.MiddleCenter, true);
        var finResumen = Texto(fin.transform, "Resumen", new Vector2(0, 140), new Vector2(940, 50), "Tiempo: 00:00   ·   Detectado: 0 veces", 34, TextoClaro, TextAnchor.MiddleCenter, false);
        var finDetalle = Texto(fin.transform, "Detalle", new Vector2(0, 90), new Vector2(940, 44), "Teletransportes: 0   ·   Escondites: 0", 28, TextoGris, TextAnchor.MiddleCenter, false);
        Texto(fin.transform, "VotoEtiqueta", new Vector2(0, 20), new Vector2(940, 44), "¿Te gustó la experiencia? (opcional)", 28, TextoGris, TextAnchor.MiddleCenter, false);
        var bSi = Boton("Boton_MeGusta", fin.transform, new Vector2(-190, -45), new Vector2(330, 80), "Me gusta", 34, GrisBoton, TextoClaro, false);
        var bNo = Boton("Boton_NoMeGusta", fin.transform, new Vector2(190, -45), new Vector2(330, 80), "No me gusta", 34, GrisBoton, TextoClaro, false);
        var bReintentar = Boton("Boton_Reintentar", fin.transform, new Vector2(-210, -190), new Vector2(380, 95), "Reintentar", 40, Amarillo, TextoOscuro, true);
        var bMenuFin = Boton("Boton_Menu", fin.transform, new Vector2(210, -190), new Vector2(380, 95), "Menú", 40, GrisBoton, TextoClaro, false);
        Enlazar(bSi, gm.VotoMeGusta);
        Enlazar(bNo, gm.VotoNoMeGusta);
        Enlazar(bReintentar, gm.Reintentar);
        Enlazar(bMenuFin, gm.MostrarMenu);

        foreach (var p in new[] { menu, tut, pausa, fin }) p.SetActive(false);

        // ---------- Reloj de muñeca (RF-10): controlador izquierdo ----------
        var cam = Object.FindObjectOfType<OVRCameraRig>();
        var reloj = CrearReloj(cam.leftControllerAnchor);

        // ---------- Puntero laser ----------
        var rigRoot = GameObject.Find("OVRCameraRigInteraction");
        var laser = rigRoot.GetComponent<LaserPointer>() ?? rigRoot.AddComponent<LaserPointer>();
        laser.manoDerecha = cam.rightControllerAnchor;
        laser.manoIzquierda = cam.leftControllerAnchor;
        laser.capasUI = 1 << LayerMask.NameToLayer("UI");
        EditorUtility.SetDirty(laser);

        // ---------- GameManager ----------
        gm.rig = rigRoot.transform;
        gm.cabeza = cam.centerEyeAnchor;
        gm.entidad = GameObject.Find("Entidad").GetComponent<EntityAI>();
        gm.salida = GameObject.Find("Salida").transform.Find("Puerta_Luz");
        gm.ruido = rigRoot.GetComponent<PlayerNoiseEmitter>();
        gm.reloj = reloj;
        gm.laser = laser;
        gm.panelMenu = menu; gm.panelTutorial = tut; gm.panelPausa = pausa; gm.panelFin = fin;
        gm.finTitulo = finTitulo; gm.finResumen = finResumen; gm.finDetalle = finDetalle;
        gm.finReintentarTexto = bReintentar.texto;
        gm.botonVotoMeGusta = bSi; gm.botonVotoNoMeGusta = bNo;
        gm.pausaVolumenTexto = volTexto; gm.pausaBarraRelleno = rr; gm.pausaBarraAncho = 300f;
        EditorUtility.SetDirty(gm);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BackroomsE4Builder] E4 construido.");
        BackroomsFixesBuilder.Construir(); // paneles siempre por encima + guardia de cabeza + filtro de teletransporte
        BackroomsMenuBuilder.Construir();  // menu de inicio con identidad Backrooms (reemplaza Panel_Menu y agrega Opciones / Creditos)
    }

    // ---------- reloj ----------
    static WristClock CrearReloj(Transform ancla)
    {
        var viejo = ancla.Find("RelojMuneca");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

        var go = new GameObject("RelojMuneca", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(ancla, false);
        // Sobre el controlador, mirando hacia arriba y un poco hacia el usuario; texto legible al girar la muñeca.
        go.transform.localPosition = new Vector3(0f, 0.035f, -0.02f);
        go.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
        go.transform.localScale = Vector3.one * 0.0005f; // 200x100 px -> 10x5 cm

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 100);
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 4f;

        var borde = Imagen(go.transform, "Borde", Vector2.zero, new Vector2(200, 100), new Color(0.55f, 0.53f, 0.48f));
        var fondo = Imagen(borde.transform, "Fondo", Vector2.zero, new Vector2(188, 88), new Color(0.07f, 0.07f, 0.06f));
        var t = Texto(fondo.transform, "Tiempo", Vector2.zero, new Vector2(188, 88), "08:00", 66, new Color(0.96f, 0.84f, 0.42f), TextAnchor.MiddleCenter, true);
        var reloj = go.AddComponent<WristClock>();
        reloj.texto = t;
        go.SetActive(false);
        return reloj;
    }

    // ---------- helpers de UI ----------
    static GameObject Panel(string nombre, Transform padre, Vector2 tam)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        go.transform.localScale = Vector3.one * EscalaCanvas;
        go.GetComponent<RectTransform>().sizeDelta = tam;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f;
        var fondo = Imagen(go.transform, "Fondo", Vector2.zero, tam, FondoPanel);
        fondo.sprite = sprite;
        fondo.type = Image.Type.Sliced;
        var borde = fondo.gameObject.AddComponent<Outline>();
        borde.effectColor = new Color(0.88f, 0.78f, 0.38f, 0.5f);
        borde.effectDistance = new Vector2(3, -3);
        return go;
    }

    static Image Imagen(Transform padre, string nombre, Vector2 pos, Vector2 tam, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Texto(Transform padre, string nombre, Vector2 pos, Vector2 tam, string contenido, int tamFuente, Color color, TextAnchor alineacion, bool negrita)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(padre, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        var t = go.AddComponent<Text>();
        t.font = fuente;
        t.text = contenido;
        t.fontSize = tamFuente;
        t.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
        t.color = color;
        t.alignment = alineacion;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static VRButton Boton(string nombre, Transform padre, Vector2 pos, Vector2 tam, string etiqueta, int tamFuente, Color fondo, Color colorTexto, bool negrita)
    {
        var img = Imagen(padre, nombre, pos, tam, fondo);
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        var col = img.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(tam.x, tam.y, 20f);
        var t = Texto(img.transform, "Texto", Vector2.zero, tam, etiqueta, tamFuente, colorTexto, TextAnchor.MiddleCenter, negrita);
        var b = img.gameObject.AddComponent<VRButton>();
        b.fondo = img;
        b.texto = t;
        return b;
    }

    static void Enlazar(VRButton boton, UnityAction accion)
    {
        UnityEventTools.AddPersistentListener(boton.alHacerClick, accion);
    }
}
