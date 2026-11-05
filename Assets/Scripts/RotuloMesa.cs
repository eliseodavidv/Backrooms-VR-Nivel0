using UnityEngine;

// Cartel sobre una mesa: siempre de cara al jugador (solo gira en Y) para que se lea desde cualquier lado.
public class RotuloMesa : MonoBehaviour
{
    private Transform cabeza;

    void Start()
    {
        var rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null) cabeza = rig.centerEyeAnchor;
    }

    void LateUpdate()
    {
        if (cabeza == null) return;
        Vector3 d = transform.position - cabeza.position;
        d.y = 0f;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }
}
