using UnityEngine;

/// <summary>
/// Abre automáticamente la puerta principal ("Entrance door" de la fachada) cuando el
/// jugador está dentro de un BoxCollider configurado como trigger frente a la puerta,
/// y la cierra cuando sale de él.
///
/// El giro se hace SOBRE LA BISAGRA, no sobre el centro de la hoja: se usan la posición
/// y el eje Y (up) del Transform "hinge" como eje de rotación. La hoja ("doorLeaf")
/// NO necesita ser hija de la bisagra — el script la reubica cada frame orbitándola
/// alrededor de ese eje, de forma exacta (sin acumular error).
///
/// La presencia del jugador se resuelve con OnTriggerEnter + OnTriggerStay y un pequeño
/// margen de tiempo: así, aunque se pierda un OnTriggerExit (p. ej. tras un recenter de
/// VR que teletransporta el rig), la puerta vuelve a cerrarse igualmente.
///
/// Colocar este componente en el mismo GameObject que lleva el BoxCollider (isTrigger).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DoorAutoOpen : MonoBehaviour
{
    [Header("Pivote (bisagra)")]
    [Tooltip("Transform situado en el borde-bisagra de la hoja. Se usan su POSICIÓN y su eje Y (up) como eje de giro.")]
    public Transform hinge;

    [Tooltip("La hoja de la puerta que gira. Rota alrededor de 'hinge', no de su propio centro.")]
    public Transform doorLeaf;

    [Header("Apertura")]
    [Tooltip("Grados de apertura. Positivo abre hacia el interior de la casa.")]
    public float openAngle = 90f;

    [Tooltip("Velocidad de giro, en grados por segundo (movimiento suave).")]
    public float degreesPerSecond = 140f;

    [Header("Detección del jugador")]
    [Tooltip("Detecta al jugador por su CharacterController (el OVRPlayerController lo tiene).")]
    public bool detectByCharacterController = true;

    [Tooltip("Tag alternativo para reconocer al jugador.")]
    public string playerTag = "Player";

    [Tooltip("Segundos que la puerta sigue considerando presente al jugador tras el último contacto (evita parpadeos y tolera un OnTriggerExit perdido).")]
    public float presenceGrace = 0.25f;

    // Estado de referencia con la puerta CERRADA (capturado al inicio).
    private Quaternion closedLeafRotation;
    private Vector3 closedLeafOffset;   // posición de la hoja relativa a la bisagra

    // Ángulo actual de apertura (0 = cerrada, openAngle = abierta).
    private float currentAngle;

    // Instante del último frame en que el jugador estuvo dentro del trigger.
    private float lastInsideTime = -999f;

    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    private void Awake()
    {
        if (hinge == null || doorLeaf == null)
        {
            Debug.LogWarning("[DoorAutoOpen] Falta asignar 'hinge' o 'doorLeaf' en '" + name + "'. La puerta no se moverá.", this);
            enabled = false;
            return;
        }

        closedLeafRotation = doorLeaf.rotation;
        closedLeafOffset = doorLeaf.position - hinge.position;
        currentAngle = 0f;
    }

    private bool IsPlayer(Collider other)
    {
        if (detectByCharacterController && other.GetComponentInParent<CharacterController>() != null)
            return true;

        if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            return true;

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            lastInsideTime = Time.time;
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayer(other))
            lastInsideTime = Time.time;
    }

    private void Update()
    {
        bool playerNear = (Time.time - lastInsideTime) <= presenceGrace;
        float targetAngle = playerNear ? openAngle : 0f;

        if (Mathf.Approximately(currentAngle, targetAngle))
            return;

        currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, degreesPerSecond * Time.deltaTime);

        // Reconstrucción exacta del giro alrededor del eje de la bisagra.
        Quaternion swing = Quaternion.AngleAxis(currentAngle, hinge.up);
        doorLeaf.rotation = swing * closedLeafRotation;
        doorLeaf.position = hinge.position + swing * closedLeafOffset;
    }

    // Dibuja el trigger y la bisagra en la vista de escena para ubicarlos con facilidad.
    private void OnDrawGizmosSelected()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.20f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }

        if (hinge != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(hinge.position, 0.05f);
            Gizmos.DrawLine(hinge.position - hinge.up * 0.2f, hinge.position + hinge.up * 1.6f);
        }
    }
}
