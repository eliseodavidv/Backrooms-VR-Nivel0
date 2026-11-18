using UnityEngine;
using UnityEngine.UI;

// Indicador de vidas (RF-18): fila horizontal de 3 corazones, estatica, en la esquina inferior izquierda de la vista (estilo HUD de
// Minecraft con la estetica de terror del menu; arriba a la izquierda, 14-32 grados a la izquierda y 18 grados arriba). SIEMPRE 3 corazones: los perdidos se ven grises y agrietados.
//
// Tecnica (para que quede estable en el Quest 2): el canvas de corazones NO esta en la escena de juego. Vive en una raiz aparte,
// lejos, en la capa 31, y una camara dedicada (ortografica, solo esa capa) lo dibuja en una RenderTexture transparente de 512x224.
// Un OVROverlay de tipo Quad (capa del compositor, Overlay = por encima de todo) hijo del CenterEyeAnchor muestra esa textura.
// (La RenderTexture es 512x224 y el overlay 22x9,6 grados; ver las constantes.)
// Al ser capa del compositor y estar pegada a la cabeza (head-locked), el visor la coloca con la pose real de ese instante:
// no flota ni se retrasa. Sin lerp ni slerp: seguimiento rigido.
// Sin visor (Editor) OVROverlay se desactiva solo; entonces se ve un quad normal con la misma textura (tambien es el respaldo
// si el overlay fallara). Con el overlay activo ese quad se apaga solo.
//
// Se actualiza por eventos: SaludJugador.OnVidaCambiada, GameManager.OnFaseCambiada y PursuitFeedback.AlLatir.
// Solo se ve durante la partida: fuera de ella se apagan la camara, el overlay y el LateUpdate (no cuesta nada en menus).
[DefaultExecutionOrder(19000)]
public class IndicadorVidas : MonoBehaviour
{
    // ---- Colocacion respecto a la cabeza (grados): esquina SUPERIOR izquierda. Borde izquierdo a 14, borde derecho a 32, centro 18 hacia arriba ----
    const float Distancia = 1.2f;
    const float AnguloIzquierda = 23f;       // centro de la fila (14..32)
    const float AnguloArriba = 18f;
    const float AnchoGrados = 22f;           // overlay: fila de 18 + 2 de margen a cada lado (ahi se ve la mancha de sangre)

    // ---- RenderTexture / canvas: 1 unidad = 1 pixel; 512 px = 22 grados (23,3 px por grado) ----
    const int RtAncho = 512, RtAlto = 224;
    const float PxPorGrado = RtAncho / AnchoGrados;
    const float AltoGrados = AnchoGrados * RtAlto / RtAncho;   // 9,6 grados: misma proporcion que la textura
    const float Celda = 6f * PxPorGrado;                       // 6 grados por corazon (el dibujo ocupa ~88 % de la celda)
    const int Max = 3;
    const int CapaHud = 31;                  // capa sin nombre; la excluyen las camaras del visor
    const float DuracionPerdida = 0.5f;

    private SaludJugador salud;
    private GameManager gm;
    private GameObject hudRaiz;
    private Camera camHud;
    private RenderTexture rt;
    private GameObject overlayGo;
    private Camera[] camarasVisor;
    private int[] mascarasVisor;

    private readonly Image[] corazones = new Image[Max];
    private readonly Image[] destellos = new Image[Max];
    private readonly RectTransform[] rtCorazon = new RectTransform[Max];
    private readonly RectTransform[] rtDestello = new RectTransform[Max];
    private readonly float[] inicioPerdida = new float[Max];   // < 0 = sin animacion

    private Sprite spVivo, spPerdido;
    private int vidas = Max;
    private float finInvulnerable;
    private float pulso;                 // latido fuerte sincronizado con el sonido (solo con 1 vida)

    // Parpadeo de tubo fluorescente con 1 vida
    private bool luzOn = true;
    private int rafaga;
    private float proxCambio, proxRafaga;

    public bool Visible { get { return overlayGo != null && overlayGo.activeSelf; } }
    public int VidasMostradas { get { return vidas; } }

