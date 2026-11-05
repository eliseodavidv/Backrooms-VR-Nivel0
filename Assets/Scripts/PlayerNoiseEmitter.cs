using System;
using UnityEngine;

// Detecta los teletransportes del jugador (salto brusco de la cabeza en un solo frame) y los publica como ruido.
[DefaultExecutionOrder(-50)] // antes que GameManager: el teletransporte a la salida se cuenta antes de declarar la victoria
public class PlayerNoiseEmitter : MonoBehaviour
{
    public static event Action<Vector3> Teletransporte;

    [Tooltip("CenterEyeAnchor.")]
    public Transform cabeza;
    [Tooltip("Salto horizontal minimo en un frame para contarlo como teletransporte.")]
    public float umbralSalto = 1f;

    private Vector3 anterior;
    private bool inicializado;

    // Tras reposicionar al jugador por codigo (reinicio de partida): el siguiente salto no cuenta como teletransporte.
    public void Reanclar() { inicializado = false; }

    void Update()
    {
        if (cabeza == null) return;
        Vector3 actual = cabeza.position;
        if (inicializado)
        {
            Vector3 d = actual - anterior;
            d.y = 0f;
            if (d.magnitude >= umbralSalto)
            {
                Debug.Log("[Ruido] Teletransporte a " + actual.ToString("F1"));
                Teletransporte?.Invoke(actual);
            }
        }
        anterior = actual;
        inicializado = true;
    }
}
