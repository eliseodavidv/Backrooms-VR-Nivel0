using UnityEngine;

// Solo Editor: sin tracking real la camara queda a y=0 (sobre el piso) y la Game view muestra el fondo.
// Este componente sube el TrackingSpace a una altura de ojos de pie cuando la cabeza no trae altura propia.
// Se decide por la altura real de la cabeza y no por OVRManager.isHmdPresent (con Android como plataforma activa
// puede ser true aunque no llegue pose). En builds y con un visor que si hace tracking no hace nada.
[DefaultExecutionOrder(10000)]
[RequireComponent(typeof(OVRCameraRig))]
public class EditorEyeHeight : MonoBehaviour
{
    public float alturaOjos = 1.6f;

#if UNITY_EDITOR
    private OVRCameraRig rig;
    private bool aplicado;

    void Awake() { rig = GetComponent<OVRCameraRig>(); }

    void LateUpdate()
    {
        if (rig == null || rig.trackingSpace == null || rig.centerEyeAnchor == null) return;

        // Con tracking real la cabeza ya tiene altura (> 0,3 m sobre el origen): no se toca nada.
        bool hayTracking = rig.centerEyeAnchor.localPosition.y > 0.3f;
        var p = rig.trackingSpace.localPosition;
        if (hayTracking)
        {
            if (aplicado) { p.y = 0f; rig.trackingSpace.localPosition = p; aplicado = false; }
            return;
        }
        p.y = alturaOjos;
        rig.trackingSpace.localPosition = p;
        aplicado = true;
    }
#endif
}
