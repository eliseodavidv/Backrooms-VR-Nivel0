using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Rehace SOLO el aspecto de mesas y armas (idempotente). No toca interactables, rotulos ni la logica de combate:
//  - Mesa: tablero a 0,75 m con 4 patas y travesanos (madera), footprint 0,5 x 0,9 como antes.
//  - Barra: palanca de hierro de ~75 cm (malla procedural: tubo octogonal con gancho curvo y garra aplanada) y 2 anillos de agarre de goma.
//  - Pistola: ~22 cm con corredera, canon, armazon, empunadura, guardamonte, gatillo y miras.
// Shader Backrooms/ArmaLit: barato (1 pass) y legible aunque la escena no tenga luz para objetos dinamicos.
// Menu: Backrooms/Combate/Aspecto (armas y mesas)
public static class BackroomsAspectoBuilder
{
    const string DirMat = "Assets/Materiales/Combate";
    const string DirMalla = "Assets/Modelo 3D/Combate";
    const float AlturaMesa = 0.75f;

    [MenuItem("Backrooms/Combate/Aspecto (armas y mesas)")]
    public static void Aplicar()
    {
        if (EditorSceneManager.GetActiveScene().name != "Backrooms_Main")
        {
            Debug.LogError("[Aspecto] Abre la escena Backrooms_Main (no se toca ninguna otra).");
            return;
        }
        var raiz = GameObject.Find("Armas");
        if (raiz == null) { Debug.LogError("[Aspecto] No existe la raiz 'Armas'."); return; }

        var hierro = Mat("Mat_Arma_Hierro", new Color(0.22f, 0.22f, 0.25f), 0.55f, 40f);
        var goma = Mat("Mat_Arma_Goma", new Color(0.05f, 0.05f, 0.055f), 0.05f, 8f);
        var corredera = Mat("Mat_Arma_Corredera", new Color(0.30f, 0.31f, 0.35f), 0.75f, 60f);
        var plastico = Mat("Mat_Arma_Plastico", new Color(0.07f, 0.07f, 0.075f), 0.18f, 16f);
        var tablero = Mat("Mat_Mesa_Tablero", new Color(0.66f, 0.47f, 0.27f), 0.06f, 12f);
        var patas = Mat("Mat_Mesa_Patas", new Color(0.44f, 0.29f, 0.16f), 0.04f, 10f);

        // ----- mesas -----
        foreach (Transform t in raiz.transform)
            if (t.name.StartsWith("Mesa_")) ReconstruirMesa(t, tablero, patas);

        // ----- armas -----
        int barras = 0, pistolas = 0;
        foreach (var arma in raiz.GetComponentsInChildren<ArmaAgarrable>(true))
        {
            Transform mesa = MesaDe(arma.transform);
            if (arma is BarraMetal) { ReconstruirBarra(arma.transform, hierro, goma); Colocar(arma.transform, mesa, AlturaMesa + 0.0145f, 0.375f, Quaternion.Euler(0f, 180f, 0f)); barras++; }
            else if (arma is Pistola) { ReconstruirPistola(arma.transform, corredera, plastico); Colocar(arma.transform, mesa, AlturaMesa + 0.0165f, 0.10f, Quaternion.Euler(0f, 180f, 90f)); pistolas++; }
            // La zona de agarre se recalcula con la geometria nueva.
            BackroomsAgarreBuilder.AjustarZona(arma.transform, arma is BarraMetal ? new Vector3(0.10f, 0.10f, 0.06f) : new Vector3(0.09f, 0.09f, 0.09f));
            EditorUtility.SetDirty(arma);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Aspecto] Listo: " + raiz.transform.childCount + " hijos en Armas; " + barras + " barra(s), " + pistolas + " pistola(s).");
    }

    // ---------- mesa ----------

