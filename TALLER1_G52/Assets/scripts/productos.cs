using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

public enum TipoProducto { Basico, Fragil, Pesado, Otro }

[Serializable]
public class PlantillaProducto
{
    public string Id;
    public string Nombre;
    public TipoProducto Tipo;
    public float Peso;
    public float Precio;
    public float Tiempo; 

    public override string ToString()
        => $"{Id} | {Nombre} | {Tipo} | {Peso}kg | ${Precio} | {Tiempo}s";
}

[Serializable]
public class InstanciaProducto
{
    public string IdUnico;
    public string Nombre;
    public TipoProducto Tipo;
    public float Peso;
    public float Precio;
    public float Tiempo;
    public DateTime GeneradoUTC;

    public InstanciaProducto(PlantillaProducto p, string idGenerado)
    {
        IdUnico = idGenerado;
        Nombre = p.Nombre;
        Tipo = p.Tipo;
        Peso = p.Peso;
        Precio = p.Precio;
        Tiempo = p.Tiempo;
        GeneradoUTC = DateTime.UtcNow;
    }
}

public static class ProductoCatalogoSimple
{
    public static List<PlantillaProducto> LeerCatalogo(string fileName)
    {
        List<PlantillaProducto> lista = new List<PlantillaProducto>();
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError("No se encontró el archivo: " + path);
            return lista;
        }

        try
        {
            string contenido = File.ReadAllText(path);
            using (StringReader reader = new StringReader(contenido))
            {
                string line;
                var ci = CultureInfo.InvariantCulture;

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.TrimStart().StartsWith("#")) continue;

                    string[] parts = line.Split('|');
                    if (parts.Length < 6)
                    {
                        Debug.LogWarning("Línea inválida: " + line);
                        continue;
                    }

                    TipoProducto tipo = TipoProducto.Otro;
                    string t = parts[2].Trim().ToLowerInvariant();
                    if (t.Contains("basico")) tipo = TipoProducto.Basico;
                    else if (t.Contains("fragil") || t.Contains("frágil")) tipo = TipoProducto.Fragil;
                    else if (t.Contains("pesad")) tipo = TipoProducto.Pesado;


                    if (!float.TryParse(parts[3].Trim(), NumberStyles.Float, ci, out float peso)) { Debug.LogWarning("Peso inválido: " + line); continue; }
                    if (!float.TryParse(parts[4].Trim(), NumberStyles.Float, ci, out float precio)) { Debug.LogWarning("Precio inválido: " + line); continue; }
                    if (!float.TryParse(parts[5].Trim(), NumberStyles.Float, ci, out float tiempo)) { Debug.LogWarning("Tiempo inválido: " + line); continue; }

                    var p = new PlantillaProducto
                    {
                        Id = parts[0].Trim(),
                        Nombre = parts[1].Trim(),
                        Tipo = tipo,
                        Peso = peso,
                        Precio = precio,
                        Tiempo = Mathf.Max(0f, tiempo),
                    };

                    lista.Add(p);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error leyendo catálogo: " + e.Message);
        }

        return lista;
    }
}
