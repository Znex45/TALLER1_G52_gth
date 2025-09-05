// Controlador.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControladorDeLaEscena : MonoBehaviour
{
    [Header("UI")]
    public Button ButtonIniciar;
    public Button ButtonCerrarInteraccion;
    public TMP_Text TextEstado;
    public TMP_Text TextMetricas;

    public TMP_Text TextPila;
    public TMP_Text TextDespachadorProducto;
    public TMP_Text TextDespachadorTemporizador;

    public ScrollRect ScrollRectPila;

    [Header("Parametros")]
    public string NombreArchivoCatalogo = "productos.txt";
    public float CicloGeneracionSeg = 2.5f; // intervalo entre generaciones (2.5s)

    // catálogo y pila
    List<PlantillaProducto> catalogo = new List<PlantillaProducto>();
    Stack<InstanciaProducto> pila = new Stack<InstanciaProducto>();

    // coroutines / estado
    Coroutine coGeneracion;
    Coroutine coDespacho;
    bool corriendo = false;

    // producto en proceso (para evitar inconsistencias si se cierra mientras despacha)
    InstanciaProducto productoEnProceso = null;

    // métricas
    DateTime inicioUTC;
    int totalGenerados = 0;
    int totalDespachados = 0;
    int maxAlturaPila = 0;
    float sumaTiempoDespacho = 0f;
    float pesoTotalDespachado = 0f;
    float ingresoTotalDespachado = 0f;

    Dictionary<TipoProducto, int> generadosPorTipo = new Dictionary<TipoProducto, int>();
    Dictionary<TipoProducto, int> despachadosPorTipo = new Dictionary<TipoProducto, int>();

    void Awake()
    {
        if (ButtonIniciar != null) ButtonIniciar.onClick.AddListener(Iniciar);
        if (ButtonCerrarInteraccion != null) ButtonCerrarInteraccion.onClick.AddListener(CerrarInteraccion);

        foreach (TipoProducto t in Enum.GetValues(typeof(TipoProducto)))
        {
            generadosPorTipo[t] = 0;
            despachadosPorTipo[t] = 0;
        }
    }

    void Start()
    {
        catalogo = ProductoCatalogoSimple.LeerCatalogo(NombreArchivoCatalogo);
        if (catalogo == null || catalogo.Count == 0)
        {
            if (TextEstado != null) TextEstado.text = "No se cargó el catálogo.";
        }
        else
        {
            if (TextEstado != null) TextEstado.text = $"Catalogo ({catalogo.Count}) listo.";
        }

        UpdatePilaUI();
        if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
        if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
    }

    public void Iniciar()
    {
        if (corriendo) return;
        if (catalogo == null || catalogo.Count == 0) { if (TextEstado) TextEstado.text = "Catalogo vacío."; return; }

        ResetEstado();
        corriendo = true;
        inicioUTC = DateTime.UtcNow;
        coGeneracion = StartCoroutine(LoopGeneracion());
        coDespacho = StartCoroutine(LoopDespacho());

        if (TextEstado) TextEstado.text = "Simulación iniciada.";
    }

    public void CerrarInteraccion()
    {
        if (!corriendo) return;
        corriendo = false;

        if (coGeneracion != null) StopCoroutine(coGeneracion);

        // Si había un producto en proceso, devolverlo a la pila para mantener coherencia
        if (productoEnProceso != null)
        {
            pila.Push(productoEnProceso);
            productoEnProceso = null;
            UpdatePilaUI();
        }

        if (coDespacho != null) StopCoroutine(coDespacho);

        // calcular métricas finales
        float duracion = (float)(DateTime.UtcNow - inicioUTC).TotalSeconds;
        float promedio = totalDespachados > 0 ? (sumaTiempoDespacho / totalDespachados) : 0f;

        if (TextMetricas != null)
        {
            TextMetricas.text =
                "== METRICAS FINALES ==\n" +
                $"Duracion: {duracion:F1} s\n" +
                $"Generados: {totalGenerados}\n" +
                $"Despachados: {totalDespachados}\n" +
                $"En Pila: {pila.Count}\n" +
                $"Altura Maxima Pila: {maxAlturaPila}\n" +
                $"Tiempo Promedio Despacho: {promedio:F2} s\n" +
                $"Peso Total Despachado: {pesoTotalDespachado:F2} kg\n" +
                $"Ingreso Total Despachado: ${ingresoTotalDespachado:F2}\n";
        }

        // Guardar reporte mínimo
        var reporte = new Utilidades.ReporteFinal
        {
            fechaUTC = DateTime.UtcNow.ToString("o"),
            duracionSeg = duracion,
            generados = totalGenerados,
            despachados = totalDespachados,
            enPila = pila.Count,
            maxAlturaPila = maxAlturaPila,
            tiempoPromedioDespacho = promedio,
            pesoTotalDespachado = pesoTotalDespachado,
            ingresoTotalDespachado = ingresoTotalDespachado
        };

        string path = Utilidades.GuardarJSON(reporte, "reporte_pila");
        if (TextEstado) TextEstado.text = $"Simulación detenida. JSON: {path}";
    }

    // === Corrutinas ===

    IEnumerator LoopGeneracion()
    {
        int serie = 0;
        while (corriendo)
        {
            // genera aleatoriamente entre 1 y 3 productos por ciclo
            int cantidad = UnityEngine.Random.Range(1, 4); // 1..3 inclusive
            for (int i = 0; i < cantidad; i++)
            {
                var plantilla = catalogo[UnityEngine.Random.Range(0, catalogo.Count)];
                string id = $"{plantilla.Id}-{DateTime.UtcNow.Ticks}-{serie++}";
                var inst = new InstanciaProducto(plantilla, id);

                pila.Push(inst);
                totalGenerados++;
                generadosPorTipo[inst.Tipo]++;

                if (pila.Count > maxAlturaPila) maxAlturaPila = pila.Count;
            }

            UpdatePilaUI();
            if (TextEstado) TextEstado.text = $"Generados: {cantidad} | Altura pila: {pila.Count}";

            // espera entre ciclos (configurable)
            yield return new WaitForSeconds(CicloGeneracionSeg);
        }
    }

    IEnumerator LoopDespacho()
    {
        while (corriendo)
        {
            if (pila.Count == 0)
            {
                if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
                if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
                yield return null;
                continue;
            }

            // siempre sacamos de la pila actual con Pop() (LIFO)
            var prod = pila.Pop();
            productoEnProceso = prod; // marcamos que está siendo procesado
            UpdatePilaUI();

            float t = Mathf.Max(0f, prod.Tiempo);
            if (TextDespachadorProducto) TextDespachadorProducto.text = prod.Nombre;

            float restante = t;
            while (restante > 0f && corriendo)
            {
                if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = $"{restante:F1}s";
                yield return new WaitForSeconds(0.1f);
                restante -= 0.1f;
            }

            if (!corriendo)
            {
                // si se interrumpe, devolvemos el producto a la pila
                pila.Push(prod);
                productoEnProceso = null;
                UpdatePilaUI();
                yield break;
            }

            // despacho finalizado: contabilizamos
            totalDespachados++;
            if (!despachadosPorTipo.ContainsKey(prod.Tipo)) despachadosPorTipo[prod.Tipo] = 0;
            despachadosPorTipo[prod.Tipo]++;

            sumaTiempoDespacho += t;
            pesoTotalDespachado += prod.Peso;
            ingresoTotalDespachado += prod.Precio;

            if (TextEstado) TextEstado.text = $"Despachado: {prod.Nombre} ({t:F2}s)";

            productoEnProceso = null; // limpieza
            if (TextDespachadorProducto) TextDespachadorProducto.text = (pila.Count > 0) ? pila.Peek().Nombre : "---";
            if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
        }
    }

    // === UI Pila ===
    private void UpdatePilaUI()
    {
        if (TextPila == null) return;

        var arr = pila.ToArray();
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < arr.Length; i++)
        {
            // --- CORRECCIÓN: extraer correctamente el ID de plantilla (p. ej. "P-001")
            string plantillaId = arr[i].IdUnico ?? "";

            if (!string.IsNullOrEmpty(plantillaId))
            {
                int last = plantillaId.LastIndexOf('-');
                if (last > 0)
                {
                    int prev = plantillaId.LastIndexOf('-', last - 1);
                    if (prev > 0)
                    {
                        // Si hay al menos dos '-' -> la plantillaId es todo hasta prev (excluyendo el '-' de prev)
                        plantillaId = plantillaId.Substring(0, prev);
                    }
                    else
                    {
                        // Solo un '-' encontrado (caso raro) -> tomar lo que está antes del último '-'
                        plantillaId = plantillaId.Substring(0, last);
                    }
                }
                // si no hay '-' dejamos el id completo
            }

            // Mostrar: Nombre | Tipo | Peso | Precio | (ID del txt)
            sb.AppendLine($"{i + 1}. {arr[i].Nombre} | {arr[i].Tipo} | {arr[i].Peso}kg | ${arr[i].Precio} | ({plantillaId})");
        }
        TextPila.text = sb.ToString();

        if (ScrollRectPila != null)
        {
            Canvas.ForceUpdateCanvases();
            if (ScrollRectPila.verticalNormalizedPosition <= 0.05f)
            {
                ScrollRectPila.verticalNormalizedPosition = 0f;
            }
        }
    }

    void ResetEstado()
    {
        pila.Clear();
        totalGenerados = 0;
        totalDespachados = 0;
        maxAlturaPila = 0;
        sumaTiempoDespacho = 0f;
        pesoTotalDespachado = 0f;
        ingresoTotalDespachado = 0f;
        productoEnProceso = null;

        foreach (TipoProducto t in Enum.GetValues(typeof(TipoProducto)))
        {
            generadosPorTipo[t] = 0;
            despachadosPorTipo[t] = 0;
        }

        UpdatePilaUI();
        if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
        if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
        if (TextMetricas) TextMetricas.text = "";
    }
}





