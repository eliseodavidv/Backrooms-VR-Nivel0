using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Combate de Backrooms_Main. Cada bloque es un menu idempotente (borra y reconstruye lo suyo):
//   Backrooms/Combate/Bloque 1 - Armas y mesas
// Solo toca la escena activa si es Backrooms_Main; las armas y mesas cuelgan de una raiz propia ("Armas")
// fuera de "Laberinto" para que un rebake del NavMesh (NavMeshSurface recoge los hijos de Laberinto) no las incluya.
public static class BackroomsCombateBuilder
{
    const string DirMat = "Assets/Materiales/Combate";
    const string RaizArmas = "Armas";

    // Mesas: celda (x,y) del laberinto, lado de la pared contra la que se apoya (-1 = oeste, +1 = este).
    // Celda (x,y) esta en (3x, 0, -3y). Se eligen celdas con pared en ese lado y lejos del spawn (0,0).
    struct Mesa { public int x, y, lado; public string arma; public Mesa(int x, int y, int lado, string arma) { this.x = x; this.y = y; this.lado = lado; this.arma = arma; } }
    static readonly Mesa[] Mesas =
    {
        new Mesa(0, 1, -1, "Barra"),   // pared O del pasillo del spawn; a 3 m en linea recta (spawn mira al sur)
        new Mesa(0, 2, -1, "Pistola"), // pared O; a 6 m, misma linea de vista
        new Mesa(2, 10, +1, "Pistola"),// pared E; zona sur-oeste (premio por explorar)
    };

