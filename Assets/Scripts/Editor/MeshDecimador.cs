using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Decimacion por colapso de aristas con metrica de error cuadrica (Garland-Heckbert), sobre mallas indexadas.
// Las aristas de borde (costuras UV) llevan un plano de penalizacion para que no se rompan.
public static class MeshDecimador
{
    class Q
    {
        public double a, b, c, d, e, f, g, h, i, j; // matriz simetrica 4x4 (10 coeficientes)
        public void Add(double[] p, double w)
        {
            a += w * p[0] * p[0]; b += w * p[0] * p[1]; c += w * p[0] * p[2]; d += w * p[0] * p[3];
            e += w * p[1] * p[1]; f += w * p[1] * p[2]; g += w * p[1] * p[3];
            h += w * p[2] * p[2]; i += w * p[2] * p[3]; j += w * p[3] * p[3];
        }
        public void Add(Q o) { a += o.a; b += o.b; c += o.c; d += o.d; e += o.e; f += o.f; g += o.g; h += o.h; i += o.i; j += o.j; }
        public double Err(Vector3 v)
        {
            double x = v.x, y = v.y, z = v.z;
            return a * x * x + 2 * b * x * y + 2 * c * x * z + 2 * d * x + e * y * y + 2 * f * y * z + 2 * g * y + h * z * z + 2 * i * z + j;
        }
    }

    struct Arista { public double costo; public int a, b, vA, vB; }

    // Ganchos para malla skineada (opcionales; null/vacio = comportamiento de siempre):
    //  - FactorCosto(a, b): multiplica el costo de colapsar el vertice 'a' en 'b' (p. ej. penaliza unir vertices con pesos de hueso distintos).
    //  - UltimoOrigen: tras Decimar, indice del vertice ORIGINAL de cada vertice de la malla resultante (los vertices que quedan
    //    son originales, asi que sus pesos de piel se heredan tal cual).
    public static System.Func<int, int, double> FactorCosto;
    public static int[] UltimoOrigen;
    // Un colapso se rechaza si gira la normal de un triangulo mas que esto (coseno). 0.2 = estricto (valor de siempre).
    public static float UmbralNormal = 0.2f;

