using UnityEngine;

// Equivalente haptico (RNF-06) de los pasos y la respiracion de la entidad: pulsos suaves en los controladores
// cuya cadencia y fuerza crecen al acercarse. En Persecucion los reemplaza el latido (PursuitFeedback).
public class EntityProximityCue : MonoBehaviour
{
    public Transform jugador;
    public float radio = 12f;
    public float amplitudMaxima = 0.45f;

    private EntityAI ia;
    private float proximoPulso;

    void Awake() { ia = GetComponent<EntityAI>(); }

    void Update()
    {
        if (jugador == null) return;
        if (ia.estadoActual == EntityAI.Estado.Persecucion || ia.estadoActual == EntityAI.Estado.Captura) return;

        Vector3 d = jugador.position - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > radio) return;

        float cercania = 1f - dist / radio;
        if (Time.time < proximoPulso) return;
        proximoPulso = Time.time + Mathf.Lerp(1.2f, 0.4f, cercania);
        ControllerHaptics.Pulse(amplitudMaxima * cercania, 0.08f);
    }
}
