using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Agrega una puerta con mirilla a cada nicho (RF-08) de la escena abierta. Menu: Backrooms/Agregar puertas a los nichos.
//
// Cada puerta es UNA bisagra (Transform) con dos mallas: madera (tablero inferior y superior con la mirilla entre ambos,
// travesanos) y metal oxidado (bisagras y manija). Son 2 draw calls por puerta, sin colliders, sin luces, sin texturas nuevas:
// reutiliza Mat_MesaVieja_MaderaOscura y Mat_MesaVieja_Oxido de las mesas. Las dos mallas se generan una vez (UV en metros,
// para que la madera no se estire) y las comparten las tres puertas.
//
// Geometria (coordenadas del objeto "Nicho"): el hueco del nicho esta en el plano x = 1,45 (grosor del muro 0,10), mide 1,6 m
// de ancho (z de -0,8 a 0,8) y llega al techo (2,7 m). La hoja mide 1,56 x 2,66 m, 5 cm de grosor, con la bisagra en z = +0,78
// y la hoja hacia -z. Gira con yaw positivo hacia AFUERA (el pasillo, -x): entreabierta = 100 grados; cerrada = 0.
// Mirilla: y de 0,97 a 1,03 (6 cm, centro a 1,00 m) y 38 cm de ancho en el centro de la hoja.
public static class BackroomsPuertasBuilder
{
    const string DirMalla = "Assets/Modelo 3D/Puerta";
    const string MatMadera = "Assets/Materiales/Backrooms/Mat_MesaVieja_MaderaOscura.mat";
    const string MatOxido = "Assets/Materiales/Backrooms/Mat_MesaVieja_Oxido.mat";

    const float PlanoX = 1.45f;       // plano del muro del nicho
    const float BisagraZ = 0.78f;
    const float Ancho = 1.56f, Grosor = 0.05f;
    const float Y0 = 0.02f, Y1 = 2.68f;
    const float MirillaY = 1.00f, MirillaAlto = 0.06f, MirillaAncho = 0.38f;
    const float AnguloAbierta = 100f;   // entreabierta; a 130+ grados la hoja atraviesa Pared_N de Nicho_0_8

    [MenuItem("Backrooms/Agregar puertas a los nichos")]
    public static void Generar()
    {
        var madera = AssetDatabase.LoadAssetAtPath<Material>(MatMadera);
        var oxido = AssetDatabase.LoadAssetAtPath<Material>(MatOxido);
        if (madera == null || oxido == null) { Debug.LogError("[BackroomsPuertasBuilder] Faltan los materiales de las mesas."); return; }

        var mallaMadera = CrearMalla("PuertaNicho_Madera", true);
        var mallaOxido = CrearMalla("PuertaNicho_Oxido", false);

        int n = 0;
        foreach (var zona in Object.FindObjectsOfType<NichoZona>(true))
        {
            var nicho = zona.transform.parent;               // objeto "Nicho"
            if (nicho == null) continue;
            var vieja = nicho.Find("Puerta_Nicho");
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

            var raiz = new GameObject("Puerta_Nicho");
            raiz.transform.SetParent(nicho, false);
            var bisagra = new GameObject("Bisagra");
            bisagra.transform.SetParent(raiz.transform, false);
            bisagra.transform.localPosition = new Vector3(PlanoX, 0f, BisagraZ);
            bisagra.transform.localRotation = Quaternion.Euler(0f, AnguloAbierta, 0f);   // entreabierta

            Pieza("Madera", bisagra.transform, mallaMadera, madera);
            Pieza("Metal", bisagra.transform, mallaOxido, oxido);

            var p = raiz.AddComponent<PuertaNicho>();
            p.nicho = zona;
            p.bisagra = bisagra.transform;
            p.anguloAbierta = AnguloAbierta;
            n++;
        }
        var escena = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log("[BackroomsPuertasBuilder] Puertas agregadas: " + n + " (escena guardada).");
    }

    static void Pieza(string nombre, Transform padre, Mesh malla, Material mat)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.AddComponent<MeshFilter>().sharedMesh = malla;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // ---------- mallas ----------