    public static Mesh Decimar(Mesh src, int trisObjetivo)
    {
        var pos = new List<Vector3>(src.vertices);
        var nrm = new List<Vector3>(src.normals);
        var uv = new List<Vector2>(src.uv);
        var tan = new List<Vector4>(src.tangents);
        var tris = new List<int[]>();
        var subDe = new List<int>();
        for (int sm = 0; sm < src.subMeshCount; sm++)
        {
            var idx = src.GetTriangles(sm);
            for (int i = 0; i < idx.Length; i += 3) { tris.Add(new[] { idx[i], idx[i + 1], idx[i + 2] }); subDe.Add(sm); }
        }
        int nv = pos.Count;
        var vivo = new bool[tris.Count];
        for (int i = 0; i < vivo.Length; i++) vivo[i] = true;
        var qs = new Q[nv];
        for (int i = 0; i < nv; i++) qs[i] = new Q();

        var conteoArista = new Dictionary<long, int>();
        System.Func<int, int, long> clave = (x, y) => x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
        foreach (var t in tris)
            for (int k = 0; k < 3; k++)
            {
                long ck = clave(t[k], t[(k + 1) % 3]);
                conteoArista[ck] = conteoArista.ContainsKey(ck) ? conteoArista[ck] + 1 : 1;
            }

        foreach (var t in tris)
        {
            Vector3 n = Vector3.Cross(pos[t[1]] - pos[t[0]], pos[t[2]] - pos[t[0]]);
            if (n.sqrMagnitude < 1e-18f) continue;
            n.Normalize();
            double[] p = { n.x, n.y, n.z, -Vector3.Dot(n, pos[t[0]]) };
            for (int k = 0; k < 3; k++) qs[t[k]].Add(p, 1.0);
            for (int k = 0; k < 3; k++)
            {
                int u = t[k], w = t[(k + 1) % 3];
                if (conteoArista[clave(u, w)] != 1) continue;
                Vector3 e = (pos[w] - pos[u]).normalized;
                Vector3 pn = Vector3.Cross(e, n).normalized;
                double[] pb = { pn.x, pn.y, pn.z, -Vector3.Dot(pn, pos[u]) };
                qs[u].Add(pb, 100.0); qs[w].Add(pb, 100.0);
            }
        }

        var vertTris = new List<int>[nv];
        for (int i = 0; i < nv; i++) vertTris[i] = new List<int>();
        for (int i = 0; i < tris.Count; i++) foreach (var v in tris[i]) vertTris[v].Add(i);

        var version = new int[nv];
        var cola = new SortedSet<(double, int, int, int, int, int)>(); // costo, desempate, a, b, verA, verB
        int uniq = 0;
        System.Action<int, int> encolar = (a, b) =>
        {
            var qq = new Q(); qq.Add(qs[a]); qq.Add(qs[b]);
            double ca = qq.Err(pos[b]); // colapsa a -> b
            double cb = qq.Err(pos[a]); // colapsa b -> a
            if (FactorCosto != null)
            {
                ca = (ca + 1e-9) * FactorCosto(a, b);
                cb = (cb + 1e-9) * FactorCosto(b, a);
            }
            cola.Add((ca, uniq++, a, b, version[a], version[b]));
            cola.Add((cb, uniq++, b, a, version[b], version[a]));
        };
        var vistas = new HashSet<long>();
        foreach (var t in tris)
            for (int k = 0; k < 3; k++)
            {
                long ck = clave(t[k], t[(k + 1) % 3]);
                if (vistas.Add(ck)) encolar(t[k], t[(k + 1) % 3]);
            }

        int vivas = tris.Count;
        var muerto = new bool[nv];
        while (vivas > trisObjetivo && cola.Count > 0)
        {
            var e = cola.Min; cola.Remove(e);
            int a = e.Item3, b = e.Item4; // a se funde en b
            if (muerto[a] || muerto[b] || version[a] != e.Item5 || version[b] != e.Item6) continue;

            // evita voltear triangulos: comprueba normales antes/despues
            bool malo = false;
            foreach (int ti in vertTris[a])
            {
                if (!vivo[ti]) continue;
                var t = tris[ti];
                if (t[0] == b || t[1] == b || t[2] == b) continue;
                Vector3 p0 = pos[t[0]], p1 = pos[t[1]], p2 = pos[t[2]];
                Vector3 antes = Vector3.Cross(p1 - p0, p2 - p0);
                for (int k = 0; k < 3; k++) if (t[k] == a) { if (k == 0) p0 = pos[b]; else if (k == 1) p1 = pos[b]; else p2 = pos[b]; }
                Vector3 despues = Vector3.Cross(p1 - p0, p2 - p0);
                if (despues.sqrMagnitude < 1e-16f || Vector3.Dot(antes.normalized, despues.normalized) < UmbralNormal) { malo = true; break; }
            }
            if (malo) continue;

            muerto[a] = true;
            qs[b].Add(qs[a]);
            version[b]++;
            foreach (int ti in vertTris[a])
            {
                if (!vivo[ti]) continue;
                var t = tris[ti];
                bool tieneB = t[0] == b || t[1] == b || t[2] == b;
                if (tieneB) { vivo[ti] = false; vivas--; continue; }
                for (int k = 0; k < 3; k++) if (t[k] == a) t[k] = b;
                vertTris[b].Add(ti);
            }
            var vec = new HashSet<int>();
            foreach (int ti in vertTris[b])
            {
                if (!vivo[ti]) continue;
                foreach (int v in tris[ti]) if (v != b) vec.Add(v);
            }
            foreach (int v in vec) encolar(b, v);
        }

        // reconstruye
        var mapa = new Dictionary<int, int>();
        var nvp = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nt = new List<Vector4>();
        var ntri = new List<int>[src.subMeshCount];
        for (int i = 0; i < ntri.Length; i++) ntri[i] = new List<int>();
        for (int i = 0; i < tris.Count; i++)
        {
            if (!vivo[i]) continue;
            foreach (int v in tris[i])
            {
                int m;
                if (!mapa.TryGetValue(v, out m))
                {
                    m = nvp.Count; mapa[v] = m;
                    nvp.Add(pos[v]); nn.Add(nrm[v]); nuv.Add(uv[v]); nt.Add(tan.Count > v ? tan[v] : new Vector4(1, 0, 0, 1));
                }
                ntri[subDe[i]].Add(m);
            }
        }
        var origen = new int[nvp.Count];
        foreach (var kv in mapa) origen[kv.Value] = kv.Key;
        UltimoOrigen = origen;
        var res = new Mesh { name = src.name + "_Low" };
        res.indexFormat = nvp.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        res.SetVertices(nvp); res.SetNormals(nn); res.SetUVs(0, nuv); res.SetTangents(nt);
        res.subMeshCount = ntri.Length;
        for (int i = 0; i < ntri.Length; i++) res.SetTriangles(ntri[i], i);
        res.RecalculateBounds();
        return res;
    }

