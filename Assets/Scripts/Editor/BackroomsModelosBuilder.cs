using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sustituye los modelos simples por los de la Asset Store (uno por bloque, cada uno con su menu, idempotente):
//   Backrooms/Combate/Modelos 1 - Pistol 92
// Reglas: texturas a 1024 max con ASTC en Android, solo Base Color + Normal + Metallic/Smoothness, shader barato Built-in
// (Backrooms/ArmaLitTex), malla combinada en un solo draw call. No toca interactables, resalte, rotulos ni la logica de combate.
public static class BackroomsModelosBuilder
{
    const string DirMat = "Assets/Materiales/Combate";
    const string DirMalla = "Assets/Modelo 3D/Combate";
    const float AlturaMesa = 0.75f;

    // ====================================================================================================
    // BLOQUE 1: Pistol 92
    // ====================================================================================================

    const string DirPistola = "Assets/Scenes/Pistol 92/";   // el paquete quedo importado dentro de Assets/Scenes
    const float LargoPistola = 0.217f;                      // Beretta 92 real: ~21,7 cm

    [MenuItem("Backrooms/Combate/Modelos 1 - Pistol 92")]
    public static void Pistola92()
    {
        if (!EscenaOk()) return;
        var raiz = GameObject.Find("Armas");
        if (raiz == null) { Debug.LogError("[Modelos] No existe la raiz 'Armas'."); return; }

        // ----- texturas: 1024 max, ASTC en Android -----
        ConfigurarTextura(DirPistola + "Textures/Pistol Black Diffuse.png", 1024, TextureImporterFormat.ASTC_6x6, true);
        ConfigurarTextura(DirPistola + "Textures/Pistol Normal.png", 1024, TextureImporterFormat.ASTC_5x5, false);
        ConfigurarTextura(DirPistola + "Textures/Pistol Metallic.png", 1024, TextureImporterFormat.ASTC_6x6, false);   // dato, no color (proyecto en Linear)

        // ----- material -----
        var mat = MatTex("Mat_Pistol92",
            DirPistola + "Textures/Pistol Black Diffuse.png", DirPistola + "Textures/Pistol Normal.png", DirPistola + "Textures/Pistol Metallic.png",
            Color.white, 1f, 0.8f, 0.45f);

        // ----- malla combinada -----
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirPistola + "Prefabs/Pistol 92 Black.prefab");
        if (prefab == null) { Debug.LogError("[Modelos] No se encontro 'Pistol 92 Black.prefab' en " + DirPistola); return; }
        var info = CombinarPistola(prefab);
        Debug.Log("[Modelos] Pistol 92 combinada: " + info.malla.vertexCount + " vertices, " + info.malla.triangles.Length / 3 + " triangulos, tamano " + info.malla.bounds.size.ToString("F3") + " m");

