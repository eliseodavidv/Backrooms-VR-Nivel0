using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

// Genera el laberinto de Backrooms (E2) a partir del mapa de la figura 4b del informe.
// Menu: Backrooms/Construir laberinto E2. Es idempotente: reconstruye el objeto "Laberinto".
public static class BackroomsMazeBuilder
{
    const float Celda = 3f;
    const float Alto = 2.7f;
    const string DirPrefabs = "Assets/Prefabs";
    const string DirMat = "Assets/Materiales/Backrooms";
    const string DirTex = "Assets/Texturas/Backrooms";
    const string PathModulo = DirPrefabs + "/Pasillo_Modulo.prefab";
    const string PathNicho = DirPrefabs + "/Pasillo_Nicho.prefab";

    // 15x15, '.' transitable, '#' muro. Fila 0 = arriba (norte = +Z), columna 0 = izquierda (+X hacia la derecha).
    static readonly string[] Mapa =
    {
        ".#.......#.....",
        ".#.#.#####.###.",
        "...#.......#...",
        ".#.#.#.#.#...##",
        "...#.#...#.....",
        ".#####.###.###.",
        ".....#.......#.",
        "####.#.###.#.#.",
        ".#...#.#.....#.",
        ".#.#.#.#.#.###.",
        "...#.....#.....",
        ".#####.###.###.",
        ".....#.#...#...",
        "####.###.#.#.##",
        ".........#.....",
    };

    static readonly Vector2Int Inicio = new Vector2Int(0, 0);
    static readonly Vector2Int Salida = new Vector2Int(14, 14);
    static readonly Vector2Int[] Nichos = { new Vector2Int(4, 4), new Vector2Int(0, 8), new Vector2Int(6, 12) };
    static readonly Vector2Int[] Parpadean = { new Vector2Int(6, 2), new Vector2Int(10, 8), new Vector2Int(10, 12) };
    static readonly Vector2Int[] Patrulla = { new Vector2Int(10, 1), new Vector2Int(14, 0), new Vector2Int(12, 4), new Vector2Int(10, 6) };
    static readonly Vector2Int PosEntidad = new Vector2Int(10, 4);

    // 0 = N (+Z), 1 = E (+X), 2 = S (-Z), 3 = O (-X)
    static readonly Vector2Int[] Dir = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
    static readonly string[] NombreDir = { "N", "E", "S", "O" };

    static Material matPared, matRodapie, matAlfombra, matTecho, matPanel, matPuerta, matLetrero, matNicho;

    static bool Libre(int c, int r)
    {
        return c >= 0 && r >= 0 && r < Mapa.Length && c < Mapa[r].Length && Mapa[r][c] == '.';
    }

    static Vector3 Mundo(Vector2Int celda, float y = 0f)
    {
        return new Vector3(celda.x * Celda, y, -celda.y * Celda);
    }

    [MenuItem("Backrooms/Construir laberinto E2")]
    public static void Construir()
    {
        if (!VerificarMapa()) return;
        CrearMateriales();
        var modulo = CrearPrefabModulo();
        var nicho = CrearPrefabNicho();
        ArmarEscena(modulo, nicho);
        Debug.Log("[BackroomsMazeBuilder] Laberinto construido.");
    }

    // ---------- verificacion del mapa ----------
    static bool VerificarMapa()
    {
        var visto = new HashSet<Vector2Int>();
        var cola = new Queue<Vector2Int>();
        cola.Enqueue(Inicio); visto.Add(Inicio);
        while (cola.Count > 0)
        {
            var p = cola.Dequeue();
            foreach (var d in Dir)
            {
                var n = p + d;
                if (Libre(n.x, n.y) && visto.Add(n)) cola.Enqueue(n);
            }
        }
        int libres = 0;
        for (int r = 0; r < Mapa.Length; r++)
            for (int c = 0; c < Mapa[r].Length; c++)
                if (Mapa[r][c] == '.') libres++;
        bool ok = visto.Contains(Salida) && visto.Count == libres;
        Debug.Log("[BackroomsMazeBuilder] Celdas libres: " + libres + ", alcanzables desde el inicio: " + visto.Count + ", salida alcanzable: " + visto.Contains(Salida));
        if (!ok) Debug.LogError("[BackroomsMazeBuilder] El mapa tiene celdas aisladas o la salida no es alcanzable.");
        return ok;
    }

