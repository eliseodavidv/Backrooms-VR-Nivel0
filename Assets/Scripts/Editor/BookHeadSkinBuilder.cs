using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Crea la version skineada y reducida de Book Head (un solo SkinnedMeshRenderer, 3 submeshes = 3 materiales, 2 influencias por
// vertice) y la instala bajo Entidad/Modelo dejando la malla estatica anterior como respaldo desactivado.
// Se llama a mano (execute_code o el menu); no forma parte de los builders viejos.
public static class BookHeadSkinBuilder
{
    public const string RutaPrefab = "Assets/BookHeadMonster/Built-in/Prefabs/BookHeadMonster.prefab";
    public const string RutaMalla = "Assets/Modelo 3D/Combate/BookHead_Skin.asset";

    static float DistPesos(BoneWeight a, BoneWeight b)
    {
        var ia = new[] { a.boneIndex0, a.boneIndex1, a.boneIndex2, a.boneIndex3 };
        var wa = new[] { a.weight0, a.weight1, a.weight2, a.weight3 };
        var ib = new[] { b.boneIndex0, b.boneIndex1, b.boneIndex2, b.boneIndex3 };
        var wb = new[] { b.weight0, b.weight1, b.weight2, b.weight3 };
        float d = 0f;
        for (int i = 0; i < 4; i++)
        {
            if (wa[i] <= 0f) continue;
            float otro = 0f;
            for (int j = 0; j < 4; j++) if (ib[j] == ia[i]) otro += wb[j];
            d += Mathf.Abs(wa[i] - otro);
        }
        for (int j = 0; j < 4; j++)
        {
            if (wb[j] <= 0f) continue;
            bool esta = false;
            for (int i = 0; i < 4; i++) if (wa[i] > 0f && ia[i] == ib[j]) esta = true;
            if (!esta) d += wb[j];
        }
        return d; // 0 (iguales) .. 2 (totalmente distintos)
    }

    // Se queda con las 2 influencias mas fuertes y las renormaliza; remapea el indice de hueso a la lista del cuerpo.
    static BoneWeight Top2(BoneWeight w, int[] remap)
    {
        var idx = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
        var pes = new[] { w.weight0, w.weight1, w.weight2, w.weight3 };
        int p = 0, s = -1;
        for (int i = 1; i < 4; i++) if (pes[i] > pes[p]) p = i;
        for (int i = 0; i < 4; i++) if (i != p && (s < 0 || pes[i] > pes[s])) s = i;
        float w0 = pes[p], w1 = s >= 0 ? pes[s] : 0f;
        float suma = w0 + w1; if (suma <= 1e-6f) { w0 = 1f; w1 = 0f; suma = 1f; }
        var r = new BoneWeight();
        r.boneIndex0 = remap[idx[p]]; r.weight0 = w0 / suma;
        if (w1 > 1e-4f) { r.boneIndex1 = remap[idx[s]]; r.weight1 = w1 / suma; } else { r.boneIndex1 = 0; r.weight1 = 0f; r.weight0 = 1f; }
        return r;
    }

    public static string CrearMalla(int trisCuerpo, int trisDientes, int trisOjos)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        if (pf == null) return "No se encuentra " + RutaPrefab;
        var smrs = pf.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer cuerpo = null, dientes = null, ojos = null;
        foreach (var s in smrs) { if (s.name == "Body") cuerpo = s; else if (s.name == "Teeth") dientes = s; else if (s.name == "Eye") ojos = s; }
        if (cuerpo == null || dientes == null || ojos == null) return "Faltan Body/Teeth/Eye en el prefab";

        var huesos = cuerpo.bones;
        var indiceHueso = new Dictionary<Transform, int>();
        for (int i = 0; i < huesos.Length; i++) indiceHueso[huesos[i]] = i;

        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var ts = new List<Vector4>();
        var bws = new List<BoneWeight>();
        var subs = new[] { new List<int>(), new List<int>(), new List<int>() };
        var info = new System.Text.StringBuilder();

        var partes = new[] { cuerpo, dientes, ojos };
        var objetivos = new[] { trisCuerpo, trisDientes, trisOjos };
        for (int pi = 0; pi < partes.Length; pi++)
        {
            var src = partes[pi].sharedMesh;
            var bwOrig = src.boneWeights;
            var remap = new int[partes[pi].bones.Length];
            for (int i = 0; i < remap.Length; i++)
            {
                int j;
                if (!indiceHueso.TryGetValue(partes[pi].bones[i], out j)) return "El hueso " + partes[pi].bones[i].name + " de " + partes[pi].name + " no esta en el cuerpo";
                remap[i] = j;
            }
            int antes = src.triangles.Length / 3;
            // Unir vertices con pesos de piel distintos cuesta 4 veces mas (conserva codos, rodillas, cuello).
            MeshDecimador.FactorCosto = (a, b) => 1.0 + 3.0 * DistPesos(bwOrig[a], bwOrig[b]);
            Mesh low;
            // Dientes y ojos miden 3-7 cm: se pueden simplificar con menos miramiento (el cuerpo mantiene el umbral estricto).
            MeshDecimador.UmbralNormal = pi == 0 ? 0.2f : -0.6f;
            try { low = MeshDecimador.Decimar(src, objetivos[pi]); }
            finally { MeshDecimador.FactorCosto = null; MeshDecimador.UmbralNormal = 0.2f; }
            var origen = MeshDecimador.UltimoOrigen;
            int baseIdx = vs.Count;
            var lv = low.vertices; var ln = low.normals; var luv = low.uv; var lt = low.tangents;
            for (int k = 0; k < lv.Length; k++)
            {
                vs.Add(lv[k]); ns.Add(ln[k]); uvs.Add(luv.Length > k ? luv[k] : Vector2.zero);
                ts.Add(lt.Length > k ? lt[k] : new Vector4(1, 0, 0, 1));
                bws.Add(Top2(bwOrig[origen[k]], remap));
            }
            for (int sm = 0; sm < low.subMeshCount; sm++)
            {
                // Cuerpo: submesh 0,1,2 = materiales 1,2,3. Dientes y ojos usan el material 3.
                int destino = pi == 0 ? Mathf.Min(sm, 2) : 2;
                foreach (int ix in low.GetTriangles(sm)) subs[destino].Add(ix + baseIdx);
            }
            info.Append(partes[pi].name + ": " + antes + " -> " + low.triangles.Length / 3 + " tris; ");
        }

        var res = new Mesh { name = "BookHead_Skin" };
        res.indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        res.SetVertices(vs); res.SetNormals(ns); res.SetUVs(0, uvs); res.SetTangents(ts);
        res.subMeshCount = 3;
        for (int i = 0; i < 3; i++) res.SetTriangles(subs[i], i);
        res.bindposes = cuerpo.sharedMesh.bindposes;
        res.boneWeights = bws.ToArray();
        res.RecalculateBounds();
        var b = res.bounds; b.Expand(new Vector3(0.3f, 0.1f, 0.3f)); res.bounds = b; // margen para que no se corte al moverse

        AssetDatabase.DeleteAsset(RutaMalla);
        AssetDatabase.CreateAsset(res, RutaMalla);
        AssetDatabase.SaveAssets();

        int tris = res.triangles.Length / 3;
        int dosInfl = 0; foreach (var w in bws) if (w.weight1 > 0f) dosInfl++;
        return info + "| TOTAL tris=" + tris + " verts=" + vs.Count + " huesos(bindposes)=" + res.bindposes.Length + " vertices con 2 influencias=" + dosInfl + "/" + bws.Count + " bounds=" + res.bounds.size;
    }

    // Deja bajo 'modelo': Modelo_CuerpoEntero (respaldo, desactivado) y Modelo_Huesos (skineado). Devuelve el Modelo_Huesos.
    public static GameObject InstalarEnEscena(GameObject modelo, out string info)
    {
        var sb = new System.Text.StringBuilder();
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
        var malla = AssetDatabase.LoadAssetAtPath<Mesh>(RutaMalla);

        // Respaldo de la malla estatica actual.
        var mf = modelo.GetComponent<MeshFilter>(); var mr = modelo.GetComponent<MeshRenderer>();
        Material[] mats = mr != null ? mr.sharedMaterials : null;
        var respaldo = modelo.transform.Find("Modelo_CuerpoEntero");
        if (respaldo == null && mf != null && mr != null)
        {
            var go = new GameObject("Modelo_CuerpoEntero");
            Undo.RegisterCreatedObjectUndo(go, "Respaldo cuerpo entero");
            go.layer = modelo.layer;
            go.transform.SetParent(modelo.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r2 = go.AddComponent<MeshRenderer>();
            r2.sharedMaterials = mr.sharedMaterials;
            r2.shadowCastingMode = mr.shadowCastingMode; r2.receiveShadows = mr.receiveShadows;
            go.SetActive(false);
            Object.DestroyImmediate(mr); Object.DestroyImmediate(mf);
            sb.AppendLine("Malla estatica movida a Modelo_CuerpoEntero (desactivado).");
        }
        else if (respaldo != null)
        {
            var rr = respaldo.GetComponent<MeshRenderer>(); if (rr != null && mats == null) mats = rr.sharedMaterials;
        }
        if (mats == null || mats.Length < 3) return Fallo(out info, "No se pudieron obtener los materiales actuales");

        var viejo = modelo.transform.Find("Modelo_Huesos");
        if (viejo != null) Object.DestroyImmediate(viejo.gameObject);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf, modelo.transform);
        inst.name = "Modelo_Huesos";
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;

        SkinnedMeshRenderer cuerpo = null;
        foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (s.name == "Body") cuerpo = s; }
        foreach (var s in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (s != cuerpo) Object.DestroyImmediate(s.gameObject); }
        cuerpo.sharedMesh = malla;
        cuerpo.sharedMaterials = new[] { mats[0], mats[1], mats[2] };
        cuerpo.quality = SkinQuality.Bone2;
        cuerpo.updateWhenOffscreen = false;
        cuerpo.skinnedMotionVectors = false;
        if (cuerpo.rootBone == null) { var raiz0 = inst.transform.Find("root"); if (raiz0 != null) cuerpo.rootBone = raiz0; }
        cuerpo.localBounds = BoundsEnHuesoRaiz(cuerpo, malla);
        cuerpo.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cuerpo.receiveShadows = false;
        if (cuerpo.rootBone == null) { var raiz = inst.transform.Find("root"); if (raiz != null) cuerpo.rootBone = raiz; }
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = modelo.layer;
        EditorUtility.SetDirty(cuerpo);
        sb.AppendLine("Modelo_Huesos: huesos=" + cuerpo.bones.Length + " bindposes=" + malla.bindposes.Length + " rootBone=" + (cuerpo.rootBone != null ? cuerpo.rootBone.name : "null") + " transforms=" + inst.GetComponentsInChildren<Transform>(true).Length);
        info = sb.ToString();
        return inst;
    }

    // SkinnedMeshRenderer.localBounds esta en el espacio del hueso raiz (que aqui esta girado 270 grados), no en el de la malla.
    public static Bounds BoundsEnHuesoRaiz(SkinnedMeshRenderer smr, Mesh malla)
    {
        var raiz = smr.rootBone != null ? smr.rootBone : smr.transform;
        var b = malla.bounds;
        bool primero = true; Bounds r = new Bounds();
        for (int i = 0; i < 8; i++)
        {
            Vector3 c = b.center + new Vector3((i & 1) == 0 ? -b.extents.x : b.extents.x, (i & 2) == 0 ? -b.extents.y : b.extents.y, (i & 4) == 0 ? -b.extents.z : b.extents.z);
            Vector3 p = raiz.InverseTransformPoint(smr.transform.TransformPoint(c));
            if (primero) { r = new Bounds(p, Vector3.zero); primero = false; } else r.Encapsulate(p);
        }
        return r;
    }

    static GameObject Fallo(out string info, string msg) { info = msg; return null; }
}
