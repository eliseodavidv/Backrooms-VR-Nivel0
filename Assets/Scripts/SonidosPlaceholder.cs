using UnityEngine;

// Sonidos provisionales generados por codigo (sin archivos de audio), pensados para sonar creibles hasta tener audios reales.
// Cada propiedad devuelve PRIMERO el audio real de la BibliotecaSonidos (si pusiste el archivo en Assets/Audio/Reales/) y,
// si no hay, el generado. Asi se reemplazan solos. Los generados se crean una vez (Precalentar() los prepara en el menu).
public static class SonidosPlaceholder
{
    const int Fs = 22050;

    static AudioClip disparo, clic, golpeMetal, impacto, muerte, mordisco, latido, teletransporte, drone, victoria, derrota, zumbidoLuz, chasquidoLuz;
    static AudioClip[] susurros;

    static BibliotecaSonidos B { get { return BibliotecaSonidos.Instancia; } }

    // ---------- combate (como antes; ahora con reemplazo por audio real) ----------
    public static AudioClip Disparo { get { var r = B != null ? B.disparo : null; if (r != null) { var p = Procesar("disparo", r, -1f, 1.0f, 0.8f, 0.15f); if (p != null) return p; } if (disparo == null) disparo = Crear("ph_disparo", 0.25f, (t, rn) => (Ruido(rn) * 0.8f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f) * Mathf.Exp(-t * 20f)); return disparo; } }
    public static AudioClip Clic { get { var r = B != null ? B.clicArma : null; if (r != null) return r; if (clic == null) clic = Crear("ph_clic", 0.06f, (t, rn) => Ruido(rn) * Mathf.Exp(-t * 120f) * 0.6f); return clic; } }
    public static AudioClip GolpeMetal { get { var r = B != null ? B.golpeBarra : null; if (r != null) { var p = Procesar("golpe_barra", r, -1f, 1.2f, 0.7f, 0.35f); if (p != null) return p; } if (golpeMetal == null) golpeMetal = Crear("ph_golpe_metal", 0.35f, (t, rn) => (Mathf.Sin(2f * Mathf.PI * 520f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 830f * t)) * Mathf.Exp(-t * 14f) * 0.5f); return golpeMetal; } }
    public static AudioClip Impacto { get { var r = B != null ? B.impactoMini : null; if (r != null) return r; if (impacto == null) impacto = Crear("ph_impacto", 0.18f, (t, rn) => (Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.8f + Ruido(rn) * 0.3f) * Mathf.Exp(-t * 22f)); return impacto; } }
    public static AudioClip Muerte { get { var r = B != null ? B.muerteMini : null; if (r != null) return r; var mm = B != null ? B.miniMonstruo : null; if (mm != null) { var p = Procesar("mini_muerte", mm, 8.35f, 0.7f, 0.8f, 0.25f); if (p != null) return p; } if (muerte == null) muerte = Crear("ph_muerte", 0.4f, (t, rn) => Mathf.Sin(2f * Mathf.PI * (700f - 1400f * t) * t) * Mathf.Exp(-t * 6f) * 0.5f); return muerte; } }
    // Mordisco al jugador: si no hay audio real, se usa el impacto generado.
    public static AudioClip Mordisco { get { var r = B != null ? B.mordisco : null; if (r != null) return r; return Impacto; } }

