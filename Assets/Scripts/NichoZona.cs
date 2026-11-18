using System.Collections.Generic;
using UnityEngine;

// Volumen de un nicho (escondite). El jugador esta escondido si su cabeza esta dentro del volumen
// en horizontal y por debajo de la altura de agachado. La entidad no entra: el nicho es Not Walkable en el NavMesh.
[RequireComponent(typeof(BoxCollider))]
public class NichoZona : MonoBehaviour
{
    public static readonly List<NichoZona> Todas = new List<NichoZona>();

    private BoxCollider caja;

    void OnEnable()
    {
        caja = GetComponent<BoxCollider>();
        caja.isTrigger = true;
        Todas.Add(this);
    }

    void OnDisable() { Todas.Remove(this); }

    public bool ContieneHorizontal(Vector3 punto)
    {
        Bounds b = caja.bounds;
        return punto.x >= b.min.x && punto.x <= b.max.x && punto.z >= b.min.z && punto.z <= b.max.z;
    }

    public static bool JugadorEscondido(Transform cabeza, float alturaAgachado)
    {
        return NichoDelJugador(cabeza, alturaAgachado) != null;
    }

    // El nicho donde esta escondido el jugador (misma condicion de siempre: cabeza dentro en horizontal y bajo la altura
    // de agachado), o null. Lo usan las puertas y la amenaza (entidad y mini monstruos).
    public static NichoZona NichoDelJugador(Transform cabeza, float alturaAgachado)
    {
        if (cabeza == null || cabeza.position.y > alturaAgachado) return null;
        foreach (var n in Todas)
            if (n != null && n.ContieneHorizontal(cabeza.position)) return n;
        return null;
    }

    // true si la cabeza esta dentro de ESTE nicho y agachada (misma condicion que JugadorEscondido).
    public bool Escondido(Transform cabeza, float alturaAgachado)
    {
        return cabeza != null && cabeza.position.y <= alturaAgachado && ContieneHorizontal(cabeza.position);
    }

    public Vector3 Centro { get { return caja != null ? caja.bounds.center : transform.position; } }
}