    // Toma los SkinnedMeshRenderer de una instancia, deja cada uno en la pose del prefab, los decima (objetivos por nombre)
    // y los une en una malla estatica con un submesh por material. Devuelve la malla guardada en 'dstPath'.
    public static string ConstruirEstatica(GameObject inst, string dstPath, Dictionary<string, int> objetivos, out Material[] mats)
    {
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>(); var ts = new List<Vector4>();
        var subs = new List<List<int>>(); var lista = new List<Material>();
        var info = new System.Text.StringBuilder();
        foreach (var sk in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh m = sk.sharedMesh;
            int objetivo;
            if (objetivos.TryGetValue(sk.name, out objetivo) && m.triangles.Length / 3 > objetivo) m = Decimar(m, objetivo);
            Matrix4x4 mt = inst.transform.worldToLocalMatrix * sk.transform.localToWorldMatrix;
            Matrix4x4 mn = mt.inverse.transpose;
            int baseIdx = vs.Count;
            var sv = m.vertices; var sn = m.normals; var su = m.uv; var st = m.tangents;
            for (int i = 0; i < sv.Length; i++)
            {
                vs.Add(mt.MultiplyPoint3x4(sv[i]));
                ns.Add(sn.Length > i ? mn.MultiplyVector(sn[i]).normalized : Vector3.up);
                uvs.Add(su.Length > i ? su[i] : Vector2.zero);
                if (st.Length > i) { Vector3 td = mt.MultiplyVector(st[i]).normalized; ts.Add(new Vector4(td.x, td.y, td.z, st[i].w)); } else ts.Add(new Vector4(1, 0, 0, 1));
            }
            for (int smi = 0; smi < m.subMeshCount; smi++)
            {
                var l = new List<int>();
                foreach (int ix in m.GetTriangles(smi)) l.Add(ix + baseIdx);
                subs.Add(l);
                lista.Add(smi < sk.sharedMaterials.Length ? sk.sharedMaterials[smi] : null);
            }
            info.Append(sk.name + ":" + m.triangles.Length / 3 + " ");
        }
        var res = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(dstPath) };
        res.indexFormat = vs.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        res.SetVertices(vs); res.SetNormals(ns); res.SetUVs(0, uvs); res.SetTangents(ts);
        res.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) res.SetTriangles(subs[i], i);
        res.RecalculateBounds();
        AssetDatabase.CreateAsset(res, dstPath);
        AssetDatabase.SaveAssets();
        mats = lista.ToArray();
        return info + "| total tris=" + res.triangles.Length / 3 + " bounds=" + res.bounds.size + " min=" + res.bounds.min;
    }

    public static string Ejecutar(string srcPath, string dstPath, int objetivo)
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(srcPath);
        var low = Decimar(src, objetivo);
        AssetDatabase.DeleteAsset(dstPath);
        AssetDatabase.CreateAsset(low, dstPath);
        AssetDatabase.SaveAssets();
        return low.name + " tris=" + low.triangles.Length / 3 + " verts=" + low.vertexCount;
    }
}
