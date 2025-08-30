// ===================================
// Utilidades.cs
// StreamingAssets, JSON y helpers UI
// ===================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_WEBGL)
using UnityEngine.Networking;
#endif
using TMPro;

public static class Utilidades
{
    #region Lectura catálogo (StreamingAssets)
    // Lee "fileName" desde Assets/StreamingAssets (PC/Mac con IO directo; Android/WebGL con UWR)
    public static IEnumerator LeerCatalogo(string fileName, Action<List<PlantillaProducto>> onTermina)
    {
        var lista = new List<PlantillaProducto>();
        string pathOrUrl = Path.Combine(Application.streamingAssetsPath, fileName);

#if UNITY_EDITOR || UNITY_STANDALONE
        if (File.Exists(pathOrUrl))
        {
            foreach (var line in File.ReadAllLines(pathOrUrl))
                if (PlantillaProducto.TryParse(line, out var t)) lista.Add(t);
        }
        else
        {
            Debug.LogWarning($"[Catálogo] No se encontró: {pathOrUrl}");
        }
        onTermina?.Invoke(lista);
        yield break;
#else
        using (UnityWebRequest www = UnityWebRequest.Get(pathOrUrl))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Catálogo] Error leyendo: {www.error}");
            }
            else
            {
                var text = www.downloadHandler.text;
                using (StringReader sr = new StringReader(text))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                        if (PlantillaProducto.TryParse(line, out var t)) lista.Add(t);
                }
            }
            onTermina?.Invoke(lista);
        }
#endif
    }
    #endregion

    #region Guardado JSON
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

    public static string GuardarJSON<T>(T data, string prefijoArchivo = "reporte_pila")
    {
        string json = JsonUtility.ToJson(data, true);
        string dir = Application.persistentDataPath;
        string file = $"{prefijoArchivo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        string path = Path.Combine(dir, file);
        File.WriteAllText(path, json);
        return path;
    }
    #endregion

    #region Helpers UI
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
    #endregion
}