    // ---------- nuevos, bien trabajados ----------
    public static AudioClip Latido { get { var r = B != null ? B.latido : null; if (r != null) { var p = Procesar("latido", r, 1.90f, 0.56f, 0.9f, 0.12f, 130f, 7f, 2.5f); if (p != null) return p; } if (latido == null) latido = CrearLatido(); return latido; } }
    public static AudioClip Teletransporte { get { var r = B != null ? B.teletransporte : null; if (r != null) return r; if (teletransporte == null) teletransporte = CrearWhoosh(); return teletransporte; } }
    public static AudioClip Drone { get { var r = B != null ? B.droneFondo : null; if (r != null) return r; if (drone == null) drone = CrearDrone(); return drone; } }
    public static AudioClip Victoria { get { var r = B != null ? B.victoria : null; if (r != null) return r; if (victoria == null) victoria = CrearVictoria(); return victoria; } }
    public static AudioClip Derrota { get { var r = B != null ? B.derrota : null; if (r != null) return r; if (derrota == null) derrota = CrearDerrota(); return derrota; } }
    public static AudioClip ZumbidoLuz { get { var r = B != null ? B.zumbidoLuz : null; if (r != null) { var p = ProcesarBucle("zumbido_luz", r, 0.5f, 0.16f); if (p != null) return p; } if (zumbidoLuz == null) zumbidoLuz = CrearZumbidoLuz(); return zumbidoLuz; } }
    public static AudioClip ChasquidoLuz { get { var r = B != null ? B.chasquidoLuz : null; if (r != null) return r; if (chasquidoLuz == null) chasquidoLuz = CrearChasquido(); return chasquidoLuz; } }

    public static AudioClip[] Susurros
    {
        get
        {
            if (B != null && B.susurros != null)
            {
                int n = 0;
                foreach (var c in B.susurros) if (c != null) n++;
                if (n > 0)
                {
                    var reales = new AudioClip[n];
                    int k = 0;
                    foreach (var c in B.susurros) if (c != null) reales[k++] = c;
                    return reales;
                }
            }
            if (susurros == null || susurros.Length == 0 || susurros[0] == null)
                susurros = new[] { CrearSusurro(101, 1.7f), CrearSusurro(202, 2.1f), CrearSusurro(303, 1.5f) };
            return susurros;
        }
    }

    // ---------- sonidos reales sin version generada (si no hay archivo: null y simplemente no suena) ----------
    // Rugido de la entidad al detectar al jugador.
    public static AudioClip Rugido { get { var r = B != null ? B.rugidoEntidad : null; return r != null ? Procesar("rugido_entidad", r, -1f, 4.3f, 0.85f, 0.5f) : null; } }
    // Grito corto del mini monstruo al morder (un segmento del archivo mini_monstruo).
    public static AudioClip MiniAtaque { get { var r = B != null ? B.miniMonstruo : null; return r != null ? Procesar("mini_ataque", r, 1.28f, 0.47f, 0.7f, 0.08f) : null; } }
    // Grito del jugador al ser devorado.
    public static AudioClip GritoMuerte { get { var r = B != null ? B.gritoMuerte : null; return r != null ? Procesar("grito_muerte", r, -1f, 1.8f, 0.9f, 0.15f) : null; } }
    // El drone real es largo (112 s) y va comprimido: no se procesa; su volumen se ajusta con esta ganancia (1 = generado).
    public const float GananciaDroneReal = 1f;
    public static float GananciaDrone { get { return (B != null && B.droneFondo != null) ? GananciaDroneReal : 1f; } }

    // ---------- solo audio real (no hay generado: si no hay archivo, el script usa su clip de siempre) ----------
    public static bool ZumbidoLuzReal { get { return B != null && B.zumbidoLuz != null; } }
    public static AudioClip PasosEntidad { get { return B != null ? B.pasosEntidad : null; } }
    public static AudioClip RespiracionEntidad { get { return B != null ? B.respiracionEntidad : null; } }
    public static AudioClip ZumbidoSalida { get { return B != null ? B.zumbidoSalida : null; } }
    public static AudioClip UiClick { get { return B != null ? B.uiClick : null; } }
    public static AudioClip UiHover { get { return B != null ? B.uiHover : null; } }
    public static AudioClip MusicaPersecucion { get { return B != null ? B.musicaPersecucion : null; } }

    // Genera todo de una vez (en el menu) para que no haya tirones al sonar por primera vez en la partida.
    public static void Precalentar()
    {
        var a = Disparo; a = Clic; a = GolpeMetal; a = Impacto; a = Muerte; a = Mordisco; a = Latido; a = Teletransporte;
        a = Drone; a = Victoria; a = Derrota; a = ZumbidoLuz; a = ChasquidoLuz;
        var s = Susurros;
        a = Rugido; a = MiniAtaque; a = GritoMuerte;
    }