        int n = 0;
        foreach (var pis in raiz.GetComponentsInChildren<Pistola>(true))
        {
            var t = pis.transform;
            foreach (var nombre in new[] { "Corredera", "Empunadura", "Canon", "Armazon", "Guardamonte", "Gatillo", "MiraDelantera", "MiraTrasera", "Modelo", "ColCorredera", "ColEmpunadura" })
                QuitarHijos(t, nombre);

            var modelo = new GameObject("Modelo");
            modelo.transform.SetParent(t, false);
            modelo.AddComponent<MeshFilter>().sharedMesh = info.malla;
            var mr = modelo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            SinSombras(mr);

            // Dos colliders (corredera y empunadura); el ancho del arma (eje x local) se unifica para que apoye estable de lado.
            float ancho = Mathf.Max(info.corredera.size.x, info.empunadura.size.x);
            var cs = new GameObject("ColCorredera"); cs.transform.SetParent(t, false);
            var bs = cs.AddComponent<BoxCollider>(); bs.center = info.corredera.center; bs.size = new Vector3(ancho, info.corredera.size.y, info.corredera.size.z);
            var ce = new GameObject("ColEmpunadura"); ce.transform.SetParent(t, false);
            var be = ce.AddComponent<BoxCollider>(); be.center = new Vector3(info.corredera.center.x, info.empunadura.center.y, info.empunadura.center.z); be.size = new Vector3(ancho, info.empunadura.size.y, info.empunadura.size.z);

            // Puntos de la logica de combate. El origen del arma es el centro de la empunadura.
            // El SDK coloca la zona de agarre del control 4 cm DELANTE de la palma: PuntoAgarre va 4 cm delante del centro de la empunadura,
            // asi la empunadura queda en la palma. Barril = +z, arriba = +y (el eje del "grip pose" de OpenXR es la direccion del canon).
            Poner(t, "PuntoAgarre", new Vector3(0f, 0f, 0.04f));
            Poner(t, "Boquilla", info.boca);
            var dst = t.Find("Destello");
            if (dst != null) { dst.localPosition = info.boca + new Vector3(0f, 0f, 0.012f); dst.localScale = Vector3.one * 0.045f; }

            // Contador de balas en el flanco +x de la corredera (la cara que queda hacia arriba con el arma tumbada).
            var bal = t.Find("Balas");
            if (bal != null)
            {
                bal.localPosition = new Vector3(info.corredera.max.x + 0.0007f, info.corredera.center.y, info.corredera.center.z - info.corredera.size.z * 0.12f);
                bal.localRotation = Quaternion.Euler(0f, -90f, 0f);
                bal.localScale = Vector3.one;
                var br = bal.GetComponent<MeshRenderer>();
                float h = br.bounds.size.y;
                if (h > 0.0001f) bal.localScale = Vector3.one * (0.011f / h);
            }

            // Tumbada de lado sobre su mesa (estable): cara +x hacia arriba, canon hacia el pasillo (-z mundo).
            var mesa = MesaDe(t);
            t.SetPositionAndRotation(new Vector3(mesa.position.x, 1f, mesa.position.z), Quaternion.Euler(0f, 180f, 90f));
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            var b = mr.bounds;
            float dy = (AlturaMesa + 0.0008f) - b.min.y;
            float dz = mesa.position.z - b.center.z;
            t.position += new Vector3(0f, dy, dz);
            Physics.SyncTransforms();

            BackroomsAgarreBuilder.AjustarZona(t, new Vector3(0.07f, 0.07f, 0.07f));
            EditorUtility.SetDirty(pis);
            n++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Modelos] Pistol 92 aplicada a " + n + " pistola(s).");
    }

    struct InfoPistola { public Mesh malla; public Bounds corredera, empunadura; public Vector3 boca; }

    // Une las 6 piezas del prefab en una malla (1 draw call), a escala real (LargoPistola), canon hacia +z, origen en el centro de la empunadura.
    static InfoPistola CombinarPistola(GameObject prefab)
    {
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Physics.SyncTransforms();

        Bounds total = new Bounds(); bool primero = true;
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>()) { if (primero) { total = r.bounds; primero = false; } else total.Encapsulate(r.bounds); }
        float escala = LargoPistola / total.size.x;                      // el modelo original mide ~0,74 m (x = largo, +x = canon, -y = empunadura)
        var empNodo = inst.transform.Find("Cube.002").GetComponent<MeshRenderer>();
        var slideNodo = inst.transform.Find("Cube.001").GetComponent<MeshRenderer>();
        Vector3 pivote = empNodo.bounds.center;                          // centro de la empunadura
        Quaternion rot = Quaternion.Euler(0f, -90f, 0f);                 // +x -> +z
        Matrix4x4 aFinal = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * escala) * Matrix4x4.Translate(-pivote);

        // Combinacion manual: Mesh.CombineMeshes con la misma malla repetida (una vez por submalla) pierde geometria.
        // Varias piezas traen submallas (todas con el mismo material), se anexan TODAS a un unico submesh.
        var m = CombinarManual(inst.GetComponentsInChildren<MeshFilter>(), aFinal, "Pistol92_Combinada");

        var info = new InfoPistola();
        info.malla = Guardar(m, "Pistol92_Combinada");
        info.corredera = Transformar(slideNodo.bounds, aFinal);
        info.empunadura = Transformar(empNodo.bounds, aFinal);
        info.boca = new Vector3(info.corredera.center.x, info.corredera.center.y, info.corredera.max.z);
        Object.DestroyImmediate(inst);
        return info;
    }

    // Une varias mallas (con sus submallas) en una sola de un unico submesh. 'aFinal' se aplica sobre la matriz local->mundo de cada pieza.
    static Mesh CombinarManual(MeshFilter[] filtros, Matrix4x4 aFinal, string nombre)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<Vector4>(); var uv = new List<Vector2>(); var tri = new List<int>();
        foreach (var mf in filtros)
        {
            var src = mf.sharedMesh;
            if (src == null) continue;
            Matrix4x4 mt = aFinal * mf.transform.localToWorldMatrix;
            Matrix4x4 mn = mt.inverse.transpose;                    // para normales
            bool invertido = mt.determinant < 0f;                  // escala negativa => hay que invertir el orden de los triangulos
            int baseIdx = v.Count;
            var sv = src.vertices; var sn = src.normals; var st = src.tangents; var su = src.uv;
            for (int i = 0; i < sv.Length; i++)
            {
                v.Add(mt.MultiplyPoint3x4(sv[i]));
                n.Add(sn.Length > i ? mn.MultiplyVector(sn[i]).normalized : Vector3.up);
                if (st.Length > i) { Vector3 td = mt.MultiplyVector(st[i]).normalized; t.Add(new Vector4(td.x, td.y, td.z, invertido ? -st[i].w : st[i].w)); }
                else t.Add(new Vector4(1, 0, 0, 1));
                uv.Add(su.Length > i ? su[i] : Vector2.zero);
            }
            for (int s = 0; s < src.subMeshCount; s++)
            {
                var idx = src.GetTriangles(s);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    if (invertido) { tri.Add(baseIdx + idx[i]); tri.Add(baseIdx + idx[i + 2]); tri.Add(baseIdx + idx[i + 1]); }
                    else { tri.Add(baseIdx + idx[i]); tri.Add(baseIdx + idx[i + 1]); tri.Add(baseIdx + idx[i + 2]); }
                }
            }
        }
        var m = new Mesh { name = nombre };
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetNormals(n); m.SetTangents(t); m.SetUVs(0, uv);
        m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        return m;
    }

    static Bounds Transformar(Bounds b, Matrix4x4 m)
    {
        var r = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
        foreach (var sx in new[] { -1f, 1f }) foreach (var sy in new[] { -1f, 1f }) foreach (var sz in new[] { -1f, 1f })
            r.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3(sx, sy, sz))));
        return r;
    }

    // ====================================================================================================
    // BLOQUE 2: Crowbar Free
    // ====================================================================================================

    const string DirBarra = "Assets/Crowbar/";
    const float LargoBarra = 0.70f;      // palanca real: 60-75 cm
    const float AgarreDesdeMango = 0.17f; // la mano agarra a 17 cm del extremo del mango

    [MenuItem("Backrooms/Combate/Modelos 2 - Crowbar")]
    public static void Barra()
    {
        if (!EscenaOk()) return;
        var raiz = GameObject.Find("Armas");
        if (raiz == null) { Debug.LogError("[Modelos] No existe la raiz 'Armas'."); return; }

        // La malla es una sola (256 triangulos) pero hay que leer sus vertices para fijar pivote y punta: se activa Read/Write.
        string fbx = DirBarra + "Mesh/Crowbar.fbx";
        var mi = AssetImporter.GetAtPath(fbx) as ModelImporter;
        if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }

        ConfigurarTextura(DirBarra + "Textures/T_Crowbar_Albedo.png", 1024, TextureImporterFormat.ASTC_6x6, true);
        ConfigurarTextura(DirBarra + "Textures/T_Crowbar_Normal.png", 1024, TextureImporterFormat.ASTC_5x5, false);
        ConfigurarTextura(DirBarra + "Textures/T_Crowbar_Metallic.png", 1024, TextureImporterFormat.ASTC_6x6, false);
        var mat = MatTex("Mat_Crowbar", DirBarra + "Textures/T_Crowbar_Albedo.png", DirBarra + "Textures/T_Crowbar_Normal.png", DirBarra + "Textures/T_Crowbar_Metallic.png",
            Color.white, 1f, 1f, 1f);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirBarra + "Prefab/Crowbar.prefab");
        var origMesh = prefab.GetComponent<MeshFilter>().sharedMesh;
        var verts = origMesh.vertices;
        var bnd = origMesh.bounds;
        float escala = LargoBarra / bnd.size.z;

        // El extremo del mango es +z (uña plana); el gancho grande esta en -z y curva hacia -y.
        float zMango = bnd.max.z;
        float zAgarre = zMango - AgarreDesdeMango / escala;
        // Eje del mango: promedio de y y x de los vertices cerca del punto de agarre.
        Vector3 suma = Vector3.zero; int cnt = 0;
        foreach (var v in verts) if (Mathf.Abs(v.z - zAgarre) < 0.03f) { suma += v; cnt++; }
        Vector3 pivote = cnt > 0 ? new Vector3(suma.x / cnt, suma.y / cnt, zAgarre) : new Vector3(bnd.center.x, bnd.center.y, zAgarre);
        // Punta del gancho: el vertice mas lejano en -z.
        Vector3 vPunta = verts[0]; foreach (var v in verts) if (v.z < vPunta.z) vPunta = v;

        // z' = -z (el gancho pasa a +z) e y' = -y (el gancho curva hacia arriba): giro de 180 grados sobre x.
        Quaternion rot = Quaternion.Euler(180f, 0f, 0f);
        Matrix4x4 aFinal = Matrix4x4.TRS(Vector3.zero, rot, Vector3.one * escala) * Matrix4x4.Translate(-pivote);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var malla = Guardar(CombinarManual(inst.GetComponentsInChildren<MeshFilter>(), aFinal, "Crowbar_Escalada"), "Crowbar_Escalada");
        Object.DestroyImmediate(inst);
        Vector3 punta = aFinal.MultiplyPoint3x4(vPunta);
        Vector3 finMango = aFinal.MultiplyPoint3x4(new Vector3(pivote.x, pivote.y, zMango));
        Debug.Log("[Modelos] Crowbar: " + malla.vertexCount + " vertices, " + malla.triangles.Length / 3 + " triangulos, tamano " + malla.bounds.size.ToString("F3") + " m, punta en " + punta.ToString("F3") + ", extremo del mango en " + finMango.ToString("F3"));

        int n = 0;
        foreach (var barra in raiz.GetComponentsInChildren<BarraMetal>(true))
        {
            var t = barra.transform;
            foreach (var nombre in new[] { "Cuerpo", "Empunadura", "Palanca", "Anillo", "Modelo" }) QuitarHijos(t, nombre);

            var modelo = new GameObject("Modelo");
            modelo.transform.SetParent(t, false);
            modelo.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = modelo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            SinSombras(mr);

            // Collider fisico simple: capsula a lo largo del mango (el gancho no lleva collider para que apoye estable).
            var cap = modelo.AddComponent<CapsuleCollider>();
            cap.direction = 2;
            cap.radius = 0.022f;
            float z0 = finMango.z, z1 = punta.z - 0.05f;
            cap.height = Mathf.Max(0.1f, z1 - z0);
            cap.center = new Vector3(0f, 0f, (z0 + z1) * 0.5f);

            // El SDK pone la zona de agarre del control 4 cm delante de la palma: PuntoAgarre va 4 cm delante del centro del agarre.
            Poner(t, "PuntoAgarre", new Vector3(0f, 0f, 0.04f));
            Poner(t, "BaseGolpe", new Vector3(0f, 0f, 0.12f));
            Poner(t, "PuntaGolpe", punta);

            // Tumbada sobre la mesa, gancho hacia arriba, apuntando hacia el pasillo (-z mundo).
            var mesa = MesaDe(t);
            t.SetPositionAndRotation(new Vector3(mesa.position.x, 1f, mesa.position.z), Quaternion.Euler(0f, 180f, 0f));
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            Physics.SyncTransforms();
            var b = mr.bounds;
            t.position += new Vector3(0f, (AlturaMesa + 0.0008f) - b.min.y, mesa.position.z - b.center.z);
            Physics.SyncTransforms();

            BackroomsAgarreBuilder.AjustarZona(t, new Vector3(0.10f, 0.10f, 0.06f));
            EditorUtility.SetDirty(barra);
            n++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Modelos] Crowbar aplicada a " + n + " barra(s).");
    }

    // ====================================================================================================
    // Utilidades comunes
    // ====================================================================================================

    static bool EscenaOk()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Modelos] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return false;
        }
        return true;
    }

    // Textura a 'max' px con ASTC en Android (y tambien tope en el Editor). Sin cambiar nada mas del importador.
    static void ConfigurarTextura(string ruta, int max, TextureImporterFormat formatoAndroid, bool srgb)
    {
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) { Debug.LogWarning("[Modelos] Sin importador de textura: " + ruta); return; }
        imp.maxTextureSize = max;
        imp.isReadable = false;
        if (imp.textureType == TextureImporterType.Default) imp.sRGBTexture = srgb;
        var a = imp.GetPlatformTextureSettings("Android");
        a.overridden = true;
        a.maxTextureSize = max;
        a.format = formatoAndroid;
        a.compressionQuality = 50;
        imp.SetPlatformTextureSettings(a);
        imp.SaveAndReimport();
    }

    static Material MatTex(string nombre, string baseColor, string normal, string metallic, Color tinte, float metalico, float suavidad, float fuerzaNormal)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Combate");
        string path = DirMat + "/" + nombre + ".mat";
        var sh = Shader.Find("Backrooms/ArmaLitTex");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor));
        m.SetTexture("_BumpMap", normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normal) : null);
        m.SetTexture("_MetallicGlossMap", metallic != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(metallic) : null);
        m.SetColor("_Color", tinte);
        m.SetFloat("_Metallic", metalico);
        m.SetFloat("_Glossiness", suavidad);
        m.SetFloat("_BumpScale", fuerzaNormal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh Guardar(Mesh m, string nombre)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Modelo 3D")) AssetDatabase.CreateFolder("Assets", "Modelo 3D");
        if (!AssetDatabase.IsValidFolder(DirMalla)) AssetDatabase.CreateFolder("Assets/Modelo 3D", "Combate");
        string ruta = DirMalla + "/" + nombre + ".asset";
        // Se recrea el asset (no CopySerialized): sobre un asset existente el Editor dejaba un buffer de GPU obsoleto y la malla se dibujaba incompleta.
        if (AssetDatabase.LoadAssetAtPath<Mesh>(ruta) != null) AssetDatabase.DeleteAsset(ruta);
        AssetDatabase.CreateAsset(m, ruta);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
    }

    static Transform MesaDe(Transform arma)
    {
        Transform mesa = null; float mejor = float.MaxValue;
        foreach (Transform t in arma.parent)
        {
            if (!t.name.StartsWith("Mesa_")) continue;
            Vector3 d = t.position - arma.position; d.y = 0f;
            if (d.sqrMagnitude < mejor) { mejor = d.sqrMagnitude; mesa = t; }
        }
        return mesa;
    }

    static void QuitarHijos(Transform padre, string nombre)
    {
        for (int i = padre.childCount - 1; i >= 0; i--)
            if (padre.GetChild(i).name == nombre) Object.DestroyImmediate(padre.GetChild(i).gameObject);
    }

    static Transform Poner(Transform padre, string nombre, Vector3 local)
    {
        var t = padre.Find(nombre);
        if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, false); }
        t.localPosition = local;
        t.localRotation = Quaternion.identity;
        return t;
    }

    static void SinSombras(Renderer r)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}
