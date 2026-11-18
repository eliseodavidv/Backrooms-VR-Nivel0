using UnityEngine;
using UnityEngine.AI;

// Animacion procedural ligera del monstruo grande. Solo LEE el estado de EntityAI y la velocidad del NavMeshAgent;
// no cambia la IA, el NavMesh ni el combate. Sin Animator: aplica rotaciones aditivas en LateUpdate sobre la pose de reposo.
//
// Modos (campo 'modo'):
//   - Auto: HUESOS si bajo 'raiz' hay huesos con los nombres del rig de Book Head; si no, CUERPO ENTERO.
//   - Huesos: cadera, torso, cuello, cabeza, clavículas, brazos y piernas (paso alternado). Las rotaciones se definen en el
//     espacio del monstruo (adelante/arriba/derecha), asi que no dependen de los ejes locales de cada hueso.
//   - CuerpoEntero: balancea el transform 'cuerpo' entero (para la malla estatica de respaldo 'Modelo_CuerpoEntero').
//
// Volver al cuerpo entero: desactivar 'Modelo_Huesos', activar 'Modelo_CuerpoEntero' (ambos bajo Modelo) y poner modo = CuerpoEntero.
// Animaciones reales: si 'raiz' tiene un Animator con controller, este script se apaga solo (apagarSiHayAnimator).
[DisallowMultipleComponent]
public class EntidadProcedural : MonoBehaviour
{
    public enum Modo { Auto, Huesos, CuerpoEntero }

    [Header("Referencias (se autocompletan si estan vacias)")]
    public EntityAI ia;
    public NavMeshAgent agente;
    [Tooltip("Transform visual del monstruo (el hijo 'Modelo'). En modo cuerpo entero es el que se balancea; en modo huesos recibe un rebote leve.")]
    public Transform cuerpo;
    [Tooltip("Raiz donde buscar los huesos (el hijo 'Modelo_Huesos').")]
    public Transform raiz;

    [Header("Ajustes")]
    public Modo modo = Modo.Auto;
    [Range(0f, 2f)] public float intensidad = 1f;
    [Tooltip("Mas lejos que esto del jugador no se anima (ahorra CPU).")]
    public float distanciaMaxima = 40f;
    [Tooltip("No calcula nada mientras ninguna camara ve al monstruo.")]
    public bool soloSiVisible = true;
    public bool apagarSiHayAnimator = true;

    // ---- huesos (cada uno con su pose de reposo) ----
    class Hueso { public Transform t; public Quaternion reposo; }
    Hueso pelvis, muslosL, muslosR, pantL, pantR, spineBajo, spineAlto, cuello1, cuello2, cabeza, claviL, claviR, brazoL, brazoR, antebL, antebR;
    Hueso[] todos;                    // en orden padre -> hijo (para restaurar la pose)
    bool hayHuesos;
    Renderer[] renderers;

    // ---- cuerpo entero / rebote ----
    Vector3 cuerpoPos0;
    Quaternion cuerpoRot0;

    // ---- estado suavizado ----
    struct Perfil
    {
        public float frecuencia;      // ciclos de paso por segundo a velocidad maxima
        public float irregular;       // 0..1, cuanto varia el ritmo
        public float rebote;          // m (modo cuerpo)
        public float caderaRoll;      // grados
        public float torsoRoll;
        public float inclinacion;     // grados hacia delante
        public float torsoGiro;
        public float brazos;          // grados de balanceo
        public float piernas;         // grados de paso del muslo
        public float rodilla;         // grados de flexion de la pantorrilla
        public float cabezaRoll;      // inclinacion inquietante
        public float cabezaGiro;      // barrido (grados)
        public float cabezaVel;       // velocidad del barrido (rad/s)
        public float cabezaPitch;
    }
    Perfil actual;
    float fase, tBarrido, semilla;
    Transform cam;

