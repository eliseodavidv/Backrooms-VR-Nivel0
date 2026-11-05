using UnityEngine;

// Parpadeo de un panel fluorescente: cambia la emision del panel y la intensidad de una luz.
// Solo se usa en 2-3 paneles del laberinto; el resto de la iluminacion es bakeada.
public class FluorescentFlicker : MonoBehaviour
{
    [Header("Referencias")]
    public Renderer panel;
    public Light luz;

    [Header("Emision (HDR)")]
    public Color colorEncendido = new Color(1.6f, 1.55f, 1.3f);
    public Color colorApagado = new Color(0.08f, 0.08f, 0.07f);
    public float intensidadLuz = 1.2f;

    [Header("Ritmo")]
    public Vector2 tiempoEstable = new Vector2(1.5f, 5f);
    public Vector2Int parpadeos = new Vector2Int(2, 6);

    private MaterialPropertyBlock bloque;
    private float siguiente;
    private int restantes;
    private bool encendida = true;

    void Awake()
    {
        bloque = new MaterialPropertyBlock();
        siguiente = Time.time + Random.Range(0f, tiempoEstable.y);
    }

    void Update()
    {
        if (Time.time < siguiente) return;

        if (restantes > 0)
        {
            restantes--;
            encendida = !encendida;
            if (restantes == 0)
            {
                encendida = true;
                siguiente = Time.time + Random.Range(tiempoEstable.x, tiempoEstable.y);
            }
            else
            {
                siguiente = Time.time + Random.Range(0.04f, 0.14f);
            }
        }
        else
        {
            restantes = Random.Range(parpadeos.x, parpadeos.y + 1);
            encendida = false;
            siguiente = Time.time + 0.05f;
        }

        Aplicar();
    }

    void Aplicar()
    {
        if (panel != null)
        {
            panel.GetPropertyBlock(bloque);
            bloque.SetColor("_EmissionColor", encendida ? colorEncendido : colorApagado);
            panel.SetPropertyBlock(bloque);
        }
        if (luz != null) luz.intensity = encendida ? intensidadLuz : 0f;
    }
}