    // ---------- materiales y texturas ----------
    static void AsegurarCarpeta(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string padre = Path.GetDirectoryName(path).Replace('\\', '/');
        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, Path.GetFileName(path));
    }

    static Texture2D TexturaProcedural(string nombre, System.Func<int, int, Color> f)
    {
        string path = DirTex + "/" + nombre + ".png";
        if (!File.Exists(path))
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    t.SetPixel(x, y, f(x, y));
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material Material(string nombre, Color color, Texture2D tex, Vector2 tiling, Color? emision)
    {
        string path = DirMat + "/" + nombre + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", 0.05f);
        m.SetFloat("_Metallic", 0f);
        if (tex != null)
        {
            m.mainTexture = tex;
            m.mainTextureScale = tiling;
        }
        if (emision.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emision.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static void CrearMateriales()
    {
        AsegurarCarpeta(DirMat);
        var alfombraTex = TexturaProcedural("alfombra_color", (x, y) =>
        {
            float ruido = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.6f, y * 0.6f) * 0.4f;
            Color baseC = new Color(0.50f, 0.47f, 0.20f);
            return Color.Lerp(baseC * 0.65f, baseC * 1.1f, ruido);
        });
        var techoTex = TexturaProcedural("techo_color", (x, y) =>
        {
            float ruido = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
            Color c = Color.Lerp(new Color(0.70f, 0.68f, 0.58f), new Color(0.82f, 0.80f, 0.70f), ruido);
            if (x % 128 < 2 || y % 128 < 2) c *= 0.55f; // juntas de las placas
            return c;
        });

        // Papel tapiz: se reutiliza Mat_Backrooms_Wall con sus texturas, con tinte amarillo y tiling.
        matPared = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelo 3D/Materiales/Mat_Backrooms_Wall.mat");
        if (matPared == null) matPared = Material("Mat_Pared", new Color(1f, 0.93f, 0.6f), null, Vector2.one, null);
        matPared.color = new Color(1f, 0.93f, 0.6f);
        matPared.mainTextureScale = new Vector2(0.5f, 0.45f);
        matPared.SetTextureScale("_BumpMap", new Vector2(0.5f, 0.45f));
        matPared.SetFloat("_Glossiness", 0.05f);
        EditorUtility.SetDirty(matPared);

        matRodapie = Material("Mat_Rodapie", new Color(0.20f, 0.11f, 0.06f), null, Vector2.one, null);
        matAlfombra = Material("Mat_Alfombra", Color.white, alfombraTex, new Vector2(1.5f, 1.5f), null);
        matTecho = Material("Mat_Techo", Color.white, techoTex, new Vector2(1.5f, 1.5f), null);
        matPanel = Material("Mat_PanelFluorescente", new Color(0.95f, 0.95f, 0.88f), null, Vector2.one, new Color(1.6f, 1.55f, 1.3f));
        matPuerta = Material("Mat_PuertaSalida", new Color(0.85f, 1f, 0.9f), null, Vector2.one, new Color(1.6f, 2.0f, 1.7f));
        matLetrero = Material("Mat_LetreroSalida", new Color(0.1f, 0.85f, 0.3f), null, Vector2.one, new Color(0.1f, 1.4f, 0.4f));
        matNicho = matPared;
        AssetDatabase.SaveAssets();
    }

    // ---------- prefabs ----------
    static GameObject Caja(Transform padre, string nombre, Vector3 pos, Vector3 escala, Material mat, bool collider)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = pos;
        go.transform.localScale = escala;
        go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>();
        return go;
    }

    static void NoCaminable(GameObject go, bool aplicarAHijos)
    {
        var mod = go.AddComponent<NavMeshModifier>();
        mod.overrideArea = true;
        mod.area = 1; // Not Walkable
        mod.applyToChildren = aplicarAHijos;
    }

    static void MarcarEstatico(GameObject raiz)
    {
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                    | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }

    static GameObject CrearPrefabModulo()
    {
        AsegurarCarpeta(DirPrefabs);
        // Reutiliza el prefab PasilloModulo existente (mismo GUID) renombrandolo.
        if (File.Exists(DirPrefabs + "/PasilloModulo.prefab") && !File.Exists(PathModulo))
            AssetDatabase.RenameAsset(DirPrefabs + "/PasilloModulo.prefab", "Pasillo_Modulo");
        if (!File.Exists(PathModulo))
        {
            var vacio = new GameObject("Pasillo_Modulo");
            PrefabUtility.SaveAsPrefabAsset(vacio, PathModulo);
            Object.DestroyImmediate(vacio);
        }

        var raiz = PrefabUtility.LoadPrefabContents(PathModulo);
        while (raiz.transform.childCount > 0) Object.DestroyImmediate(raiz.transform.GetChild(0).gameObject);
        raiz.name = "Pasillo_Modulo";

        Caja(raiz.transform, "Piso", new Vector3(0, -0.05f, 0), new Vector3(Celda, 0.1f, Celda), matAlfombra, true);
        var techo = Caja(raiz.transform, "Techo", new Vector3(0, Alto + 0.05f, 0), new Vector3(Celda, 0.1f, Celda), matTecho, true);
        NoCaminable(techo, false);
        Caja(raiz.transform, "Panel_Fluorescente", new Vector3(0, Alto - 0.02f, 0), new Vector3(1.2f, 0.04f, 0.6f), matPanel, false);

        float mitad = Celda * 0.5f;
        for (int d = 0; d < 4; d++)
        {
            var pared = new GameObject("Pared_" + NombreDir[d]);
            pared.transform.SetParent(raiz.transform, false);
            pared.transform.localPosition = new Vector3(Dir[d].x * mitad, 0, -Dir[d].y * mitad);
            pared.transform.localRotation = Quaternion.Euler(0, 90f * d, 0);
            // Se orienta hacia el interior de la celda (-Z local).
            Caja(pared.transform, "Muro", new Vector3(0, Alto * 0.5f, -0.05f), new Vector3(Celda, Alto, 0.1f), matPared, true);
            Caja(pared.transform, "Rodapie", new Vector3(0, 0.07f, -0.115f), new Vector3(Celda, 0.14f, 0.03f), matRodapie, false);
        }

        MarcarEstatico(raiz);
        PrefabUtility.SaveAsPrefabAsset(raiz, PathModulo);
        PrefabUtility.UnloadPrefabContents(raiz);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PathModulo);
    }

    static GameObject CrearPrefabNicho()
    {
        var baseModulo = AssetDatabase.LoadAssetAtPath<GameObject>(PathModulo);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(baseModulo);
        inst.name = "Pasillo_Nicho";

        // Hueco lateral hacia +X (E): se retira el muro E y se agrega una alcoba de 1,4 m de fondo.
        inst.transform.Find("Pared_E").gameObject.SetActive(false);
        var nicho = new GameObject("Nicho");
        nicho.transform.SetParent(inst.transform, false);
        float x0 = Celda * 0.5f, prof = 1.4f, ancho = 1.6f;
        float cx = x0 + prof * 0.5f;
        Caja(nicho.transform, "NichoPiso", new Vector3(cx, -0.05f, 0), new Vector3(prof, 0.1f, ancho), matAlfombra, true);
        Caja(nicho.transform, "NichoTecho", new Vector3(cx, Alto + 0.05f, 0), new Vector3(prof, 0.1f, ancho), matTecho, true);
        Caja(nicho.transform, "NichoPared_N", new Vector3(cx, Alto * 0.5f, ancho * 0.5f + 0.05f), new Vector3(prof, Alto, 0.1f), matNicho, true);
        Caja(nicho.transform, "NichoPared_S", new Vector3(cx, Alto * 0.5f, -ancho * 0.5f - 0.05f), new Vector3(prof, Alto, 0.1f), matNicho, true);
        Caja(nicho.transform, "NichoFondo", new Vector3(x0 + prof + 0.05f, Alto * 0.5f, 0), new Vector3(0.1f, Alto, ancho + 0.2f), matNicho, true);
        NoCaminable(nicho, true); // la entidad no entra a los nichos
        MarcarEstatico(inst);

        PrefabUtility.SaveAsPrefabAsset(inst, PathNicho); // al ser instancia de prefab crea una variante
        Object.DestroyImmediate(inst);
        return AssetDatabase.LoadAssetAtPath<GameObject>(PathNicho);
    }

    // ---------- escena ----------
    static void Destruir(string nombre)
    {
        GameObject go;
        while ((go = GameObject.Find(nombre)) != null) Object.DestroyImmediate(go);
    }

    static bool Contiene(Vector2Int[] lista, Vector2Int v)
    {
        foreach (var e in lista) if (e == v) return true;
        return false;
    }

    static void ArmarEscena(GameObject modulo, GameObject nicho)
    {
        // 1. Retirar placeholders
        string[] viejos = { "PasilloModulo", "PasilloModulo_1", "PasilloModulo_2", "PasilloModulo_3", "PasilloModulo_4",
                            "PasilloModulo_L1", "PasilloModulo_L2", "PasilloModulo_L3",
                            "PointLight_0", "PointLight_1", "PointLight_2", "PointLight_3", "PointLight_4",
                            "Laberinto", "Luces", "Hotspots" };
        foreach (var n in viejos) Destruir(n);

        var raiz = new GameObject("Laberinto");
        var modulos = new GameObject("Modulos"); modulos.transform.SetParent(raiz.transform, false);
        var luces = new GameObject("Luces"); luces.transform.SetParent(raiz.transform, false);

        // 2. Celdas
        var hotspotsPos = new List<Vector3>();
        for (int r = 0; r < Mapa.Length; r++)
        {
            for (int c = 0; c < Mapa[r].Length; c++)
            {
                if (!Libre(c, r)) continue;
                var celda = new Vector2Int(c, r);
                bool esNicho = Contiene(Nichos, celda);
                int k = 0;
                if (esNicho)
                {
                    // El nicho se abre hacia un lado bloqueado (E por defecto).
                    int[] pref = { 1, 3, 0, 2 };
                    foreach (int d in pref)
                        if (!Libre(c + Dir[d].x, r + Dir[d].y)) { k = (d - 1 + 4) % 4; break; }
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(esNicho ? nicho : modulo, modulos.transform);
                go.name = (esNicho ? "Nicho_" : "Celda_") + c + "_" + r;
                go.transform.position = Mundo(celda);
                go.transform.rotation = Quaternion.Euler(0, 90f * k, 0);
                for (int d = 0; d < 4; d++)
                {
                    int mundoDir = (d + k) % 4;
                    bool bloqueado = !Libre(c + Dir[mundoDir].x, r + Dir[mundoDir].y);
                    if (esNicho && d == 1) bloqueado = false; // lado del hueco
                    go.transform.Find("Pared_" + NombreDir[d]).gameObject.SetActive(bloqueado);
                }
                hotspotsPos.Add(Mundo(celda, 0.01f));

                bool parpadea = Contiene(Parpadean, celda);
                if (parpadea) ConfigurarParpadeo(go, luces.transform);
                else if ((c + r) % 2 == 0) CrearLuzBakeada(luces.transform, Mundo(celda, Alto - 0.25f), celda);
            }
        }

        // 3. Salida
        CrearSalida(raiz.transform);

        // 4. Hotspots de teletransporte (reusa los 8 existentes, el resto del prefab TeleportHotspot)
        var hsRaiz = new GameObject("Hotspots");
        var existentes = new List<GameObject>();
        foreach (var n in new[] { "TeleportHotspot", "TeleportHotspot_1", "TeleportHotspot_2", "TeleportHotspot_3", "TeleportHotspot_4", "TeleportHotspot_5", "TeleportHotspot_6", "TeleportHotspot_7" })
        {
            var g = GameObject.Find(n);
            if (g != null) existentes.Add(g);
        }
        var prefabHotspot = AssetDatabase.LoadAssetAtPath<GameObject>(DirPrefabs + "/TeleportHotspot.prefab");
        for (int i = 0; i < hotspotsPos.Count; i++)
        {
            GameObject h;
            if (i < existentes.Count) h = existentes[i];
            else h = (GameObject)PrefabUtility.InstantiatePrefab(prefabHotspot);
            h.transform.SetParent(hsRaiz.transform, false);
            h.transform.position = hotspotsPos[i];
            h.transform.rotation = Quaternion.identity;
            if (i >= existentes.Count) h.name = "TeleportHotspot_" + i;
        }
        for (int i = hotspotsPos.Count; i < existentes.Count; i++) existentes[i].SetActive(false);

        // 5. Jugador al inicio, mirando hacia el pasillo (sur)
        var rig = GameObject.Find("OVRCameraRigInteraction");
        if (rig != null)
        {
            rig.transform.position = Mundo(Inicio);
            rig.transform.rotation = Quaternion.Euler(0, 180f, 0);
        }

        // 6. Entidad y waypoints
        ConfigurarEntidad();

        // 7. Ambiente / luz global
        var sol = GameObject.Find("Directional Light");
        if (sol != null) sol.SetActive(false);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.30f, 0.27f, 0.15f);

        // 8. NavMesh
        BakearNavMesh(raiz);

        // 9. Lightmaps
        ConfigurarLightmaps();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    static void CrearLuzBakeada(Transform padre, Vector3 pos, Vector2Int celda)
    {
        var go = new GameObject("LuzBakeada_" + celda.x + "_" + celda.y);
        go.transform.SetParent(padre, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 6f;
        l.intensity = 0.9f;
        l.color = new Color(1f, 0.97f, 0.82f);
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Baked;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
    }

    static void ConfigurarParpadeo(GameObject celda, Transform luces)
    {
        var panel = celda.transform.Find("Panel_Fluorescente");
        // Se retira de batching estatico para poder cambiar su emision en runtime.
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
        GameObjectUtility.SetStaticEditorFlags(panel.gameObject, flags);

        var go = new GameObject("LuzParpadeo_" + celda.name);
        go.transform.SetParent(luces, false);
        go.transform.position = celda.transform.position + new Vector3(0, Alto - 0.25f, 0);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 7f;
        l.intensity = 1.2f;
        l.color = new Color(1f, 0.97f, 0.82f);
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;

        var f = celda.AddComponent<FluorescentFlicker>();
        f.panel = panel.GetComponent<Renderer>();
        f.luz = l;
    }

    static void CrearSalida(Transform padre)
    {
        var s = new GameObject("Salida");
        s.transform.SetParent(padre, false);
        var centro = Mundo(Salida);
        float x = centro.x + Celda * 0.5f - 0.12f; // pegado al muro este del ultimo pasillo
        var puerta = Caja(s.transform, "Puerta_Luz", new Vector3(x, 1.05f, centro.z), new Vector3(0.04f, 2.1f, 1.1f), matPuerta, false);
        Caja(s.transform, "Letrero_Verde", new Vector3(x, 2.35f, centro.z), new Vector3(0.04f, 0.2f, 0.8f), matLetrero, false);
        var go = new GameObject("Luz_Salida");
        go.transform.SetParent(s.transform, false);
        go.transform.position = new Vector3(x - 0.6f, 1.5f, centro.z);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 5f;
        l.intensity = 1.1f;
        l.color = new Color(0.8f, 1f, 0.85f);
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Baked;
        var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
        foreach (var t in s.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }

    static void ConfigurarEntidad()
    {
        var ent = GameObject.Find("Entidad");
        if (ent == null) { Debug.LogWarning("[BackroomsMazeBuilder] No se encontro 'Entidad'."); return; }

        var wpRaiz = GameObject.Find("Patrulla") ?? new GameObject("Patrulla");
        var puntos = new Transform[Patrulla.Length];
        for (int i = 0; i < Patrulla.Length; i++)
        {
            var g = GameObject.Find("PuntoPatrulla_" + (i + 1)) ?? new GameObject("PuntoPatrulla_" + (i + 1));
            g.transform.SetParent(wpRaiz.transform, false);
            g.transform.position = Mundo(Patrulla[i], 0.1f);
            puntos[i] = g.transform;
        }

        var agente = ent.GetComponent<NavMeshAgent>();
        ent.transform.position = Mundo(PosEntidad, 1f);
        var ia = ent.GetComponent<EntityAI>();
        if (ia != null)
        {
            var so = new SerializedObject(ia);
            var arr = so.FindProperty("puntosDePatrulla");
            arr.arraySize = puntos.Length;
            for (int i = 0; i < puntos.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = puntos[i];
            so.ApplyModifiedProperties();
        }
        Debug.Log("[BackroomsMazeBuilder] Entidad: baseOffset=" + (agente != null ? agente.baseOffset.ToString() : "n/a") + ", waypoints=" + puntos.Length);
    }

    static void BakearNavMesh(GameObject raiz)
    {
        UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
        var surface = raiz.GetComponent<NavMeshSurface>() ?? raiz.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.BuildNavMesh();

        const string path = "Assets/Backrooms_Main/NavMesh.asset";
        var data = surface.navMeshData;
        if (data != null)
        {
            AsegurarCarpeta("Assets/Backrooms_Main");
            if (AssetDatabase.LoadAssetAtPath<NavMeshData>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        }
        var tri = NavMesh.CalculateTriangulation();
        Debug.Log("[BackroomsMazeBuilder] NavMesh bakeado: " + tri.vertices.Length + " vertices, " + (tri.indices.Length / 3) + " triangulos.");
    }

    [MenuItem("Backrooms/Configurar lightmaps")]
    public static void ConfigurarLightmaps()
    {
        LightingSettings ls = null;
        try { ls = Lightmapping.lightingSettings; } catch (System.Exception) { /* la escena aun no tiene settings */ }
        if (ls == null)
        {
            ls = new LightingSettings { name = "Lighting_Backrooms" };
            AssetDatabase.CreateAsset(ls, "Assets/Backrooms_Main/Lighting_Backrooms.lighting");
            Lightmapping.lightingSettings = ls;
        }
        ls.bakedGI = true;
        ls.realtimeGI = false;
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        ls.lightmapResolution = 8;
        ls.lightmapMaxSize = 1024;
        ls.directSampleCount = 32;
        ls.indirectSampleCount = 128;
        ls.environmentSampleCount = 64;
        ls.ao = true;
        EditorUtility.SetDirty(ls);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }
}