    static Perfil Para(EntityAI.Estado e)
    {
        switch (e)
        {
            case EntityAI.Estado.Persecucion:
                return new Perfil { frecuencia = 1.9f, irregular = 0.1f, rebote = 0.07f, caderaRoll = 7f, torsoRoll = 5f, inclinacion = 22f, torsoGiro = 8f, brazos = 42f, piernas = 34f, rodilla = 55f, cabezaRoll = 12f, cabezaGiro = 6f, cabezaVel = 9f, cabezaPitch = -8f };
            case EntityAI.Estado.Sospecha:
                return new Perfil { frecuencia = 0.8f, irregular = 0.4f, rebote = 0.015f, caderaRoll = 2f, torsoRoll = 2f, inclinacion = 6f, torsoGiro = 14f, brazos = 6f, piernas = 14f, rodilla = 20f, cabezaRoll = 14f, cabezaGiro = 70f, cabezaVel = 1.1f, cabezaPitch = 6f };
            case EntityAI.Estado.Busqueda:
                return new Perfil { frecuencia = 1.1f, irregular = 0.35f, rebote = 0.03f, caderaRoll = 4f, torsoRoll = 3f, inclinacion = 9f, torsoGiro = 18f, brazos = 14f, piernas = 20f, rodilla = 28f, cabezaRoll = 8f, cabezaGiro = 90f, cabezaVel = 2.2f, cabezaPitch = 4f };
            case EntityAI.Estado.Captura:
                return new Perfil { frecuencia = 0.5f, irregular = 0.1f, rebote = 0.005f, caderaRoll = 1f, torsoRoll = 1f, inclinacion = 14f, torsoGiro = 0f, brazos = 4f, piernas = 6f, rodilla = 8f, cabezaRoll = 22f, cabezaGiro = 3f, cabezaVel = 3f, cabezaPitch = -12f };
            default: // Patrulla: lento e irregular
                return new Perfil { frecuencia = 0.85f, irregular = 0.75f, rebote = 0.025f, caderaRoll = 5f, torsoRoll = 3f, inclinacion = 3f, torsoGiro = 6f, brazos = 16f, piernas = 20f, rodilla = 30f, cabezaRoll = 10f, cabezaGiro = 22f, cabezaVel = 0.6f, cabezaPitch = 2f };
        }
    }

    void Awake()
    {
        if (ia == null) ia = GetComponentInParent<EntityAI>();
        if (agente == null) agente = GetComponentInParent<NavMeshAgent>();
        if (cuerpo == null) cuerpo = raiz != null ? raiz : transform;
        if (raiz == null) raiz = cuerpo;
        semilla = Random.value * 100f;

        if (apagarSiHayAnimator)
        {
            var an = raiz.GetComponentInChildren<Animator>(true);
            if (an != null && an.runtimeAnimatorController != null) { enabled = false; return; }
        }

        if (modo != Modo.CuerpoEntero) BuscarHuesos();
        if (modo == Modo.Huesos && !hayHuesos) Debug.LogWarning("[EntidadProcedural] modo Huesos pedido pero no hay huesos bajo '" + raiz.name + "'; se usa cuerpo entero.");
        cuerpoPos0 = cuerpo.localPosition;
        cuerpoRot0 = cuerpo.localRotation;
        renderers = raiz.GetComponentsInChildren<Renderer>(false);
        actual = Para(EntityAI.Estado.Patrulla);
    }

