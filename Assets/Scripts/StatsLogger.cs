using System.Globalization;
using System.IO;
using UnityEngine;

// RF-14: registro de estadisticas por partida en un CSV dentro de Application.persistentDataPath.
// Una fila por partida; el voto (opcional, siempre la ultima columna) se actualiza en la ultima fila cuando el jugador vota.
public static class StatsLogger
{
    public const string Cabecera = "fecha_utc,partida,resultado,causa,tiempo_s,detectado,teletransportes,escondites,mini_monstruos_muertos,voto";
    const string CabeceraAnterior = "fecha_utc,partida,resultado,causa,tiempo_s,detectado,teletransportes,escondites,voto";

    public static string Ruta { get { return Path.Combine(Application.persistentDataPath, "backrooms_stats.csv"); } }

    public static int Guardar(string resultado, string causa, float tiempoSegundos, int detectado, int teletransportes, int escondites, int miniMonstruosMuertos, string voto)
    {
        try
        {
            string ruta = Ruta;
            if (!File.Exists(ruta)) File.WriteAllText(ruta, Cabecera + "\n");
            else MigrarSiHaceFalta(ruta);
            int partida = File.ReadAllLines(ruta).Length; // cabecera + filas previas => numero de esta partida
            string fila = string.Join(",", new[]
            {
                System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                partida.ToString(CultureInfo.InvariantCulture),
                resultado,
                causa,
                tiempoSegundos.ToString("F1", CultureInfo.InvariantCulture),
                detectado.ToString(CultureInfo.InvariantCulture),
                teletransportes.ToString(CultureInfo.InvariantCulture),
                escondites.ToString(CultureInfo.InvariantCulture),
                miniMonstruosMuertos.ToString(CultureInfo.InvariantCulture),
                string.IsNullOrEmpty(voto) ? "ninguno" : voto,
            });
            File.AppendAllText(ruta, fila + "\n");
            Debug.Log("[Stats] Partida " + partida + " guardada en " + ruta + " -> " + fila);
            return partida;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Stats] No se pudo guardar: " + e.Message);
            return -1;
        }
    }

    // Un CSV creado antes de existir la columna mini_monstruos_muertos se actualiza: cabecera nueva y "0" en las filas viejas
    // (la columna se inserta antes de "voto", que sigue siendo la ultima).
    static void MigrarSiHaceFalta(string ruta)
    {
        string[] lineas = File.ReadAllLines(ruta);
        if (lineas.Length == 0 || lineas[0].Trim() != CabeceraAnterior) return;
        lineas[0] = Cabecera;
        for (int i = 1; i < lineas.Length; i++)
        {
            int k = lineas[i].LastIndexOf(',');
            if (k < 0) continue;
            lineas[i] = lineas[i].Substring(0, k) + ",0" + lineas[i].Substring(k);
        }
        File.WriteAllLines(ruta, lineas);
        Debug.Log("[Stats] CSV migrado: columna mini_monstruos_muertos agregada (" + (lineas.Length - 1) + " filas previas con 0).");
    }

    // Cambia el voto de la ultima fila (el jugador vota despues de que se guardo la partida).
    public static void ActualizarUltimoVoto(string voto)
    {
        try
        {
            string ruta = Ruta;
            if (!File.Exists(ruta)) return;
            string[] lineas = File.ReadAllLines(ruta);
            if (lineas.Length < 2) return;
            string ultima = lineas[lineas.Length - 1];
            int i = ultima.LastIndexOf(',');
            lineas[lineas.Length - 1] = ultima.Substring(0, i + 1) + (string.IsNullOrEmpty(voto) ? "ninguno" : voto);
            File.WriteAllLines(ruta, lineas);
            Debug.Log("[Stats] Voto registrado: " + voto);
        }
        catch (System.Exception e)
        {
            Debug.LogError("[Stats] No se pudo actualizar el voto: " + e.Message);
        }
    }
}