    static void ReconstruirMesa(Transform mesa, Material top, Material pata)
    {
        for (int i = mesa.childCount - 1; i >= 0; i--)
        {
            var h = mesa.GetChild(i);
            if (h.name == "Tablero" || h.name == "Pata" || h.name == "Travesano" || h.name == "Tapete") Object.DestroyImmediate(h.gameObject);
        }
        // Tapete claro (4 mm) bajo el arma: contraste para que el metal oscuro se lea desde lejos. Sin collider.
        Caja("Tapete", mesa, new Vector3(0f, AlturaMesa + 0.002f, 0f), new Vector3(0.32f, 0.004f, 0.80f), Mat("Mat_Mesa_Tapete", new Color(0.86f, 0.82f, 0.66f), 0.02f, 8f), false);
        // Tablero: 0,5 (x) x 0,9 (z) x 0,04; cara superior a 0,75 m. Unico collider de la mesa.
        Caja("Tablero", mesa, new Vector3(0f, AlturaMesa - 0.02f, 0f), new Vector3(0.5f, 0.04f, 0.9f), top, true);
        foreach (var dx in new[] { -0.21f, 0.21f })
            foreach (var dz in new[] { -0.41f, 0.41f })
                Caja("Pata", mesa, new Vector3(dx, (AlturaMesa - 0.04f) * 0.5f, dz), new Vector3(0.055f, AlturaMesa - 0.04f, 0.055f), pata, false);
        // Travesanos bajo el tablero (dan aspecto de mesa y no cuestan casi nada).
        foreach (var dx in new[] { -0.21f, 0.21f })
            Caja("Travesano", mesa, new Vector3(dx, AlturaMesa - 0.07f, 0f), new Vector3(0.025f, 0.06f, 0.78f), pata, false);
        foreach (var dz in new[] { -0.41f, 0.41f })
            Caja("Travesano", mesa, new Vector3(0f, AlturaMesa - 0.07f, dz), new Vector3(0.37f, 0.06f, 0.025f), pata, false);
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

    // Deja el arma apoyada sobre la mesa: y de su origen, desplazamiento en z respecto al centro de la mesa y orientacion.
    static void Colocar(Transform arma, Transform mesa, float y, float dz, Quaternion rot)
    {
        arma.SetPositionAndRotation(new Vector3(mesa.position.x, y, mesa.position.z + dz), rot);
        var rb = arma.GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    // ---------- barra / palanca ----------

    static void ReconstruirBarra(Transform arma, Material hierro, Material goma)
    {
        Limpiar(arma, "Cuerpo", "Empunadura", "Palanca", "Anillo");
        var go = new GameObject("Palanca");
        go.transform.SetParent(arma, false);
        go.AddComponent<MeshFilter>().sharedMesh = MallaPalanca();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = hierro;
        SinSombras(mr);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.direction = 2;
        cap.radius = 0.0165f;
        cap.height = 0.70f;
        cap.center = new Vector3(0f, 0f, 0.35f);

        // Dos anillos de agarre de goma en el tercio del mango.
        foreach (var z in new[] { 0.06f, 0.20f })
        {
            var a = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            a.name = "Anillo";
            Object.DestroyImmediate(a.GetComponent<Collider>());
            a.transform.SetParent(arma, false);
            a.transform.localPosition = new Vector3(0f, 0f, z);
            a.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            a.transform.localScale = new Vector3(0.034f, 0.0125f, 0.034f);
            var r = a.GetComponent<MeshRenderer>();
            r.sharedMaterial = goma;
            SinSombras(r);
        }

        Poner(arma, "PuntoAgarre", new Vector3(0f, 0f, 0.13f));
        Poner(arma, "BaseGolpe", new Vector3(0f, 0f, 0.05f));
        Poner(arma, "PuntaGolpe", new Vector3(0f, 0.04f, 0.72f));
    }

    // Tubo octogonal de ~75 cm: mango recto, curva de 75 grados con radio 7 cm y garra que se ensancha y aplana.
    // Origen en el extremo del mango; el gancho curva hacia +y.
    static Mesh MallaPalanca()
    {
        const int seg = 8;
        var anillos = new List<(Vector3 pos, Vector3 n, float rx, float ry)>();
        foreach (var z in new[] { 0f, 0.18f, 0.45f, 0.66f })
            anillos.Add((new Vector3(0f, 0f, z), Vector3.up, 0.0125f, 0.0125f));
        const float R = 0.07f, z0 = 0.66f, maxA = 75f;
        for (float a = 15f; a <= maxA + 0.01f; a += 15f)
        {
            float th = a * Mathf.Deg2Rad, k = a / maxA;
            anillos.Add((new Vector3(0f, R * (1f - Mathf.Cos(th)), z0 + R * Mathf.Sin(th)),
                         new Vector3(0f, Mathf.Cos(th), -Mathf.Sin(th)),
                         Mathf.Lerp(0.0125f, 0.021f, k), Mathf.Lerp(0.0125f, 0.0055f, k)));
        }

        var v = new List<Vector3>();
        foreach (var an in anillos)
            for (int s = 0; s < seg; s++)
            {
                float f = 2f * Mathf.PI * s / seg;
                v.Add(an.pos + Vector3.right * Mathf.Cos(f) * an.rx + an.n * Mathf.Sin(f) * an.ry);
            }
        var tri = new List<int>();
        for (int i = 0; i < anillos.Count - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * seg + s, b = i * seg + (s + 1) % seg, c = (i + 1) * seg + s, d = (i + 1) * seg + (s + 1) % seg;
                tri.AddRange(new[] { a, b, c, b, d, c });
            }
        // Tapas
        int c0 = v.Count; v.Add(anillos[0].pos);
        for (int s = 0; s < seg; s++) tri.AddRange(new[] { c0, (s + 1) % seg, s });
        int last = (anillos.Count - 1) * seg;
        int c1 = v.Count; v.Add(anillos[anillos.Count - 1].pos);
        for (int s = 0; s < seg; s++) tri.AddRange(new[] { c1, last + s, last + (s + 1) % seg });

        var m = new Mesh { name = "Palanca" };
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();

        if (!AssetDatabase.IsValidFolder("Assets/Modelo 3D")) AssetDatabase.CreateFolder("Assets", "Modelo 3D");
        if (!AssetDatabase.IsValidFolder(DirMalla)) AssetDatabase.CreateFolder("Assets/Modelo 3D", "Combate");
        string ruta = DirMalla + "/Palanca.asset";
        var existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
        if (existente != null) { EditorUtility.CopySerialized(m, existente); Object.DestroyImmediate(m); EditorUtility.SetDirty(existente); return existente; }
        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // ---------- pistola ----------

    static void ReconstruirPistola(Transform arma, Material metal, Material plastico)
    {
        Limpiar(arma, "Corredera", "Empunadura", "Canon", "Armazon", "Guardamonte", "Gatillo", "MiraDelantera", "MiraTrasera");
        // Origen = parte superior trasera de la empunadura; z hacia el canon; ~22 cm de largo.
        Caja("Corredera", arma, new Vector3(0f, 0.045f, 0.085f), new Vector3(0.028f, 0.030f, 0.185f), metal, true);
        var canon = Cil("Canon", arma, new Vector3(0f, 0.045f, 0.195f), 0.013f, 0.035f, metal);
        canon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Caja("Armazon", arma, new Vector3(0f, 0.019f, 0.07f), new Vector3(0.024f, 0.022f, 0.14f), plastico, false);
        var emp = Caja("Empunadura", arma, new Vector3(0f, -0.045f, 0.0f), new Vector3(0.030f, 0.105f, 0.045f), plastico, true);
        emp.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
        Caja("Guardamonte", arma, new Vector3(0f, -0.020f, 0.055f), new Vector3(0.012f, 0.008f, 0.055f), plastico, false);
        Caja("Guardamonte", arma, new Vector3(0f, -0.007f, 0.082f), new Vector3(0.012f, 0.030f, 0.008f), plastico, false);
        Caja("Gatillo", arma, new Vector3(0f, -0.003f, 0.045f), new Vector3(0.007f, 0.024f, 0.008f), metal, false);
        Caja("MiraDelantera", arma, new Vector3(0f, 0.064f, 0.17f), new Vector3(0.005f, 0.008f, 0.008f), metal, false);
        Caja("MiraTrasera", arma, new Vector3(0f, 0.064f, 0.0f), new Vector3(0.014f, 0.008f, 0.008f), metal, false);

        Poner(arma, "PuntoAgarre", new Vector3(0f, -0.045f, 0f));
        Poner(arma, "Boquilla", new Vector3(0f, 0.045f, 0.214f));
        var dst = arma.Find("Destello");
        if (dst != null) { dst.localPosition = new Vector3(0f, 0.045f, 0.23f); dst.localScale = Vector3.one * 0.05f; }

        // Contador de balas en el costado de la corredera (cara que queda hacia arriba con el arma tumbada).
        var bal = arma.Find("Balas");
        if (bal != null)
        {
            bal.localPosition = new Vector3(0.0145f, 0.046f, 0.09f);
            bal.localRotation = Quaternion.Euler(0f, -90f, 0f);
            bal.localScale = Vector3.one;
            var mr = bal.GetComponent<MeshRenderer>();
            float h = mr.bounds.size.y;
            if (h > 0.0001f) bal.localScale = Vector3.one * (0.014f / h);
        }
    }

    // ---------- utilidades ----------

    static void Limpiar(Transform padre, params string[] nombres)
    {
        for (int i = padre.childCount - 1; i >= 0; i--)
        {
            var h = padre.GetChild(i);
            foreach (var n in nombres)
                if (h.name == n) { Object.DestroyImmediate(h.gameObject); break; }
        }
    }

    static Transform Poner(Transform padre, string nombre, Vector3 local)
    {
        var t = padre.Find(nombre);
        if (t == null) { t = new GameObject(nombre).transform; t.SetParent(padre, false); }
        t.localPosition = local;
        t.localRotation = Quaternion.identity;
        return t;
    }

    static GameObject Caja(string nombre, Transform padre, Vector3 local, Vector3 escala, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = nombre;
        g.transform.SetParent(padre, false);
        g.transform.localPosition = local;
        g.transform.localScale = escala;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        SinSombras(r);
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    // Cilindro de 'diametro' y 'largo' (en metros) a lo largo de su eje Y local, sin collider.
    static GameObject Cil(string nombre, Transform padre, Vector3 local, float diametro, float largo, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = nombre;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(padre, false);
        g.transform.localPosition = local;
        g.transform.localScale = new Vector3(diametro, largo * 0.5f, diametro);
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        SinSombras(r);
        return g;
    }

    static void SinSombras(Renderer r)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static Material Mat(string nombre, Color color, float especular, float dureza)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materiales")) AssetDatabase.CreateFolder("Assets", "Materiales");
        if (!AssetDatabase.IsValidFolder(DirMat)) AssetDatabase.CreateFolder("Assets/Materiales", "Combate");
        string path = DirMat + "/" + nombre + ".mat";
        var sh = Shader.Find("Backrooms/ArmaLit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetColor("_Color", color);
        m.SetColor("_SpecColor", new Color(especular, especular, especular, 1f));
        m.SetFloat("_Shininess", dureza);
        EditorUtility.SetDirty(m);
        return m;
    }
}
