using System;
using System.IO;
using UnityEngine;
using System.Collections.Generic;

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


    public static string GuardarJSON<T>(T data, string prefijo = "reporte")
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            string dir = Application.streamingAssetsPath; 
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

    public static void SetTexto(TMPro.TMP_Text txt, string contenido)
    {
        if (txt != null) txt.text = contenido;
    }

    public static string FormatoTipos(Dictionary<TipoProducto, int> gen, Dictionary<TipoProducto, int> desp)
    {
        System.Text.StringBuilder sb = new();
        foreach (var kv in gen)
        {
            int d = desp != null && desp.ContainsKey(kv.Key) ? desp[kv.Key] : 0;
            sb.AppendLine($"- {kv.Key}: {kv.Value} / {d}");
        }
        return sb.ToString();
    }


    public static Texture2D CargarTexturaProducto(string nombreProducto, int maxWidth = 512, int maxHeight = 512)
    {
        if (string.IsNullOrWhiteSpace(nombreProducto)) return null;

        string Normalizar(string s)
        {
            s = s.Trim().ToLowerInvariant();
            s = s.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u")
                 .Replace("ü", "u").Replace("ñ", "n");
            s = s.Replace(" ", "");
            return s;
        }

        string baseName = Normalizar(nombreProducto);

        var candidatos = new List<string>
        {
            Path.Combine(Application.streamingAssetsPath, baseName + ".jpg"),
            Path.Combine(Application.streamingAssetsPath, baseName + ".png"),
            Path.Combine(Application.dataPath, "ImagenesJPG", baseName + ".jpg"),
            Path.Combine(Application.dataPath, "ImagenesJPG", baseName + ".png")
        };

        foreach (var ruta in candidatos)
        {
            try
            {
                if (File.Exists(ruta))
                {
                    byte[] bytes = File.ReadAllBytes(ruta);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes, markNonReadable: false))
                    {

                        if (tex.width > maxWidth || tex.height > maxHeight)
                        {
                            float rw = (float)maxWidth / tex.width;
                            float rh = (float)maxHeight / tex.height;
                            float r = Mathf.Min(rw, rh);
                            int nw = Mathf.Max(1, Mathf.RoundToInt(tex.width * r));
                            int nh = Mathf.Max(1, Mathf.RoundToInt(tex.height * r));

                            var rt = RenderTexture.GetTemporary(nw, nh, 0, RenderTextureFormat.ARGB32);
                            var old = RenderTexture.active;
                            Graphics.Blit(tex, rt);
                            RenderTexture.active = rt;

                            var tex2 = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
                            tex2.ReadPixels(new Rect(0, 0, nw, nh), 0, 0);
                            tex2.Apply();

                            RenderTexture.active = old;
                            RenderTexture.ReleaseTemporary(rt);
                            UnityEngine.Object.Destroy(tex);
                            tex = tex2;
                        }
                        return tex;
                    }
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"No se pudo cargar textura '{ruta}': {e.Message}");
            }
        }
        return null;
    }
}





