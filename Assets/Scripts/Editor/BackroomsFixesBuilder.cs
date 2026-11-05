using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Correcciones tras la prueba en el Quest: filtro de alcance del teletransporte, guardia de cabeza en pared
// (fundido a negro + "Vuelve atras") y paneles que se dibujan siempre por encima de la geometria.
// Menu: Backrooms/Aplicar fixes del visor. Es idempotente y lo vuelve a llamar BackroomsE4Builder.
public static class BackroomsFixesBuilder
{
    const string DirMat = "Assets/Materiales/Backrooms";

    [MenuItem("Backrooms/Aplicar fixes del visor")]
    public static void Construir()
    {
        var cam = Object.FindObjectOfType<OVRCameraRig>();
        var cabeza = cam.centerEyeAnchor;
        var rig = GameObject.Find("OVRCameraRigInteraction");
        var laberinto = GameObject.Find("Laberinto").transform;
        var gm = Object.FindObjectOfType<GameManager>();

        AplicarMaterialPaneles();

        // ---------- fundido a negro + mensaje (hijos de la camara, solo visibles al tocar una pared) ----------
        var matFundido = CrearMaterial("Mat_FundidoNegro", "Backrooms/OverlayTop", new Color(0f, 0f, 0f, 0f));
        var matMensaje = CrearMaterial("Mat_MensajeFundido", "Backrooms/UIAlways", Color.white);
        matMensaje.renderQueue = 4200; // por encima del fundido (Overlay+100 = 4100)

        var fundidoT = cabeza.Find("FundidoNegro");
        if (fundidoT == null)
        {
            var q = new GameObject("FundidoNegro");
            q.transform.SetParent(cabeza, false);
            q.transform.localPosition = new Vector3(0f, 0f, 0.12f);
            q.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            q.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = q.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matFundido;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            fundidoT = q.transform;
        }

        var mensajeT = cabeza.Find("MensajeVuelveAtras");
        if (mensajeT == null) mensajeT = CrearMensaje(cabeza, matMensaje);

        // ---------- guardia de cabeza ----------
        var guardia = rig.GetComponent<HeadWallGuard>() ?? rig.AddComponent<HeadWallGuard>();
        guardia.cabeza = cabeza;
        guardia.laberinto = laberinto;
        guardia.gm = gm;
        guardia.fundido = fundidoT.GetComponent<Renderer>();
        guardia.mensaje = mensajeT.gameObject;
        EditorUtility.SetDirty(guardia);

        // ---------- filtro de alcance del teletransporte ----------
        var gate = rig.GetComponent<TeleportReachGate>() ?? rig.AddComponent<TeleportReachGate>();
        gate.cabeza = cabeza;
        gate.laberinto = laberinto;
        EditorUtility.SetDirty(gate);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BackroomsFixesBuilder] Fixes del visor aplicados.");
    }

    // Paneles de menu / tutorial / pausa / fin: todos sus Image y Text pasan al shader ZTest Always.
    public static void AplicarMaterialPaneles()
    {
        var mat = CrearMaterial("Mat_UIAlways", "Backrooms/UIAlways", Color.white);
        var raiz = GameObject.Find("UI_E4");
        if (raiz == null) return;
        int n = 0;
        foreach (var g in raiz.GetComponentsInChildren<Graphic>(true))
        {
            g.material = mat;
            EditorUtility.SetDirty(g);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[BackroomsFixesBuilder] " + n + " elementos de UI de los paneles con Backrooms/UIAlways (siempre por encima).");
    }

    static Transform CrearMensaje(Transform cabeza, Material mat)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var go = new GameObject("MensajeVuelveAtras", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(cabeza, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        go.transform.localScale = Vector3.one * 0.0005f; // 800 px -> 0,4 m a 0,5 m de la cara
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 260);
        go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;

        Text Linea(string nombre, Vector2 pos, string texto, int tam, Color color, bool negrita)
        {
            var t = new GameObject(nombre, typeof(RectTransform));
            t.layer = LayerMask.NameToLayer("UI");
            t.transform.SetParent(go.transform, false);
            var rt = t.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(780, 110);
            var tx = t.AddComponent<Text>();
            tx.font = font;
            tx.text = texto;
            tx.fontSize = tam;
            tx.fontStyle = negrita ? FontStyle.Bold : FontStyle.Normal;
            tx.color = color;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.raycastTarget = false;
            tx.material = mat;
            return tx;
        }

        Linea("Titulo", new Vector2(0, 40), "VUELVE ATRÁS", 78, new Color(0.93f, 0.91f, 0.84f), true);
        Linea("Detalle", new Vector2(0, -50), "Tu cabeza está dentro de una pared", 38, new Color(0.74f, 0.72f, 0.65f), false);
        go.SetActive(false);
        return go.transform;
    }

    static Material CrearMaterial(string nombre, string shader, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Backrooms");
        string path = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find(shader);
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }
}