    // La crea SaludJugador al empezar la escena. Sin camara del visor no hace nada.
    public static IndicadorVidas Crear(SaludJugador salud, GameManager gm)
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig == null || rig.centerEyeAnchor == null) return null;
        var go = new GameObject("Vidas_Pivot");
        go.transform.SetParent(rig.centerEyeAnchor, false);
        var iv = go.AddComponent<IndicadorVidas>();
        iv.Construir(salud, gm, rig);
        return iv;
    }

    void Construir(SaludJugador s, GameManager g, OVRCameraRig rig)
    {
        salud = s; gm = g;

        spVivo = Resources.Load<Sprite>("Vidas/corazon_vivo");
        spPerdido = Resources.Load<Sprite>("Vidas/corazon_perdido");
        var spMancha = Resources.Load<Sprite>("Vidas/mancha_fila");
        var matHud = Resources.Load<Material>("Vidas/Mat_Vidas_Hud");
        var matQuad = Resources.Load<Material>("Vidas/Mat_Vidas_Quad");
        if (matHud == null || matQuad == null) Debug.LogWarning("[IndicadorVidas] Faltan Mat_Vidas_Hud / Mat_Vidas_Quad (menu Backrooms/Generar texturas de vidas).");

        // RenderTexture transparente, sin MSAA ni mipmaps. Premultiplicada (ver shader Backrooms/UIHud).
        rt = new RenderTexture(RtAncho, RtAlto, 0, RenderTextureFormat.ARGB32) { name = "RT_Vidas", antiAliasing = 1, useMipMap = false, autoGenerateMips = false, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        rt.Create();

        ConstruirHud(spMancha, matHud);
        ConstruirOverlay(matQuad);
        ExcluirCapaHudDeLasCamarasDelVisor(rig);

        salud.OnVidaCambiada += AlCambiarVida;
        if (gm != null) gm.OnFaseCambiada += AlCambiarFase;
        PursuitFeedback.AlLatir += AlLatir;

        vidas = salud.Vida;
        PintarEstadoFijo();
        proxRafaga = Time.unscaledTime + 1f;
        Mostrar(gm != null && gm.fase == GameManager.Fase.Jugando);
    }

    // Raiz del canvas + camara dedicada, lejos del juego. La camara solo ve la capa 31 y escribe en la RenderTexture.
    void ConstruirHud(Sprite spMancha, Material matHud)
    {
        hudRaiz = new GameObject("Vidas_HUD");
        hudRaiz.transform.position = new Vector3(0f, -3000f, 0f);

        var goCam = new GameObject("Vidas_Camara");
        goCam.transform.SetParent(hudRaiz.transform, false);
        goCam.transform.localPosition = new Vector3(0f, 0f, -10f);
        camHud = goCam.AddComponent<Camera>();
        camHud.orthographic = true;
        camHud.orthographicSize = RtAlto * 0.5f;
        camHud.nearClipPlane = 0.1f;
        camHud.farClipPlane = 30f;
        camHud.cullingMask = 1 << CapaHud;
        camHud.clearFlags = CameraClearFlags.SolidColor;
        camHud.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camHud.allowMSAA = false;
        camHud.allowHDR = false;
        camHud.useOcclusionCulling = false;
        camHud.stereoTargetEye = StereoTargetEyeMask.None;
        camHud.targetTexture = rt;
        camHud.depth = -50;
        camHud.enabled = false;

        var lienzo = new GameObject("Vidas_Canvas", typeof(RectTransform));
        lienzo.layer = CapaHud;
        lienzo.transform.SetParent(hudRaiz.transform, false);
        lienzo.GetComponent<RectTransform>().sizeDelta = new Vector2(RtAncho, RtAlto);
        var canvas = lienzo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camHud;

        // Mancha de sangre vieja detras de la fila (identidad, sin tapar la vista: alfa bajo y bordes transparentes).
        if (spMancha != null)
        {
            var m = NuevaImagen("Mancha", lienzo.transform, Vector2.zero, new Vector2(RtAncho, RtAlto), spMancha, matHud);
            m.color = new Color(1f, 1f, 1f, 0.9f);
        }

        for (int i = 0; i < Max; i++)
        {
            float x = (i - 1) * Celda;
            var tam = new Vector2(Celda, Celda);
            corazones[i] = NuevaImagen("Corazon_" + i, lienzo.transform, new Vector2(x, 0f), tam, spVivo, matHud);
            destellos[i] = NuevaImagen("Destello_" + i, lienzo.transform, new Vector2(x, 0f), tam, spVivo, matHud);
            destellos[i].color = new Color(0.95f, 0.10f, 0.06f, 0f);
            destellos[i].enabled = false;
            rtCorazon[i] = corazones[i].rectTransform;
            rtDestello[i] = destellos[i].rectTransform;
            inicioPerdida[i] = -1f;
        }
    }

    // OVROverlay Quad, Overlay, dinamico, hijo del CenterEyeAnchor (via Vidas_Pivot, que esta en pose identidad). Mas un quad
    // normal de respaldo en el mismo objeto. El objeto nace inactivo: asi la textura queda asignada antes de que OVROverlay despierte.
    void ConstruirOverlay(Material matQuad)
    {
        // Direccion de la esquina: Euler(x<0) mira hacia arriba, Euler(y<0) hacia la izquierda. El quad queda de cara al ojo
        // (su "adelante" apunta alejandose de la cabeza, como un canvas de espacio mundo).
        var dir = Quaternion.Euler(-AnguloArriba, -AnguloIzquierda, 0f);
        float ancho = 2f * Distancia * Mathf.Tan(AnchoGrados * 0.5f * Mathf.Deg2Rad);
        float alto = 2f * Distancia * Mathf.Tan(AltoGrados * 0.5f * Mathf.Deg2Rad);

        overlayGo = new GameObject("Vidas_Overlay");
        overlayGo.SetActive(false);
        overlayGo.transform.SetParent(transform, false);
        overlayGo.transform.localPosition = dir * (Vector3.forward * Distancia);
        overlayGo.transform.localRotation = dir;
        overlayGo.transform.localScale = new Vector3(ancho, alto, 1f);

        overlayGo.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = overlayGo.AddComponent<MeshRenderer>();
        if (matQuad != null)
        {
            var inst = new Material(matQuad) { name = "Mat_Vidas_Quad (instancia)" };
            inst.mainTexture = rt;
            mr.sharedMaterial = inst;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        var ov = overlayGo.AddComponent<OVROverlay>();
        ov.currentOverlayShape = OVROverlay.OverlayShape.Quad;
        ov.currentOverlayType = OVROverlay.OverlayType.Overlay;   // Overlay (no Underlay): siempre por encima de paredes y objetos
        ov.noDepthBufferTesting = true;
        ov.compositionDepth = 0;
        ov.isDynamic = true;                                      // la RenderTexture cambia cada cuadro
        ov.isAlphaPremultiplied = true;                           // el shader Backrooms/UIHud deja el RGB premultiplicado
        ov.textures = new Texture[] { rt, null };                 // mono: solo el indice 0
    }

    // Las camaras del visor no deben dibujar la capa 31 (el canvas esta lejos, pero asi no hay duda).
    void ExcluirCapaHudDeLasCamarasDelVisor(OVRCameraRig rig)
    {
        var lista = new System.Collections.Generic.List<Camera>();
        foreach (var t in new Transform[] { rig.centerEyeAnchor, rig.leftEyeAnchor, rig.rightEyeAnchor })
        {
            if (t == null) continue;
            var c = t.GetComponent<Camera>();
            if (c != null) lista.Add(c);
        }
        camarasVisor = lista.ToArray();
        mascarasVisor = new int[camarasVisor.Length];
        for (int i = 0; i < camarasVisor.Length; i++)
        {
            mascarasVisor[i] = camarasVisor[i].cullingMask;
            camarasVisor[i].cullingMask &= ~(1 << CapaHud);
        }
    }

    Image NuevaImagen(string nombre, Transform padre, Vector2 pos, Vector2 tam, Sprite sprite, Material mat)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = CapaHud;
        go.transform.SetParent(padre, false);
        var rtr = go.GetComponent<RectTransform>();
        rtr.anchoredPosition = pos;
        rtr.sizeDelta = tam;
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        if (mat != null) img.material = mat;
        return img;
    }

    void OnDestroy()
    {
        if (salud != null) salud.OnVidaCambiada -= AlCambiarVida;
        if (gm != null) gm.OnFaseCambiada -= AlCambiarFase;
        PursuitFeedback.AlLatir -= AlLatir;
        if (camarasVisor != null)
            for (int i = 0; i < camarasVisor.Length; i++)
                if (camarasVisor[i] != null) camarasVisor[i].cullingMask = mascarasVisor[i];
        if (camHud != null) camHud.targetTexture = null;
        if (hudRaiz != null) Destroy(hudRaiz);
        if (rt != null) { rt.Release(); Destroy(rt); }
    }

    // ---------- eventos ----------

    void AlCambiarFase(GameManager.Fase fase)
    {
        Mostrar(fase == GameManager.Fase.Jugando);
    }

    void AlCambiarVida(int nuevas)
    {
        bool perdio = nuevas < vidas;
        int anterior = vidas;
        vidas = Mathf.Clamp(nuevas, 0, Max);
        if (perdio && Visible)
        {
            // El corazon perdido es el de mas a la derecha que seguia vivo.
            for (int i = vidas; i < anterior && i < Max; i++) inicioPerdida[i] = Time.unscaledTime;
            finInvulnerable = Time.time + (salud != null ? salud.invulnerabilidad : 1.5f);
        }
        else
        {
            PintarEstadoFijo();
            finInvulnerable = 0f;
        }
        luzOn = true; rafaga = 0;
    }

    void AlLatir()
    {
        if (vidas == 1 && Visible) pulso = 1f;
    }

    // Solo en partida: camara, overlay y LateUpdate encendidos. Fuera de ella, todo apagado (no cuesta nada en menu/pausa/fin).
    // Este componente no se desuscribe nunca al ocultarse: la suscripcion a los eventos es de Construir a OnDestroy.
    void Mostrar(bool ver)
    {
        if (overlayGo == null) return;
        if (camHud != null) camHud.enabled = ver;
        overlayGo.SetActive(ver);
        enabled = ver;
        if (ver) PintarEstadoFijo();
    }

    // ---------- dibujo ----------

    // Vivos y perdidos segun 'vidas', sin animaciones en curso.
    void PintarEstadoFijo()
    {
        for (int i = 0; i < Max; i++)
        {
            inicioPerdida[i] = -1f;
            bool vivo = i < vidas;
            corazones[i].sprite = vivo ? spVivo : spPerdido;
            corazones[i].color = Color.white;
            rtCorazon[i].localScale = Vector3.one;
            destellos[i].enabled = false;
        }
    }

    // Latido suave de dos golpes (lub-dub), ciclo de 1,25 s. Devuelve 0..1.
    static float LatidoBase(float t)
    {
        float p = Mathf.Repeat(t, 1.25f);
        float a = Mathf.Exp(-Mathf.Pow((p - 0.10f) / 0.07f, 2f));
        float b = 0.6f * Mathf.Exp(-Mathf.Pow((p - 0.38f) / 0.09f, 2f));
        return Mathf.Clamp01(a + b);
    }

    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        pulso = Mathf.MoveTowards(pulso, 0f, dt * 4f);
        float luz = ParpadeoFluorescente(t);
        float suave = 1f + 0.05f * LatidoBase(t);              // latido suave, siempre, en los corazones vivos
        float fuerte = 1f + 0.14f * pulso;                     // golpe sincronizado con el sonido (1 vida)
        bool invulnerable = Time.time < finInvulnerable;

        for (int i = 0; i < Max; i++)
        {
            if (inicioPerdida[i] >= 0f)
            {
                AnimarPerdida(i, (t - inicioPerdida[i]) / DuracionPerdida);
                continue;
            }
            if (i >= vidas) continue;           // perdido: queda fijo

            float brillo = luz;
            // Invulnerabilidad: el ultimo corazon vivo parpadea suave.
            if (invulnerable && i == vidas - 1)
                brillo *= 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * 14f));
            float b = Mathf.Lerp(0.28f, 1f, brillo);
            corazones[i].color = new Color(b, b, b, 1f);
            rtCorazon[i].localScale = Vector3.one * (vidas == 1 ? suave * fuerte : suave);
        }
    }

    // k: 0..1. Primera mitad: se encoge y destella rojo; a mitad se cambia al corazon perdido y vuelve a su tamano.
    void AnimarPerdida(int i, float k)
    {
        if (k >= 1f)
        {
            inicioPerdida[i] = -1f;
            corazones[i].sprite = spPerdido;
            corazones[i].color = Color.white;
            rtCorazon[i].localScale = Vector3.one;
            destellos[i].enabled = false;
            return;
        }
        float escala, flash;
        if (k < 0.5f)
        {
            float u = k / 0.5f;
            escala = Mathf.Lerp(1.12f, 0.55f, u);
            flash = 1f - u * 0.5f;
            corazones[i].sprite = spVivo;
        }
        else
        {
            float u = (k - 0.5f) / 0.5f;
            escala = Mathf.Lerp(0.55f, 1f, u);
            flash = 0.5f * (1f - u);
            corazones[i].sprite = spPerdido;
        }
        corazones[i].color = Color.white;
        rtCorazon[i].localScale = Vector3.one * escala;
        rtDestello[i].localScale = Vector3.one * escala;
        destellos[i].enabled = true;
        destellos[i].sprite = k < 0.5f ? spVivo : spPerdido;
        destellos[i].color = new Color(0.95f, 0.10f, 0.06f, flash);
    }

    // Con 1 vida: rafagas cortas de fallo, como el tubo del titulo. Devuelve 0..1 (1 = encendido).
    float ParpadeoFluorescente(float t)
    {
        if (vidas != 1) return 1f;
        if (rafaga > 0)
        {
            if (t >= proxCambio)
            {
                rafaga--;
                luzOn = rafaga == 0 ? true : !luzOn;
                proxCambio = t + Random.Range(0.04f, 0.11f);
                if (rafaga == 0) proxRafaga = t + Random.Range(1.2f, 3.5f);
            }
        }
        else if (t >= proxRafaga)
        {
            rafaga = 2 * Random.Range(1, 4);     // par: termina encendida
            luzOn = false;
            proxCambio = t + Random.Range(0.04f, 0.11f);
        }
        return luzOn ? 1f : 0.08f;
    }
}
