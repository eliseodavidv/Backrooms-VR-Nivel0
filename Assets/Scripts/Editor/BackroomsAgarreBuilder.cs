using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Grab;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Arregla el agarre de las armas SIN reconstruir nada: trabaja sobre las armas que ya estan en "Armas".
// Por arma (idempotente, se puede relanzar):
//   - quita el GrabInteractable suelto del bloque 1 original (solo controles)
//   - anade las plantillas oficiales del Interaction SDK: control + mano (ISDK_HandGrabInteraction)
//     y a distancia, el arma vuela a la mano (ISDK_DistanceGrabInteraction), todas sobre el mismo Grabbable
//   - crea ZonaAgarre (collider trigger mas grande que el modelo, para que sea facil de tocar)
//   - material de resalte y rotulo sobre la mesa
// Menu: Backrooms/Combate/Agarre (controles + manos + distancia)
public static class BackroomsAgarreBuilder
{
    const string Plantillas = "Packages/com.meta.xr.sdk.interaction/Editor/QuickActions/Templates/";
    const string DirMat = "Assets/Materiales/Combate";

    [MenuItem("Backrooms/Combate/Agarre (controles + manos + distancia)")]
    public static void Aplicar()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Agarre] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return;
        }
        var raiz = GameObject.Find("Armas");
        if (raiz == null) { Debug.LogError("[Agarre] No existe la raiz 'Armas'."); return; }

        var plHand = AssetDatabase.LoadAssetAtPath<GameObject>(Plantillas + "Template_HandGrabInteraction.prefab");
        var plDist = AssetDatabase.LoadAssetAtPath<GameObject>(Plantillas + "Template_DistanceGrabInteraction_InteractableToHand.prefab");
        if (plHand == null || plDist == null) { Debug.LogError("[Agarre] No se encontraron las plantillas del SDK."); return; }

        var matResalte = MatUnlit("Mat_Resalte", new Color(1f, 0.85f, 0.1f));
        var matFondo = MatUnlit("Mat_RotuloFondo", new Color(0.04f, 0.04f, 0.06f));

        int n = 0;
        foreach (var arma in raiz.GetComponentsInChildren<ArmaAgarrable>(true))
        {
            var go = arma.gameObject;
            var rb = go.GetComponent<Rigidbody>();
            var grabbable = go.GetComponent<Grabbable>();
            var punto = go.transform.Find("PuntoAgarre");

            // --- limpiar lo anterior ---
            var viejo = go.GetComponent<GrabInteractable>();
            if (viejo != null) Object.DestroyImmediate(viejo);
            arma.interactable = null;
            foreach (var nombre in new[] { "ISDK_HandGrabInteraction", "ISDK_DistanceGrabInteraction", "ZonaAgarre" })
            {
                var t = go.transform.Find(nombre);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            // Un broadcaster suelto en la raiz solo existe en tiempo de ejecucion, pero por si acaso:
            foreach (var b in go.GetComponents<InteractableTriggerBroadcaster>()) Object.DestroyImmediate(b);

            // --- control + mano ---
            var h = Instanciar(plHand, go.transform, "ISDK_HandGrabInteraction");
            var hand = h.GetComponent<HandGrabInteractable>();
            hand.InjectRigidbody(rb);
            hand.InjectSupportedGrabTypes(GrabTypeFlags.All);
            hand.InjectOptionalPointableElement(grabbable);
            var gi = h.GetComponent<GrabInteractable>();
            gi.InjectRigidbody(rb);
            gi.InjectOptionalPointableElement(grabbable);
            if (punto != null) gi.InjectOptionalGrabSource(punto);

            // --- a distancia (vuela a la mano) ---
            var d = Instanciar(plDist, go.transform, "ISDK_DistanceGrabInteraction");
            var dh = d.GetComponentInChildren<DistanceHandGrabInteractable>();
            dh.InjectRigidbody(rb);
            dh.InjectSupportedGrabTypes(GrabTypeFlags.All);
            dh.InjectOptionalPointableElement(grabbable);
            var dg = d.GetComponentInChildren<DistanceGrabInteractable>();
            dg.InjectRigidbody(rb);
            dg.InjectOptionalPointableElement(grabbable);
            if (punto != null) dg.InjectOptionalGrabSource(punto);
            // Sin zona de enganche ni holograma: se quitan los componentes que lo requieren.
            QuitarPorNombre(d, "SnapInteractor");
            QuitarPorNombre(d, "ReticleDataMesh");
            QuitarPorNombre(d, "ReticleDataIcon"); // dibujaba un circulo blanco (Reticle-Circle) sobre el arma

            // --- zona de agarre grande ---
            AjustarZona(go.transform, arma is BarraMetal ? new Vector3(0.10f, 0.10f, 0.06f) : new Vector3(0.09f, 0.09f, 0.09f));

            // --- referencias de ArmaAgarrable ---
            arma.interactables = new MonoBehaviour[] { gi, hand, dg, dh };
            arma.materialResalte = matResalte;
            arma.rotulo = CrearRotulo(go.transform, arma is BarraMetal ? "BARRA" : "PISTOLA",
                arma is BarraMetal ? "apunta y aprieta el agarre\n(o toca el arma con la mano)" : "apunta y aprieta el agarre\ngatillo = disparar (6 balas)", matFondo);
            EditorUtility.SetDirty(arma);
            EditorUtility.SetDirty(grabbable);
            n++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Agarre] Listo en " + n + " arma(s): control + mano + distancia, zona de agarre, resalte y rotulo.");
    }

    // ---------- piezas ----------

    static GameObject Instanciar(GameObject plantilla, Transform padre, string nombre)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(plantilla, padre);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = nombre;
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        return inst;
    }

    static void QuitarPorNombre(GameObject raiz, string tipo)
    {
        foreach (var c in raiz.GetComponentsInChildren<Component>(true))
            if (c != null && c.GetType().Name == tipo) Object.DestroyImmediate(c);
    }

    // Collider trigger que envuelve los renderers del arma mas un margen (en ejes locales del arma).
    public static void AjustarZona(Transform arma, Vector3 margen)
    {
        var existente = arma.Find("ZonaAgarre");
        if (existente != null) Object.DestroyImmediate(existente.gameObject);

        bool primero = true;
        Bounds b = new Bounds();
        foreach (var r in arma.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.gameObject.name == "Destello" || r.GetComponent<TextMesh>() != null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            foreach (var sx in new[] { -1f, 1f }) foreach (var sy in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            {
                Vector3 esquina = mb.center + Vector3.Scale(mb.extents, new Vector3(sx, sy, sz));
                Vector3 local = arma.InverseTransformPoint(r.transform.TransformPoint(esquina));
                if (primero) { b = new Bounds(local, Vector3.zero); primero = false; } else b.Encapsulate(local);
            }
        }
        if (primero) return;

        var z = new GameObject("ZonaAgarre");
        z.transform.SetParent(arma, false);
        var col = z.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = b.center;
        col.size = b.size + margen * 2f;
    }

    static GameObject CrearRotulo(Transform arma, string titulo, string detalle, Material fondo)
    {
        // La mesa es el hermano "Mesa_N" mas cercano en planta.
        Transform mesa = null; float mejor = float.MaxValue;
        foreach (Transform t in arma.parent)
        {
            if (!t.name.StartsWith("Mesa_")) continue;
            Vector3 d = t.position - arma.position; d.y = 0f;
            if (d.sqrMagnitude < mejor) { mejor = d.sqrMagnitude; mesa = t; }
        }
        if (mesa == null) return null;
        var viejo = mesa.Find("Rotulo");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

        var rot = new GameObject("Rotulo");
        rot.transform.SetParent(mesa, false);
        rot.transform.position = new Vector3(mesa.position.x, 1.55f, mesa.position.z);
        rot.AddComponent<RotuloMesa>();

        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

        var tt = Texto("Titulo", rot.transform, titulo, f, new Color(1f, 0.85f, 0.1f), 0.12f);
        var td = Texto("Detalle", rot.transform, detalle, f, new Color(0.95f, 0.95f, 0.95f), 0.065f);
        var bt = tt.GetComponent<MeshRenderer>().bounds; var bd = td.GetComponent<MeshRenderer>().bounds;
        float ancho = Mathf.Max(bt.size.x, bd.size.x) + 0.12f;
        float altoT = bt.size.y, altoD = bd.size.y;
        float alto = altoT + altoD + 0.16f;
        tt.transform.localPosition = new Vector3(0f, alto * 0.5f - 0.06f - altoT * 0.5f, 0f);
        td.transform.localPosition = new Vector3(0f, -alto * 0.5f + 0.06f + altoD * 0.5f, 0f);

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Fondo";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(rot.transform, false);
        q.transform.localPosition = new Vector3(0f, 0f, 0.01f); // detras del texto (el lector mira hacia +z del cartel)
        q.transform.localScale = new Vector3(ancho, alto, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = fondo;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return rot;
    }

    // TextMesh escalado para que la altura de la primera linea sea 'altura' metros (legible en VR).
    static GameObject Texto(string nombre, Transform padre, string texto, Font f, Color color, float altura)
    {
        var o = new GameObject(nombre);
        o.transform.SetParent(padre, false);
        var tm = o.AddComponent<TextMesh>();
        tm.font = f;
        tm.text = texto;
        tm.fontSize = 64;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        var mr = o.GetComponent<MeshRenderer>();
        mr.sharedMaterial = f != null ? f.material : null;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        // Altura de una linea = altura total / numero de lineas; se escala el objeto para llegar a 'altura' por linea.
        int lineas = texto.Split('\n').Length;
        float actual = mr.bounds.size.y / lineas;
        if (actual > 0.0001f) o.transform.localScale = Vector3.one * (altura / actual);
        return o;
    }

    static Material MatUnlit(string nombre, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Combate");
        string path = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Unlit/Color"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Unlit/Color");
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }
}