    // Hoja en el sistema de la bisagra: x = grosor (centrado), y = altura, z de 0 a -Ancho.
    static Mesh CrearMalla(string nombre, bool esMadera)
    {
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        float zc = -Ancho * 0.5f;
        float mz0 = zc - MirillaAncho * 0.5f, mz1 = zc + MirillaAncho * 0.5f;          // mirilla: de mz0 a mz1 (z crece hacia la bisagra)
        float my0 = MirillaY - MirillaAlto * 0.5f, my1 = MirillaY + MirillaAlto * 0.5f;

        if (esMadera)
        {
            // Tablero inferior y superior (ancho completo) y los dos tramos a los lados de la mirilla.
            Caja(v, nrm, uv, tri, new Vector3(0f, (Y0 + my0) * 0.5f, zc), new Vector3(Grosor, my0 - Y0, Ancho));
            Caja(v, nrm, uv, tri, new Vector3(0f, (my1 + Y1) * 0.5f, zc), new Vector3(Grosor, Y1 - my1, Ancho));
            Caja(v, nrm, uv, tri, new Vector3(0f, MirillaY, (0f + mz1) * 0.5f), new Vector3(Grosor, MirillaAlto, 0f - mz1));          // lado de la bisagra
            Caja(v, nrm, uv, tri, new Vector3(0f, MirillaY, (mz0 - Ancho) * 0.5f), new Vector3(Grosor, MirillaAlto, mz0 + Ancho));   // lado libre
            // Travesanos por fuera (cara hacia el pasillo, -x), lejos de la mirilla.
            Caja(v, nrm, uv, tri, new Vector3(-0.035f, 0.38f, zc), new Vector3(0.02f, 0.14f, Ancho - 0.06f));
            Caja(v, nrm, uv, tri, new Vector3(-0.035f, 2.12f, zc), new Vector3(0.02f, 0.14f, Ancho - 0.06f));
        }
        else
        {
            // Bisagras (dos tiras y su eje) y manija con su placa, por fuera.
            foreach (float y in new[] { 0.45f, 2.10f })
            {
                Caja(v, nrm, uv, tri, new Vector3(-0.034f, y, -0.16f), new Vector3(0.016f, 0.07f, 0.32f));
                Caja(v, nrm, uv, tri, new Vector3(0f, y, 0f), new Vector3(0.06f, 0.13f, 0.04f));
            }
            Caja(v, nrm, uv, tri, new Vector3(-0.034f, 1.18f, -1.40f), new Vector3(0.012f, 0.16f, 0.07f));
            Caja(v, nrm, uv, tri, new Vector3(-0.058f, 1.18f, -1.40f), new Vector3(0.04f, 0.035f, 0.16f));
        }

        var m = new Mesh { name = nombre };
        m.SetVertices(v); m.SetNormals(nrm); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
        m.RecalculateBounds();
        m.RecalculateTangents();
        m.UploadMeshData(false);

        System.IO.Directory.CreateDirectory(DirMalla);
        string path = DirMalla + "/" + nombre + ".asset";
        var previo = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (previo != null) { EditorUtility.CopySerialized(m, previo); Object.DestroyImmediate(m); m = previo; EditorUtility.SetDirty(m); }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    // Caja con normales por cara y UV planares en metros (continuas entre caras: la veta no se estira).
    static void Caja(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 s)
    {
        Vector3 h = s * 0.5f;
        Cara(v, n, uv, t, c, Vector3.right, Vector3.up, Vector3.forward, h.x, h.y, h.z);
        Cara(v, n, uv, t, c, Vector3.left, Vector3.up, Vector3.forward, h.x, h.y, h.z);
        Cara(v, n, uv, t, c, Vector3.up, Vector3.right, Vector3.forward, h.y, h.x, h.z);
        Cara(v, n, uv, t, c, Vector3.down, Vector3.right, Vector3.forward, h.y, h.x, h.z);
        Cara(v, n, uv, t, c, Vector3.forward, Vector3.right, Vector3.up, h.z, h.x, h.y);
        Cara(v, n, uv, t, c, Vector3.back, Vector3.right, Vector3.up, h.z, h.x, h.y);
    }

    static void Cara(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 c, Vector3 normal, Vector3 u, Vector3 w, float hn, float hu, float hw)
    {
        Vector3 centro = c + normal * hn;
        var p = new[] { centro - u * hu - w * hw, centro + u * hu - w * hw, centro + u * hu + w * hw, centro - u * hu + w * hw };
        int b = v.Count;
        for (int i = 0; i < 4; i++)
        {
            v.Add(p[i]); n.Add(normal);
            uv.Add(new Vector2(Vector3.Dot(p[i], u), Vector3.Dot(p[i], w)));
        }
        // Devanado: en Unity la normal de (a,b,c) es Cross(b-a, c-a); si no coincide, se invierte.
        bool bien = Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), normal) > 0f;
        if (bien) { t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3); }
        else { t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2); }
    }
}