    // Reproduce un clip 3D en un punto del mundo con el espacializador de Meta y el grupo SFX del mixer.
    public static void EnPunto(AudioClip clip, Vector3 punto, float volumen = 1f)
    {
        Sonido3D.EnPunto(clip, punto, volumen, BusAudio.SFX);
    }

    // ================= procesado de audios reales =================

    static readonly System.Collections.Generic.Dictionary<string, AudioClip> procesados = new System.Collections.Generic.Dictionary<string, AudioClip>();

    // Prepara un audio real CORTO: mezcla a mono, recorta (inicio < 0 = quita el silencio inicial; duracion = largo maximo, y
    // tambien corta la cola silenciosa), fundido de salida, filtro de graves, saturacion suave y pico normalizado.
    // Devuelve null si el clip aun no esta cargado (el llamador usa el generado y lo reintenta despues).
    static AudioClip Procesar(string clave, AudioClip real, float inicio, float duracion, float pico, float fundidoSalida, float pasaAltosHz = 0f, float saturacion = 0f, float excitador = 0f)
    {
        if (real == null) return null;
        AudioClip hecho;
        if (procesados.TryGetValue(clave, out hecho) && hecho != null) return hecho;
        if (real.loadState != AudioDataLoadState.Loaded) { real.LoadAudioData(); if (real.loadState != AudioDataLoadState.Loaded) return null; }
        int ch = real.channels, fs = real.frequency, total = real.samples;
        var datos = new float[total * ch];
        if (!real.GetData(datos, 0)) return null;
        var m = new float[total];
        float max = 0f;
        for (int i = 0; i < total; i++)
        {
            float acc = 0f;
            for (int k = 0; k < ch; k++) acc += datos[i * ch + k];
            m[i] = acc / ch;
            max = Mathf.Max(max, Mathf.Abs(m[i]));
        }
        if (max < 0.0001f) return null;
        float umbral = max * 0.03f;
        int i0;
        if (inicio >= 0f) i0 = Mathf.Clamp((int)(inicio * fs), 0, total - 1);
        else { i0 = 0; while (i0 < total - 1 && Mathf.Abs(m[i0]) < umbral) i0++; i0 = Mathf.Max(0, i0 - fs / 100); }
        int fin = total;
        if (duracion > 0f) fin = Mathf.Min(total, i0 + (int)(duracion * fs));
        int ultimo = fin - 1;
        while (ultimo > i0 && Mathf.Abs(m[ultimo]) < umbral) ultimo--;
        fin = Mathf.Min(fin, ultimo + fs / 12);                       // deja ~80 ms de cola
        int n = Mathf.Max(1, fin - i0);
        var o = new float[n];
        System.Array.Copy(m, i0, o, 0, n);

        int fIn = Mathf.Min(n, fs / 500), fOut = Mathf.Min(n, Mathf.Max(1, (int)(fundidoSalida * fs)));
        for (int i = 0; i < fIn; i++) o[i] *= i / (float)fIn;
        for (int i = 0; i < fOut; i++) o[n - 1 - i] *= i / (float)fOut;
        // 1) Saturacion fuerte: un golpe muy grave genera armonicos en frecuencias medias (150-500 Hz), que SI reproducen los
        //    altavoces del Quest. 2) Luego el filtro de graves (2 etapas) quita el sub-grave que no se oiria de todos modos.
        if (saturacion > 0f)
        {
            float norm = (float)System.Math.Tanh(saturacion);
            float mx = 0f;
            for (int i = 0; i < n; i++) mx = Mathf.Max(mx, Mathf.Abs(o[i]));
            if (mx > 0f) for (int i = 0; i < n; i++) o[i] = (float)System.Math.Tanh(o[i] / mx * saturacion) / norm * mx;
        }
        if (pasaAltosHz > 0f)
        {
            float a = 1f / (1f + 2f * Mathf.PI * pasaAltosHz / fs);
            for (int etapa = 0; etapa < 2; etapa++)
            {
                float yPrev = 0f, xPrev = 0f;
                for (int i = 0; i < n; i++) { float x = o[i]; float y = a * (yPrev + x - xPrev); xPrev = x; yPrev = y; o[i] = y; }
            }
        }
        // Excitador: un golpe seco en frecuencias medias (170 / 340 / 510 Hz) que sigue la envolvente del propio sonido. Conserva el
        // ritmo del latido real y le da cuerpo en los altavoces pequenos del Quest.
        if (excitador > 0f)
        {
            float ex = 0f, mx2 = 0f;
            for (int i = 0; i < n; i++) mx2 = Mathf.Max(mx2, Mathf.Abs(o[i]));
            float subida = 1f - Mathf.Exp(-1f / (0.002f * fs)), bajada = 1f - Mathf.Exp(-1f / (0.035f * fs));
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Abs(o[i]);
                ex += (a > ex ? subida : bajada) * (a - ex);
                float t = i / (float)fs;
                float tono = Mathf.Sin(2f * Mathf.PI * 170f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 340f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 510f * t);
                o[i] += excitador * ex * tono;
            }
        }
        float pk = 0f;
        for (int i = 0; i < n; i++) pk = Mathf.Max(pk, Mathf.Abs(o[i]));
        if (pk > 0.0001f) { float g = pico / pk; for (int i = 0; i < n; i++) o[i] *= g; }

