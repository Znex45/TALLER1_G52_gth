// ========================================
// ControladorDeLaEscena.cs
// Orquestador: UI + Pila + Corrutinas + JSON
// ========================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControladorDeLaEscena : MonoBehaviour
{
    #region Referencias UI
    [Header("UI")]
    [SerializeField] Button ButtonIniciar;
    [SerializeField] Button ButtonCerrarInteraccion;
    [SerializeField] TMP_Text TextEstado;     // mensajes cortos
    [SerializeField] TMP_Text TextMetricas;   // métricas finales
    #endregion

    #region Parámetros
    [Header("Parámetros")]
    [SerializeField] string NombreArchivoCatalogo = "productos.txt";
    [SerializeField, Min(0.1f)] float CicloGeneracionSeg = 1f;  // cada cuánto se generan 1-3 productos
    #endregion

    #region Datos internos
    private List<PlantillaProducto> _catalogo = new();
    private Stack<InstanciaProducto> _pila = new();

    private Coroutine _coGeneracion;
    private Coroutine _coDespacho;
    private bool _corriendo = false;

    // Métricas
    private DateTime _inicioUTC;
    private int _totalGenerados = 0;
    private int _totalDespachados = 0;
    private int _maxAlturaPila = 0;
    private float _sumaTiempoDespacho = 0f;
    private float _pesoTotalDespachado = 0f;
    private float _ingresoTotalDespachado = 0f;

    private Dictionary<TipoProducto, int> _generadosPorTipo = new();
    private Dictionary<TipoProducto, int> _despachadosPorTipo = new();
    #endregion

    #region Ciclo de vida
    void Awake()
    {
        // Botones
        if (ButtonIniciar) ButtonIniciar.onClick.AddListener(Iniciar);
        if (ButtonCerrarInteraccion) ButtonCerrarInteraccion.onClick.AddListener(CerrarInteraccion);

        // Inicializa contadores por tipo
        foreach (TipoProducto k in Enum.GetValues(typeof(TipoProducto)))
        {
            _generadosPorTipo[k] = 0;
            _despachadosPorTipo[k] = 0;
        }

        Utilidades.SetTexto(TextEstado, "Cargando catálogo...");
    }

    IEnumerator Start()
    {
        // Cargar catálogo de StreamingAssets
        yield return Utilidades.LeerCatalogo(NombreArchivoCatalogo, (lista) => {
            _catalogo = lista ?? new List<PlantillaProducto>();
        });

        if (_catalogo.Count == 0)
            Utilidades.SetTexto(TextEstado, "⚠️ Catálogo vacío o no encontrado.");
        else
            Utilidades.SetTexto(TextEstado, $"Catálogo cargado ({_catalogo.Count}). Presiona Iniciar.");
    }
    #endregion

    #region Controles
    public void Iniciar()
    {
        if (_corriendo) { Utilidades.SetTexto(TextEstado, "Ya está corriendo."); return; }
        if (_catalogo.Count == 0) { Utilidades.SetTexto(TextEstado, "No hay catálogo válido."); return; }

        ResetEstado();

        _corriendo = true;
        _inicioUTC = DateTime.UtcNow;

        _coGeneracion = StartCoroutine(LoopGeneracion());
        _coDespacho = StartCoroutine(LoopDespacho());

        Utilidades.SetTexto(TextEstado, "▶️ Simulación iniciada.");
        if (TextMetricas) TextMetricas.text = "";
    }

    public void CerrarInteraccion()
    {
        if (!_corriendo) { Utilidades.SetTexto(TextEstado, "No hay simulación activa."); return; }

        _corriendo = false;
        if (_coGeneracion != null) StopCoroutine(_coGeneracion);
        if (_coDespacho != null) StopCoroutine(_coDespacho);

        // Métricas finales
        float duracion = (float)(DateTime.UtcNow - _inicioUTC).TotalSeconds;
        float promedio = _totalDespachados > 0 ? (_sumaTiempoDespacho / _totalDespachados) : 0f;

        if (TextMetricas)
        {
            TextMetricas.text =
                $"== MÉTRICAS FINALES ==\n" +
                $"Duración: {duracion:F1} s\n" +
                $"Generados: {_totalGenerados}\n" +
                $"Despachados: {_totalDespachados}\n" +
                $"En Pila: {_pila.Count}\n" +
                $"Altura Máxima Pila: {_maxAlturaPila}\n" +
                $"Tiempo Promedio Despacho: {promedio:F2} s\n" +
                $"Peso Total Despachado: {_pesoTotalDespachado:F2}\n" +
                $"Ingreso Total Despachado: ${_ingresoTotalDespachado:F2}\n" +
                $"Por Tipo (Generados / Despachados):\n" +
                Utilidades.FormatoTipos(_generadosPorTipo, _despachadosPorTipo);
        }

        // Armar y guardar reporte JSON
        var porTipo = new List<Utilidades.TipoKV>();
        foreach (var k in _generadosPorTipo.Keys)
        {
            porTipo.Add(new Utilidades.TipoKV
            {
                tipo = k.ToString(),
                generados = _generadosPorTipo[k],
                despachados = _despachadosPorTipo.ContainsKey(k) ? _despachadosPorTipo[k] : 0
            });
        }

        var reporte = new Utilidades.ReporteFinal
        {
            fechaUTC = DateTime.UtcNow.ToString("o"),
            duracionSeg = duracion,
            generados = _totalGenerados,
            despachados = _totalDespachados,
            enPila = _pila.Count,
            maxAlturaPila = _maxAlturaPila,
            tiempoPromedioDespacho = promedio,
            pesoTotalDespachado = _pesoTotalDespachado,
            ingresoTotalDespachado = _ingresoTotalDespachado,
            porTipo = porTipo
        };

        string path = Utilidades.GuardarJSON(reporte, "reporte_pila");
        Utilidades.SetTexto(TextEstado, $"⏹️ Simulación detenida. JSON: {path}");
    }
    #endregion

    #region Corrutinas
    private IEnumerator LoopGeneracion()
    {
        int serie = 0;
        while (_corriendo)
        {
            int cantidad = UnityEngine.Random.Range(1, 4); // 1..3
            for (int i = 0; i < cantidad; i++)
            {
                var plantilla = _catalogo[UnityEngine.Random.Range(0, _catalogo.Count)];
                string idUnico = $"{plantilla.IdPlantilla}-{DateTime.UtcNow.Ticks}-{serie++}";
                var inst = new InstanciaProducto(plantilla, idUnico);

                _pila.Push(inst);
                _totalGenerados++;
                _generadosPorTipo[inst.Tipo]++;

                if (_pila.Count > _maxAlturaPila) _maxAlturaPila = _pila.Count;
            }

            Utilidades.SetTexto(TextEstado, $"Generados: {cantidad} | Altura pila: {_pila.Count}");
            yield return new WaitForSeconds(CicloGeneracionSeg);
        }
    }

    private IEnumerator LoopDespacho()
    {
        while (_corriendo)
        {
            if (_pila.Count == 0)
            {
                yield return null; // sin elementos este frame
                continue;
            }

            // LIFO: desapilar desde la cima
            var prod = _pila.Pop();

            float t = Mathf.Max(0f, prod.Tiempo);
            yield return new WaitForSeconds(t); // espera propia del producto

            _totalDespachados++;
            if (!_despachadosPorTipo.ContainsKey(prod.Tipo)) _despachadosPorTipo[prod.Tipo] = 0;
            _despachadosPorTipo[prod.Tipo]++;

            _sumaTiempoDespacho += t;
            _pesoTotalDespachado += prod.Peso;
            _ingresoTotalDespachado += prod.Precio;

            Utilidades.SetTexto(TextEstado, $"Despachado: {prod.Nombre} ({t:F2}s) | Pila: {_pila.Count}");
        }
    }
    #endregion

    #region Utilidades internas
    private void ResetEstado()
    {
        _pila.Clear();
        _totalGenerados = 0;
        _totalDespachados = 0;
        _maxAlturaPila = 0;
        _sumaTiempoDespacho = 0f;
        _pesoTotalDespachado = 0f;
        _ingresoTotalDespachado = 0f;

        foreach (TipoProducto k in Enum.GetValues(typeof(TipoProducto)))
        {
            _generadosPorTipo[k] = 0;
            _despachadosPorTipo[k] = 0;
        }
    }
    #endregion
}

