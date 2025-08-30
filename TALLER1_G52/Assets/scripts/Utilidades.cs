using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;

public static class Utilidades
{
    [Serializable]
    public class TipoKV { public string tipo; public int generados; public int despachados; }

    [Serializable]
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
        public List<TipoKV> porTipo;
    }

    public static string GuardarJSON<T>(T data, string prefijo = "reporte_pila")
    {
        string json = JsonUtility.ToJson(data, true);
        string dir = Application.persistentDataPath;
        string file = $"{prefijo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        string path = Path.Combine(dir, file);
        File.WriteAllText(path, json);
        return path;
    }

    public static void SetTexto(TMP_Text t, string msg)
    {
        if (t != null) t.text = msg;
        Debug.Log(msg);
    }

    public static string FormatoTipos(Dictionary<TipoProducto, int> gen, Dictionary<TipoProducto, int> dep)
    {
        System.Text.StringBuilder sb = new();
        foreach (var k in gen.Keys)
        {
            int g = gen[k];
            int d = dep.ContainsKey(k) ? dep[k] : 0;
            sb.AppendLine($"- {k}: {g} / {d}");
        }
        return sb.ToString();
    }
}