    void Start()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) cam = rig.centerEyeAnchor;
        Debug.Log("[EntidadProcedural] Modo " + (hayHuesos ? "HUESOS (" + todos.Length + " huesos animados)" : "CUERPO ENTERO (sin huesos en '" + raiz.name + "')"));
    }

    void OnDisable()
    {
        // Deja la pose de reposo al apagarse.
        if (hayHuesos && todos != null)
        {
            foreach (var h in todos) if (h != null && h.t != null) h.t.localRotation = h.reposo;
            if (cuerpo != null) cuerpo.localPosition = cuerpoPos0;
        }
        else if (cuerpo != null) { cuerpo.localPosition = cuerpoPos0; cuerpo.localRotation = cuerpoRot0; }
    }

    // ---------- huesos ----------

    Hueso Buscar(string nombre)
    {
        foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            if (t.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase)) return new Hueso { t = t, reposo = t.localRotation };
        return null;
    }

    void BuscarHuesos()
    {
        pelvis = Buscar("pelvis");
        muslosL = Buscar("thigh_l"); muslosR = Buscar("thigh_r");
        pantL = Buscar("calf_l"); pantR = Buscar("calf_r");
        spineBajo = Buscar("spine_02");
        spineAlto = Buscar("spine_04");
        cuello1 = Buscar("neck_01"); cuello2 = Buscar("neck_02");
        cabeza = Buscar("head");
        claviL = Buscar("clavicle_l"); claviR = Buscar("clavicle_r");
        brazoL = Buscar("upperarm_l"); brazoR = Buscar("upperarm_r");
        antebL = Buscar("lowerarm_l"); antebR = Buscar("lowerarm_r");

        var lista = new System.Collections.Generic.List<Hueso>();
        foreach (var h in new[] { pelvis, muslosL, muslosR, pantL, pantR, spineBajo, spineAlto, cuello1, cuello2, cabeza, claviL, claviR, brazoL, brazoR, antebL, antebR })
            if (h != null) lista.Add(h);
        todos = lista.ToArray();
        // Hace falta al menos cadera o torso, mas la cabeza.
        hayHuesos = todos.Length >= 4 && (pelvis != null || spineBajo != null) && cabeza != null;
        if (!hayHuesos) todos = new Hueso[0];
    }

    // Rota el hueso 'h' (sobre su pose de reposo) en el espacio del monstruo: pitch eje derecha, yaw eje arriba, roll eje adelante.
    // El padre ya esta movido (se procesa en orden padre -> hijo), asi que no importan los ejes locales del hueso.
    void Aplicar(Hueso h, float pitch, float yaw, float roll)
    {
        if (h == null) return;
        Quaternion mundoReposo = h.t.parent.rotation * h.reposo;
        Quaternion d = Quaternion.AngleAxis(yaw, transform.up) * Quaternion.AngleAxis(pitch, transform.right) * Quaternion.AngleAxis(roll, transform.forward);
        h.t.rotation = d * mundoReposo;
    }

    bool Visible()
    {
        if (renderers == null) return true;
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && renderers[i].isVisible) return true;
        return false;
    }

    // ---------- bucle ----------

    void LateUpdate()
    {
        if (ia == null) return;
        if (cam != null && (cam.position - transform.position).sqrMagnitude > distanciaMaxima * distanciaMaxima) return;
        if (soloSiVisible && !Visible()) return;

        float dt = Time.deltaTime;
        float vMax = Mathf.Max(0.1f, ia.velocidadPersecucion);
        float vel = agente != null && agente.enabled ? agente.velocity.magnitude : 0f;
        float marcha = Mathf.Clamp01(vel / vMax * 1.6f);            // 0 parado .. 1 a toda velocidad

        // Suaviza el perfil hacia el del estado actual (transiciones sin saltos).
        Perfil o = Para(ia.estadoActual);
        float k = 1f - Mathf.Exp(-4f * dt);
        actual = new Perfil
        {
            frecuencia = Mathf.Lerp(actual.frecuencia, o.frecuencia, k),
            irregular = Mathf.Lerp(actual.irregular, o.irregular, k),
            rebote = Mathf.Lerp(actual.rebote, o.rebote, k),
            caderaRoll = Mathf.Lerp(actual.caderaRoll, o.caderaRoll, k),
            torsoRoll = Mathf.Lerp(actual.torsoRoll, o.torsoRoll, k),
            inclinacion = Mathf.Lerp(actual.inclinacion, o.inclinacion, k),
            torsoGiro = Mathf.Lerp(actual.torsoGiro, o.torsoGiro, k),
            brazos = Mathf.Lerp(actual.brazos, o.brazos, k),
            piernas = Mathf.Lerp(actual.piernas, o.piernas, k),
            rodilla = Mathf.Lerp(actual.rodilla, o.rodilla, k),
            cabezaRoll = Mathf.Lerp(actual.cabezaRoll, o.cabezaRoll, k),
            cabezaGiro = Mathf.Lerp(actual.cabezaGiro, o.cabezaGiro, k),
            cabezaVel = Mathf.Lerp(actual.cabezaVel, o.cabezaVel, k),
            cabezaPitch = Mathf.Lerp(actual.cabezaPitch, o.cabezaPitch, k)
        };

        // Fase del paso: ritmo irregular (se acelera y se arrastra) segun ruido.
        float ruido = Mathf.PerlinNoise(Time.time * 0.7f, semilla) * 2f - 1f;
        fase += dt * Mathf.PI * 2f * actual.frecuencia * marcha * (1f + ruido * actual.irregular * 0.6f);
        tBarrido += dt * actual.cabezaVel;

        float s = Mathf.Sin(fase);
        float c = Mathf.Cos(fase);
        float respira = Mathf.Sin(Time.time * 1.3f + semilla);                       // siempre, incluso parado
        float temblor = Mathf.PerlinNoise(Time.time * 2.1f, semilla + 7f) * 2f - 1f;  // cabeza inquieta
        // Barrido de cabeza: vaiven con pausas en los extremos + ruido.
        float barrido = Mathf.Sin(tBarrido + semilla);
        barrido = Mathf.Sign(barrido) * Mathf.Pow(Mathf.Abs(barrido), 0.6f);
        float amp = intensidad;

        float incl = actual.inclinacion * marcha + actual.inclinacion * 0.25f * (1f - marcha);
        float giroCabeza = (actual.cabezaGiro * barrido + temblor * 6f) * amp;
        float rollCabeza = (actual.cabezaRoll * (0.6f * temblor + 0.4f * respira)) * amp;

        if (hayHuesos)
        {
            // Orden padre -> hijo. Piernas: paso alternado (muslo L atras cuando R adelante); la pantorrilla se dobla al
            // adelantar la pierna. Signo: pitch positivo lleva un miembro colgante hacia atras.
            float pasoP = actual.piernas * marcha * amp;
            float flexL = Mathf.Max(0f, -c), flexR = Mathf.Max(0f, c);
            float rod = actual.rodilla * marcha * amp;

            Aplicar(pelvis, 0f, s * actual.torsoGiro * 0.4f * marcha * amp, s * actual.caderaRoll * marcha * amp);
            Aplicar(muslosL, s * pasoP, 0f, 0f);
            Aplicar(muslosR, -s * pasoP, 0f, 0f);
            Aplicar(pantL, rod * flexL + 4f * marcha * amp, 0f, 0f);
            Aplicar(pantR, rod * flexR + 4f * marcha * amp, 0f, 0f);

            Aplicar(spineBajo, (incl * 0.5f + respira * 1.2f) * amp, -s * actual.torsoGiro * 0.5f * marcha * amp, -s * actual.torsoRoll * marcha * amp);
            Aplicar(spineAlto, incl * 0.5f * amp, giroCabeza * 0.25f, c * actual.torsoRoll * 0.5f * marcha * amp);
            Aplicar(cuello1, (actual.cabezaPitch * 0.3f + temblor * 3f) * amp, giroCabeza * 0.25f, rollCabeza * 0.3f);
            Aplicar(cuello2, (actual.cabezaPitch * 0.3f + temblor * 2f) * amp, giroCabeza * 0.25f, rollCabeza * 0.3f);
            Aplicar(cabeza, (actual.cabezaPitch * 0.4f - incl * 0.6f) * amp, giroCabeza * 0.5f, rollCabeza * 0.4f);

            // brazos colgando: balanceo opuesto a la pierna del mismo lado + inercia suave
            float sw = actual.brazos * marcha * amp;
            float cuelgue = 4f * respira * amp;
            Aplicar(claviL, 0f, 0f, 3f * respira * amp);
            Aplicar(claviR, 0f, 0f, -3f * respira * amp);
            Aplicar(brazoL, -s * sw + cuelgue, 0f, 2f * temblor * amp);
            Aplicar(brazoR, s * sw - cuelgue, 0f, -2f * temblor * amp);
            Aplicar(antebL, (-s * sw * 0.5f) - 8f * marcha * amp, 0f, 0f);
            Aplicar(antebR, (s * sw * 0.5f) - 8f * marcha * amp, 0f, 0f);

            // peso al caminar: leve subida y bajada del cuerpo en cada paso
            cuerpo.localPosition = cuerpoPos0 + Vector3.up * (Mathf.Abs(s) * actual.rebote * 0.5f * marcha * amp);
        }
        else
        {
            // Cuerpo entero: rebote a cada paso (dos por ciclo), balanceo lateral, inclinacion y giro de torso.
            float rebote = Mathf.Abs(s) * actual.rebote * marcha * amp + respira * 0.004f * amp;
            float pitch = (incl + c * 1.5f * marcha) * amp;
            float yaw = (s * actual.torsoGiro * 0.6f * marcha + giroCabeza * 0.15f) * amp;
            float roll = (s * actual.caderaRoll * marcha + rollCabeza * 0.25f) * amp;
            cuerpo.localPosition = cuerpoPos0 + Vector3.up * rebote;
            cuerpo.localRotation = cuerpoRot0 * Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
