using UnityEngine;

// Sonidos provisionales generados por codigo (sin archivos de audio). Sustituir luego por clips reales:
// basta con cambiar el valor devuelto por cada propiedad.
public static class SonidosPlaceholder
{
    const int Frecuencia = 22050;

    static AudioClip disparo, clic, golpeMetal, impacto, muerte;

    public static AudioClip Disparo { get { if (disparo == null) disparo = Crear("ph_disparo", 0.25f, (t, r) => (Ruido(r) * 0.8f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f) * Mathf.Exp(-t * 20f)); return disparo; } }
    public static AudioClip Clic { get { if (clic == null) clic = Crear("ph_clic", 0.06f, (t, r) => Ruido(r) * Mathf.Exp(-t * 120f) * 0.6f); return clic; } }
    public static AudioClip GolpeMetal { get { if (golpeMetal == null) golpeMetal = Crear("ph_golpe_metal", 0.35f, (t, r) => (Mathf.Sin(2f * Mathf.PI * 520f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 830f * t)) * Mathf.Exp(-t * 14f) * 0.5f); return golpeMetal; } }
    public static AudioClip Impacto { get { if (impacto == null) impacto = Crear("ph_impacto", 0.18f, (t, r) => (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.8f + Ruido(r) * 0.3f) * Mathf.Exp(-t * 22f)); return impacto; } }
    public static AudioClip Muerte { get { if (muerte == null) muerte = Crear("ph_muerte", 0.4f, (t, r) => Mathf.Sin(2f * Mathf.PI * (700f - 1400f * t) * t) * Mathf.Exp(-t * 6f) * 0.5f); return muerte; } }

    static float Ruido(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

    static AudioClip Crear(string nombre, float segundos, System.Func<float, System.Random, float> f)
    {
        int n = Mathf.CeilToInt(segundos * Frecuencia);
        var datos = new float[n];
        var rnd = new System.Random(nombre.GetHashCode());
        for (int i = 0; i < n; i++) datos[i] = Mathf.Clamp(f(i / (float)Frecuencia, rnd), -1f, 1f);
        var clip = AudioClip.Create(nombre, n, 1, Frecuencia, false);
        clip.SetData(datos, 0);
        return clip;
    }

    // Reproduce un clip 3D sencillo en un punto del mundo (sin crear AudioSource permanente).
    public static void EnPunto(AudioClip clip, Vector3 punto, float volumen = 1f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, punto, volumen);
    }
}
