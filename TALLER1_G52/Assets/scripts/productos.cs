using System;
using UnityEngine;

public enum TipoProducto { Basico, Fragil, Pesado, Otro }

[Serializable]
public class PlantillaProducto
{
    public string IdPlantilla; 
    public string Nombre;
    public TipoProducto Tipo;
    public float Peso;
    public float Precio;
    public float Tiempo; 

    public static bool TryParse(string line, out PlantillaProducto t)
    {
        t = null;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var parts = line.Split('|');
        if (parts.Length < 6) return false;


        var tipoStr = parts[2].Trim().ToLowerInvariant();
        var tipo = TipoProducto.Otro;
        if (tipoStr.Contains("basico")) tipo = TipoProducto.Basico;
        else if (tipoStr.Contains("fragi")) tipo = TipoProducto.Fragil;
        else if (tipoStr.Contains("pesad")) tipo = TipoProducto.Pesado;

        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (!float.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Float, ci, out float peso)) return false;
        if (!float.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Float, ci, out float precio)) return false;
        if (!float.TryParse(parts[5].Trim(), System.Globalization.NumberStyles.Float, ci, out float tiempo)) return false;

        t = new PlantillaProducto
        {
            IdPlantilla = parts[0].Trim(),
            Nombre = parts[1].Trim(),
            Tipo = tipo,
            Peso = peso,
            Precio = precio,
            Tiempo = Mathf.Max(0f, tiempo),
        };
        return true;
    }
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

    public InstanciaProducto(PlantillaProducto t, string idGenerado)
    {
        IdUnico = idGenerado;
        Nombre = t.Nombre;
        Tipo = t.Tipo;
        Peso = t.Peso;
        Precio = t.Precio;
        Tiempo = t.Tiempo;
        GeneradoUTC = DateTime.UtcNow;
    }
}

