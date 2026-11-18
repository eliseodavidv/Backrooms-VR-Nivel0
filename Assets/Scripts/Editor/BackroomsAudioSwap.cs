using UnityEditor;
using UnityEngine;

// Version anterior: asignaba 4 audios (zumbido, pasos, respiracion, latido) a componentes de la escena.
// Ahora todo el reemplazo por audios reales lo hace BackroomsAudioBuilder (BibliotecaSonidos), que cubre todos los sonidos
// y se actualiza solo al poner archivos en Assets/Audio/Reales/. Estos menus se conservan y solo redirigen.
public static class BackroomsAudioSwap
{
    [MenuItem("Backrooms/Aplicar audios reales")]
    public static void Aplicar()
    {
        BackroomsAudioBuilder.ReconstruirBiblioteca();
    }

    [MenuItem("Backrooms/Restaurar audios placeholder")]
    public static void Restaurar()
    {
        Debug.Log("[AudioSwap] Para volver a los sonidos generados, quita los archivos de Assets/Audio/Reales/ (la biblioteca se actualiza sola).");
    }
}
