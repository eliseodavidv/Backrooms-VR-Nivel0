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
        if (cabeza == null || cabeza.position.y > alturaAgachado) return false;
        foreach (var n in Todas)
            if (n != null && n.ContieneHorizontal(cabeza.position)) return true;
        return false;
    }
}
