// Utilidades.cs
using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

public static class Utilidades
{
    public class ReporteFinal
    {
        public string fechaUTC;
        public float duracionSeg;
        public int generados;
        public int despachados;
        public int enPila;
        public int maxAlturaPila;
        public float tiempoPromedioDespacho;
        public float pesoTotalDespachado;
        public float ingresoTotalDespachado;
    }

    // Guardar en StreamingAssets (tal como pediste)
    public static string GuardarJSON<T>(T data, string prefijo = "reporte")
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string dir = Application.streamingAssetsPath; // usar StreamingAssets
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string file = $"{prefijo}_{DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")}.json";
            string path = Path.Combine(dir, file);
            File.WriteAllText(path, json);
            Debug.Log($"JSON guardado en StreamingAssets: {path}");
            return path;
        }
        catch (Exception ex)
        {
            Debug.LogError("Error guardando JSON en StreamingAssets: " + ex.Message);
            return "";
        }
    }
}