        var clip = AudioClip.Create("real_" + clave, n, 1, fs, false);
        clip.SetData(o, 0);
        procesados[clave] = clip;
        return clip;
    }

    // Prepara un bucle real de duracion media (zumbido de luz): mono, volumen parejo por RMS y empalme sin salto:
    // los ultimos "cruce" segundos se funden con el inicio, asi al repetirse no suena el corte.
    static AudioClip ProcesarBucle(string clave, AudioClip real, float cruceSeg, float rmsObjetivo)
    {
        if (real == null) return null;
        AudioClip hecho;
        if (procesados.TryGetValue(clave, out hecho) && hecho != null) return hecho;
        if (real.loadState != AudioDataLoadState.Loaded) { real.LoadAudioData(); if (real.loadState != AudioDataLoadState.Loaded) return null; }
        int ch = real.channels, fs = real.frequency, total = real.samples;
        var datos = new float[total * ch];
        if (!real.GetData(datos, 0)) return null;
        var m = new float[total];
        for (int i = 0; i < total; i++)
        {
            float acc = 0f;
            for (int k = 0; k < ch; k++) acc += datos[i * ch + k];
            m[i] = acc / ch;
        }
        int xf = Mathf.Min((int)(cruceSeg * fs), total / 3);
        int largo = total - xf;
        var o = new float[largo];
        for (int i = 0; i < largo; i++) o[i] = m[i];
        for (int i = 0; i < xf; i++)
        {
            float w = (i / (float)xf) * Mathf.PI * 0.5f;
            o[i] = m[i] * Mathf.Sin(w) + m[largo + i] * Mathf.Cos(w);   // potencia constante
        }
        double e = 0; float pk = 0f;
        for (int i = 0; i < largo; i++) { e += o[i] * o[i]; pk = Mathf.Max(pk, Mathf.Abs(o[i])); }
        float rms = Mathf.Sqrt((float)(e / largo));
        if (rms < 0.0001f) return null;
        float gan = Mathf.Min(rmsObjetivo / rms, 0.9f / Mathf.Max(0.0001f, pk));
        for (int i = 0; i < largo; i++) o[i] *= gan;
        var clip = AudioClip.Create("real_" + clave, largo, 1, fs, false);
        clip.SetData(o, 0);
        procesados[clave] = clip;
        return clip;
    }

    // ================= sintesis =================

    static float Ruido(System.Random r) { return (float)(r.NextDouble() * 2.0 - 1.0); }

    static float Suave(float a, float b, float x) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x)); }

    static AudioClip Crear(string nombre, float segundos, System.Func<float, System.Random, float> f)
    {
        int n = Mathf.CeilToInt(segundos * Fs);
        var datos = new float[n];
        var rnd = new System.Random(nombre.GetHashCode());
        for (int i = 0; i < n; i++) datos[i] = Mathf.Clamp(f(i / (float)Fs, rnd), -1f, 1f);
        var clip = AudioClip.Create(nombre, n, 1, Fs, false);
        clip.SetData(datos, 0);
        return clip;
    }

    static float[] Buf(float segundos) { return new float[Mathf.CeilToInt(segundos * Fs)]; }

    static AudioClip Clip(string nombre, float[] d, float pico)
    {
        float m = 0f;
        for (int i = 0; i < d.Length; i++) { float a = Mathf.Abs(d[i]); if (a > m) m = a; }
        if (m > 0.0001f) { float k = pico / m; for (int i = 0; i < d.Length; i++) d[i] *= k; }
        var c = AudioClip.Create(nombre, d.Length, 1, Fs, false);
        c.SetData(d, 0);
        return c;
    }

    // Filtro pasa-banda de estado variable (barato). fc en Hz, q: 0,3 muy ancho ... 1 estrecho.
    class Banda
    {
        float low, band;
        public float Procesar(float x, float fc, float q)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(fc, Fs * 0.45f) / Fs);
            low += f * band;
            float high = x - low - q * band;
            band += f * high;
            return band;
        }
    }

    // Ecos cortos tipo sala (peines con realimentacion): da cola de reverb a los sonidos breves.
    static void Cola(float[] d, float[] retardosSeg, float realimentacion, float mezcla)
    {
        var salida = (float[])d.Clone();
        foreach (float s in retardosSeg)
        {
            int dl = Mathf.RoundToInt(s * Fs);
            var buf = new float[d.Length];
            for (int i = 0; i < d.Length; i++)
            {
                float previo = i >= dl ? buf[i - dl] : 0f;
                buf[i] = d[i] + previo * realimentacion;
                salida[i] += (buf[i] - d[i]) * mezcla;
            }
        }
        System.Array.Copy(salida, d, d.Length);
    }

    // Latido "lub-dub" de 0,46 s que se oye en los altavoces del Quest: golpe grave + golpe seco en medios (190 y 420 Hz)
    // + un clic corto de ruido, con saturacion suave. Corto y seco: no suena a musica.
    static AudioClip CrearLatido()
    {
        var d = Buf(0.46f);
        var rnd = new System.Random(11);
        float[] inicios = { 0f, 0.19f };
        float[] amps = { 1f, 0.72f };
        for (int b = 0; b < 2; b++)
        {
            int i0 = (int)(inicios[b] * Fs);
            int n = (int)(0.24f * Fs);
            float ph = 0f, lp = 0f;
            for (int i = 0; i < n && i0 + i < d.Length; i++)
            {
                float t = i / (float)Fs;
                float f = 46f + 60f * Mathf.Exp(-t * 55f);
                ph += 2f * Mathf.PI * f / Fs;
                float thump = Mathf.Sin(ph) * Mathf.Exp(-t * 30f);
                // Golpe seco en medios (190 / 380 / 620 Hz): es lo que sobrevive en los altavoces pequenos del Quest.
                float knock = (Mathf.Sin(2f * Mathf.PI * 190f * t) + 0.7f * Mathf.Sin(2f * Mathf.PI * 380f * t) + 0.45f * Mathf.Sin(2f * Mathf.PI * 620f * t)) * Mathf.Exp(-t * 48f);
                lp += 0.45f * (Ruido(rnd) - lp);
                float click = lp * Mathf.Exp(-t * 260f);
                d[i0 + i] += amps[b] * (0.30f * thump + 1.5f * knock + 1.0f * click);
            }
        }
        for (int i = 0; i < d.Length; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.9);
        return Clip("ph_latido2", d, 0.9f);
    }

    // Whoosh suave de teletransporte + pisada amortiguada al llegar (alfombra).
    static AudioClip CrearWhoosh()
    {
        var d = Buf(0.6f);
        var rnd = new System.Random(21);
        var banda = new Banda();
        float lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Fs;
            float u = Mathf.Clamp01(t / 0.5f);
            float fc = 250f + 1250f * Mathf.Sin(Mathf.PI * u);
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);
            float s = banda.Procesar(Ruido(rnd), fc, 0.6f) * env * 2.0f;
            if (t >= 0.38f)
            {
                float tt = t - 0.38f;
                lp += 0.08f * (Ruido(rnd) - lp);
                s += (Mathf.Sin(2f * Mathf.PI * 95f * tt) * Mathf.Exp(-tt * 35f) * 0.45f + lp * 3f * Mathf.Exp(-tt * 50f) * 0.35f);
            }
            d[i] = s;
        }
        return Clip("ph_whoosh", d, 0.6f);
    }

    // Susurro: ruido filtrado en dos formantes, troceado en "silabas" suaves de duracion y tono distintos.
    static AudioClip CrearSusurro(int semilla, float segundos)
    {
        var d = Buf(segundos);
        var rnd = new System.Random(semilla);
        var env = new float[d.Length];
        var fcs = new float[d.Length];
        float t = 0.12f;
        int silabas = rnd.Next(4, 8);
        for (int s = 0; s < silabas; s++)
        {
            float dur = 0.09f + (float)rnd.NextDouble() * 0.14f;
            float amp = 0.5f + (float)rnd.NextDouble() * 0.5f;
            float fc = 1500f + (float)rnd.NextDouble() * 1500f;
            int i0 = (int)(t * Fs), n = (int)(dur * Fs);
            for (int i = 0; i < n && i0 + i < d.Length; i++)
            {
                float bump = Mathf.Pow(Mathf.Sin(Mathf.PI * i / n), 2f) * amp;
                if (bump > env[i0 + i]) { env[i0 + i] = bump; fcs[i0 + i] = fc; }
            }
            t += dur + 0.04f + (float)rnd.NextDouble() * 0.16f;
            if (t > segundos - 0.2f) break;
        }
        var f1 = new Banda();
        var f2 = new Banda();
        float aire = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float x = Ruido(rnd);
            float fc = fcs[i] > 0f ? fcs[i] : 2000f;
            aire += 0.05f * (x - aire);
            float s = f1.Procesar(x, fc, 0.35f) * 0.8f + f2.Procesar(x, fc * 1.7f + 200f, 0.5f) * 0.5f;
            d[i] = (s + aire * 0.4f) * env[i];
        }
        return Clip("ph_susurro_" + semilla, d, 0.55f);
    }

    // Drone grave de 8 s en bucle perfecto (todas las frecuencias dan ciclos exactos en 8 s) con armonicos para que se
    // oiga en los altavoces del Quest, mas un rumor grave que se funde consigo mismo al repetirse.
    static AudioClip CrearDrone()
    {
        var d = Buf(8f);
        int n = d.Length;
        float[] fr = { 41f, 41.25f, 61.75f, 82.5f, 123.5f, 165f };
        float[] am = { 1f, 0.8f, 0.5f, 0.4f, 0.3f, 0.15f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Fs;
            float lfo = (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 8f + 0.5f)) * (0.92f + 0.08f * Mathf.Sin(2f * Mathf.PI * t / 4f));
            float s = 0f;
            for (int k = 0; k < fr.Length; k++) s += am[k] * Mathf.Sin(2f * Mathf.PI * fr[k] * t);
            d[i] = s * lfo * 0.22f;
        }
        int xf = Fs / 2;
        var rnd = new System.Random(31);
        var ruido = new float[n + xf];
        float lp = 0f;
        for (int i = 0; i < ruido.Length; i++) { lp += 0.012f * (Ruido(rnd) - lp); ruido[i] = lp * 14f; }
        for (int i = 0; i < n; i++)
        {
            float w = i < xf ? i / (float)xf : 1f;
            float v = ruido[i] * w + (i < xf ? ruido[n + i] * (1f - w) : 0f);
            d[i] += v * 0.35f;
        }
        return Clip("ph_drone", d, 0.6f);
    }

    // Victoria: arpegio ascendente de campanas suaves (La menor/mayor luminosa) con cola de sala. 1,9 s.
    static AudioClip CrearVictoria()
    {
        var d = Buf(1.9f);
        float[] notas = { 220f, 329.63f, 440f, 554.37f, 659.25f };
        float[] inicios = { 0f, 0.14f, 0.28f, 0.42f, 0.56f };
        for (int k = 0; k < notas.Length; k++)
        {
            int i0 = (int)(inicios[k] * Fs);
            for (int i = 0; i0 + i < d.Length; i++)
            {
                float t = i / (float)Fs;
                float env = (1f - Mathf.Exp(-t * 180f)) * Mathf.Exp(-t * 2.6f);
                float f = notas[k];
                float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * 2f * f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 3f * f * t);
                d[i0 + i] += s * env * 0.5f;
            }
        }
        Cola(d, new[] { 0.043f, 0.071f, 0.097f }, 0.45f, 0.5f);
        return Clip("ph_victoria", d, 0.6f);
    }

    // Derrota: golpe sordo que cae de tono, racimo disonante que se hunde y una cola oscura. 2,2 s.
    static AudioClip CrearDerrota()
    {
        var d = Buf(2.2f);
        var rnd = new System.Random(41);
        float ph = 0f, lp = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Fs;
            float f = 38f + 45f * Mathf.Exp(-t * 3.2f);
            ph += 2f * Mathf.PI * f / Fs;
            float boom = Mathf.Sin(ph) * Mathf.Exp(-t * 1.7f);
            float pf = Mathf.Pow(2f, -t * 0.12f);
            float racimo = (Mathf.Sin(2f * Mathf.PI * 110f * pf * t) + Mathf.Sin(2f * Mathf.PI * 116.54f * pf * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 164.81f * pf * t))
                           * 0.28f * Mathf.Exp(-t * 0.9f) * (1f - Mathf.Exp(-t * 30f));
            lp += 0.2f * (Ruido(rnd) - lp);
            float golpe = lp * Mathf.Exp(-t * 35f) * 1.6f;
            d[i] = boom + racimo + golpe;
        }
        Cola(d, new[] { 0.057f, 0.083f, 0.121f }, 0.5f, 0.6f);
        return Clip("ph_derrota", d, 0.7f);
    }

    // Zumbido de balasto de 2 s en bucle perfecto: 120 Hz con armonicos (buzz) + un silbido agudo muy leve + temblor de 3 Hz.
    static AudioClip CrearZumbidoLuz()
    {
        var d = Buf(2f);
        float[] am = { 1f, 0.55f, 0.4f, 0.25f, 0.2f, 0.12f, 0.1f, 0.06f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Fs;
            float s = 0f;
            for (int h = 0; h < am.Length; h++) s += am[h] * Mathf.Sin(2f * Mathf.PI * 120f * (h + 1) * t);
            s += 0.05f * Mathf.Sin(2f * Mathf.PI * 6000f * t);
            d[i] = s * (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * 3f * t));
        }
        return Clip("ph_zumbido_luz", d, 0.5f);
    }

    // Chasquido del tubo al apagarse / encenderse: clic de ruido + golpe sordo + "tink" metalico.
    static AudioClip CrearChasquido()
    {
        var d = Buf(0.12f);
        var rnd = new System.Random(51);
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Fs;
            float s = Ruido(rnd) * Mathf.Exp(-t * 1400f) * 0.9f
                    + Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 140f) * 0.6f
                    + Mathf.Sin(2f * Mathf.PI * 2300f * t) * Mathf.Exp(-t * 300f) * 0.3f;
            d[i] = s;
        }
        return Clip("ph_chasquido_luz", d, 0.8f);
    }
}