    [MenuItem("Backrooms/Combate/Bloque 1 - Armas y mesas")]
    public static void Bloque1()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Combate] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return;
        }

        var viejo = GameObject.Find(RaizArmas);
        if (viejo != null) Object.DestroyImmediate(viejo);
        var raiz = new GameObject(RaizArmas).transform;

        var matMesa = Mat("Mat_Mesa", new Color(0.36f, 0.27f, 0.17f));
        var matMetal = Mat("Mat_Metal", new Color(0.55f, 0.57f, 0.60f));
        var matMango = Mat("Mat_Mango", new Color(0.12f, 0.12f, 0.13f));
        var matPistola = Mat("Mat_Pistola", new Color(0.20f, 0.21f, 0.24f));

        for (int i = 0; i < Mesas.Length; i++)
        {
            var m = Mesas[i];
            Vector3 centro = new Vector3(m.x * 3f, 0f, -m.y * 3f);
            // Tablero de 0,5 (profundidad, eje x) x 0,9; a 1,0 m del centro -> ocupa 0,75..1,25 y la pared interior esta a ~1,4.
            // Columna x=0: pegada al muro (borde del tablero en el limite del NavMesh); resto: 1,0 m del centro.
            Vector3 pos = centro + new Vector3((m.x == 0 ? 1.13f : 1.0f) * m.lado, 0f, 0f);
            var mesa = CrearMesa("Mesa_" + (i + 1), raiz, pos, matMesa);

            Vector3 sobre = pos + new Vector3(0f, 0.80f, 0f);
            if (m.arma == "Barra") CrearBarra("Arma_Barra", raiz, sobre, matMetal, matMango);
            else CrearPistola("Arma_Pistola_" + (i + 1), raiz, sobre, matPistola, matMango);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Combate] Bloque 1 construido: " + Mesas.Length + " mesas con armas bajo '" + RaizArmas + "'.");
    }

    // Bloque 2: convierte las armas del bloque 1 en BarraMetal / Pistola (sonido, vibracion, trazador, municion).
    // Idempotente: se puede relanzar sin duplicar nada.
    [MenuItem("Backrooms/Combate/Bloque 2 - Combate")]
    public static void Bloque2()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Combate] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return;
        }
        var raiz = GameObject.Find(RaizArmas);
        if (raiz == null) { Debug.LogError("[Combate] Falta el bloque 1 (raiz 'Armas')."); return; }

        var matTrazador = MatUnlit("Mat_Trazador", new Color(1f, 0.85f, 0.35f));
        var matDestello = MatUnlit("Mat_Destello", new Color(1f, 0.95f, 0.7f));
        var matTexto = MatUnlit("Mat_TextoBalas", new Color(0.9f, 0.3f, 0.2f));

        int barras = 0, pistolas = 0;
        foreach (Transform t in raiz.transform)
        {
            var go = t.gameObject;
            var baseArma = go.GetComponent<ArmaAgarrable>();
            if (baseArma == null) continue;
            var gi = baseArma.interactable;

            if (go.name.StartsWith("Arma_Barra"))
            {
                if (go.GetComponent<BarraMetal>() == null)
                {
                    Object.DestroyImmediate(baseArma);
                    var b = go.AddComponent<BarraMetal>();
                    b.interactable = gi;
                }
                var barra = go.GetComponent<BarraMetal>();
                barra.baseGolpe = HijoVacio(go.transform, "BaseGolpe", new Vector3(0f, 0f, 0.05f));
                barra.puntaGolpe = HijoVacio(go.transform, "PuntaGolpe", new Vector3(0f, 0f, 0.80f));
                barra.audioFuente = AudioFuente(go);
                EditorUtility.SetDirty(barra);
                barras++;
            }
            else if (go.name.StartsWith("Arma_Pistola"))
            {
                if (go.GetComponent<Pistola>() == null)
                {
                    Object.DestroyImmediate(baseArma);
                    var p = go.AddComponent<Pistola>();
                    p.interactable = gi;
                }
                var pis = go.GetComponent<Pistola>();
                pis.boquilla = go.transform.Find("Boquilla");
                pis.audioFuente = AudioFuente(go);

                // Trazador (linea de 1 cm que se enciende 0,06 s).
                var trz = go.transform.Find("Trazador");
                if (trz == null) { trz = new GameObject("Trazador").transform; trz.SetParent(go.transform, false); }
                var lr = trz.GetComponent<LineRenderer>();
                if (lr == null) lr = trz.gameObject.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.widthMultiplier = 0.012f;
                lr.sharedMaterial = matTrazador;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.enabled = false;
                pis.trazador = lr;

                // Destello en la boquilla.
                var dst = go.transform.Find("Destello");
                if (dst == null)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.name = "Destello";
                    Object.DestroyImmediate(s.GetComponent<Collider>());
                    s.transform.SetParent(go.transform, false);
                    dst = s.transform;
                }
                dst.localPosition = new Vector3(0f, 0.05f, 0.20f);
                dst.localScale = Vector3.one * 0.06f;
                var dr = dst.GetComponent<MeshRenderer>();
                dr.sharedMaterial = matDestello;
                dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dr.receiveShadows = false;
                dst.gameObject.SetActive(false);
                pis.destello = dst.gameObject;

                // Balas infinitas: no hay contador. Si una version anterior dejo el cartel "Balas", se quita.
                var viejoContador = go.transform.Find("Balas");
                if (viejoContador != null) Object.DestroyImmediate(viejoContador.gameObject);

                EditorUtility.SetDirty(pis);
                pistolas++;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Combate] Bloque 2 aplicado: " + barras + " barra(s), " + pistolas + " pistola(s).");
    }

    // Bloque 3: prefab MiniMonstruo (Assets/Prefabs/MiniMonstruo.prefab), 5 instancias lejos del inicio,
    // SaludJugador y el destello rojo de danyo pegado a la camara. Idempotente.
    // Celdas (x,y) -> (3x, 0, -3y). El spawn del jugador es (0,0): todas quedan a >= 24 m de el.
    static readonly int[][] CeldasMonstruos =
    {
        new[] { 8, 4 },   // (24, -12)  ~27 m del inicio
        new[] { 12, 8 },  // (36, -24)  ~43 m
        new[] { 4, 12 },  // (12, -36)  ~38 m
        new[] { 2, 14 },  // (6, -42)   ~42 m, esquina sur-oeste
        new[] { 10, 12 }, // (30, -36)  ~47 m
    };

    [MenuItem("Backrooms/Combate/Bloque 3 - Mini monstruos")]
    public static void Bloque3()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Combate] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return;
        }

        var matCuerpo = MatUnlit("Mat_Monstruo", new Color(0.38f, 0.10f, 0.48f));
        var matPatas = MatUnlit("Mat_MonstruoPatas", new Color(0.18f, 0.05f, 0.24f));
        var matOjos = MatUnlit("Mat_MonstruoOjos", new Color(1f, 0.15f, 0.1f));

        // ----- prefab -----
        var molde = new GameObject("MiniMonstruo");
        var cuerpo = Caja("Cuerpo", molde.transform, new Vector3(0f, 0.21f, 0f), new Vector3(0.30f, 0.26f, 0.40f), matCuerpo, false);
        var ojoI = Caja("OjoI", molde.transform, new Vector3(-0.08f, 0.27f, 0.20f), Vector3.one * 0.07f, matOjos, false);
        var ojoD = Caja("OjoD", molde.transform, new Vector3(0.08f, 0.27f, 0.20f), Vector3.one * 0.07f, matOjos, false);
        var extras = new System.Collections.Generic.List<Renderer> { ojoI.GetComponent<Renderer>(), ojoD.GetComponent<Renderer>() };
        foreach (var dx in new[] { -0.10f, 0.10f })
            foreach (var dz in new[] { -0.12f, 0.12f })
                extras.Add(Caja("Pata", molde.transform, new Vector3(dx, 0.05f, dz), new Vector3(0.06f, 0.10f, 0.06f), matPatas, false).GetComponent<Renderer>());

        var cap = molde.AddComponent<CapsuleCollider>();
        cap.radius = 0.2f;
        cap.height = 0.5f;
        cap.center = new Vector3(0f, 0.25f, 0f);

        var rb = molde.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Agente pequeno sobre el NavMesh ya horneado (no hace falta rehornear: un agente menor cabe donde cabe el normal).
        var ag = molde.AddComponent<UnityEngine.AI.NavMeshAgent>();
        ag.radius = 0.2f;
        ag.height = 0.5f;
        ag.baseOffset = 0f;
        ag.speed = 1.8f;
        ag.acceleration = 25f;
        ag.angularSpeed = 540f;
        ag.stoppingDistance = 0f;
        ag.autoBraking = false;
        ag.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        ag.avoidancePriority = 60;

        var fuente = molde.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 1f;
        fuente.minDistance = 1f;
        fuente.maxDistance = 20f;

        var mm = molde.AddComponent<MiniMonstruo>();
        mm.cuerpo = cuerpo.GetComponent<Renderer>();
        mm.extras = extras.ToArray();
        mm.colision = cap;
        mm.audioFuente = fuente;

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        const string rutaPrefab = "Assets/Prefabs/MiniMonstruo.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(molde, rutaPrefab);
        Object.DestroyImmediate(molde);

        // ----- instancias -----
        var viejo = GameObject.Find("MiniMonstruos");
        if (viejo != null) Object.DestroyImmediate(viejo);
        var raiz = new GameObject("MiniMonstruos").transform;
        for (int i = 0; i < CeldasMonstruos.Length; i++)
        {
            Vector3 centro = new Vector3(CeldasMonstruos[i][0] * 3f, 0f, -CeldasMonstruos[i][1] * 3f);
            UnityEngine.AI.NavMeshHit h;
            if (UnityEngine.AI.NavMesh.SamplePosition(centro, out h, 2f, UnityEngine.AI.NavMesh.AllAreas)) centro = h.position;
            else Debug.LogWarning("[Combate] Celda " + CeldasMonstruos[i][0] + "," + CeldasMonstruos[i][1] + " sin NavMesh cerca; el monstruo se ajustara al iniciar.");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz);
            inst.name = "MiniMonstruo_" + (i + 1);
            inst.transform.position = centro;
            inst.transform.rotation = Quaternion.Euler(0f, i * 72f, 0f);
        }

        // ----- salud del jugador + destello de danyo -----
        var cam = Object.FindObjectOfType<OVRCameraRig>();
        var cabeza = cam.centerEyeAnchor;
        var matRojo = MatOverlay("Mat_DestelloDanio", new Color(0.8f, 0.05f, 0.05f, 0f));
        var fundido = cabeza.Find("FundidoNegro");
        var dd = cabeza.Find("DestelloDanio");
        if (dd == null)
        {
            var q = new GameObject("DestelloDanio");
            q.transform.SetParent(cabeza, false);
            q.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            q.AddComponent<MeshRenderer>();
            dd = q.transform;
        }
        if (fundido != null) { dd.localPosition = fundido.localPosition; dd.localRotation = fundido.localRotation; dd.localScale = fundido.localScale; }
        else { dd.localPosition = new Vector3(0f, 0f, 0.12f); dd.localScale = new Vector3(0.9f, 0.9f, 1f); }
        var ddr = dd.GetComponent<MeshRenderer>();
        ddr.sharedMaterial = matRojo;
        ddr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ddr.receiveShadows = false;
        ddr.enabled = false;

        var salud = Object.FindObjectOfType<SaludJugador>();
        if (salud == null) salud = new GameObject("SaludJugador").AddComponent<SaludJugador>();
        salud.destelloDanio = ddr;
        EditorUtility.SetDirty(salud);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Combate] Bloque 3 construido: " + CeldasMonstruos.Length + " mini monstruos bajo 'MiniMonstruos' + SaludJugador.");
    }

    static Material MatOverlay(string nombre, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Combate");
        string path = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Backrooms/OverlayTop"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Backrooms/OverlayTop");
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- piezas ----------

    static GameObject CrearMesa(string nombre, Transform padre, Vector3 pos, Material mat)
    {
        var mesa = new GameObject(nombre);
        mesa.transform.SetParent(padre, false);
        mesa.transform.position = pos;

        // Tablero 0,5 x 0,9 a 0,75 m de altura. Solo el tablero tiene collider (las patas son decoracion) y es bajo/pequeno:
        // en una celda de 3 m deja >2 m de paso libre.
        var tablero = Caja("Tablero", mesa.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.05f, 0.9f), mat, true);
        foreach (var dx in new[] { -0.2f, 0.2f })
            foreach (var dz in new[] { -0.4f, 0.4f })
                Caja("Pata", mesa.transform, new Vector3(dx, 0.36f, dz), new Vector3(0.05f, 0.72f, 0.05f), mat, false);
        return mesa;
    }

    static GameObject CrearBarra(string nombre, Transform padre, Vector3 pos, Material metal, Material mango)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        // Tumbada sobre la mesa, a lo largo del eje z.
        go.transform.SetPositionAndRotation(pos + new Vector3(0f, 0.03f, 0f), Quaternion.identity);

        // Barra de 0,9 m: el eje local z es el largo; el origen del objeto esta en el mango (extremo trasero).
        var cuerpo = Cilindro("Cuerpo", go.transform, new Vector3(0f, 0f, 0.40f), new Vector3(0.035f, 0.40f, 0.035f), metal);
        cuerpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var empunadura = Cilindro("Empunadura", go.transform, new Vector3(0f, 0f, 0.08f), new Vector3(0.042f, 0.09f, 0.042f), mango);
        empunadura.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Punto de agarre: la mano se alinea con este transform (origen en el mango, z hacia la punta).
        var agarre = new GameObject("PuntoAgarre").transform;
        agarre.SetParent(go.transform, false);
        agarre.localPosition = new Vector3(0f, 0f, 0.08f);

        HacerAgarrable(go, agarre, 0.9f);
        return go;
    }

    static GameObject CrearPistola(string nombre, Transform padre, Vector3 pos, Material cuerpoMat, Material mango)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.SetPositionAndRotation(pos + new Vector3(0f, 0.06f, 0f), Quaternion.identity);

        // Origen en la empunadura; z hacia el canon.
        Caja("Corredera", go.transform, new Vector3(0f, 0.05f, 0.07f), new Vector3(0.035f, 0.045f, 0.20f), cuerpoMat, true);
        var empunadura = Caja("Empunadura", go.transform, new Vector3(0f, -0.02f, -0.005f), new Vector3(0.032f, 0.10f, 0.05f), mango, true);
        empunadura.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

        var boquilla = new GameObject("Boquilla").transform;
        boquilla.SetParent(go.transform, false);
        boquilla.localPosition = new Vector3(0f, 0.05f, 0.18f);

        var agarre = new GameObject("PuntoAgarre").transform;
        agarre.SetParent(go.transform, false);
        agarre.localPosition = new Vector3(0f, 0f, 0f);

        HacerAgarrable(go, agarre, 0.6f);
        return go;
    }

    // Rigidbody + Grabbable + GrabInteractable (agarre con controladores). Se asignan los campos privados serializados.
    static void HacerAgarrable(GameObject go, Transform puntoAgarre, float masa)
    {
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = masa;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var grabbable = go.AddComponent<Grabbable>();
        var so = new SerializedObject(grabbable);
        so.FindProperty("_rigidbody").objectReferenceValue = rb;
        so.FindProperty("_kinematicWhileSelected").boolValue = true;
        so.FindProperty("_throwWhenUnselected").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        var gi = go.AddComponent<GrabInteractable>();
        var sg = new SerializedObject(gi);
        sg.FindProperty("_rigidbody").objectReferenceValue = rb;
        sg.FindProperty("_grabSource").objectReferenceValue = puntoAgarre;
        sg.FindProperty("_pointableElement").objectReferenceValue = grabbable;
        sg.ApplyModifiedPropertiesWithoutUndo();

        var arma = go.AddComponent<ArmaAgarrable>();
        arma.interactable = gi;
    }

    // ---------- utilidades ----------

    static GameObject Caja(string nombre, Transform padre, Vector3 local, Vector3 escala, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nombre;
        g.transform.SetParent(padre, false);
        g.transform.localPosition = local;
        g.transform.localScale = escala;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        SinSombras(g);
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static GameObject Cilindro(string nombre, Transform padre, Vector3 local, Vector3 escala, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = nombre;
        g.transform.SetParent(padre, false);
        g.transform.localPosition = local;
        g.transform.localScale = escala;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        SinSombras(g);
        // Un solo collider capsula basta; el del cilindro primitivo ya lo es.
        return g;
    }

    static void SinSombras(GameObject g)
    {
        var r = g.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static Transform HijoVacio(Transform padre, string nombre, Vector3 local)
    {
        var t = padre.Find(nombre);
        if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, false); }
        t.localPosition = local;
        t.localRotation = Quaternion.identity;
        return t;
    }

    static AudioSource AudioFuente(GameObject go)
    {
        var a = go.GetComponent<AudioSource>();
        if (a == null) a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = false;
        a.spatialBlend = 1f;
        a.minDistance = 1f;
        a.maxDistance = 25f;
        return a;
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

    static Material Mat(string nombre, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Combate");
        string path = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Mobile/Diffuse"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = Shader.Find("Mobile/Diffuse");
        m.color = color;
        EditorUtility.SetDirty(m);
        return m;
    }
}
