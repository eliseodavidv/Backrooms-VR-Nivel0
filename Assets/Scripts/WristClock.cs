using UnityEngine;
using UnityEngine.UI;

// RF-10: reloj de muñeca (UI diegetica, anclada al controlador izquierdo). Cuenta regresiva mm:ss.
public class WristClock : MonoBehaviour
{
    public Text texto;

    void Awake()
    {
        // Fuente monoespaciada si el sistema tiene alguna; si no, la fuente por defecto.
        var mono = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Roboto Mono", "Droid Sans Mono", "monospace" }, 64);
        if (mono != null && texto != null) texto.font = mono;
    }

    public void Mostrar(float segundos)
    {
        if (texto == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0f, segundos));
        texto.text = (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    }
}
