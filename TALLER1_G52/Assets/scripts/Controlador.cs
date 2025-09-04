using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControladorDeLaEscena : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button ButtonIniciar;
    [SerializeField] Button ButtonCerrarInteraccion;
    [SerializeField] TMP_Text TextEstado;
    [SerializeField] TMP_Text TextMetricas;

    [SerializeField] TMP_Text TextPila;
    [SerializeField] TMP_Text TextDespachadorProducto;
    [SerializeField] TMP_Text TextDespachadorTemporizador;

    // NUEVO: referencia al ScrollRect del panel pila
    [SerializeField] ScrollRect ScrollRectPila;

    [Header("Parámetros")]
    [SerializeField] string NombreArchivoCatalogo = "productos.txt";
    [SerializeField, Min(0.1f)] float CicloGeneracionSeg = 1f;

    // Datos
    private List<PlantillaProducto> _catalogo = new();
    private Stack<InstanciaProducto> _pila = new();

    // Corrutinas / estado
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

    // ===== Ciclo de vida =====
    void Awake()
    {
        if (ButtonIniciar) ButtonIniciar.onClick.AddListener(Iniciar);
        if (ButtonCerrarInteraccion) ButtonCerrarInteraccion.onClick.AddListener(CerrarInteraccion);

        foreach (TipoProducto k in Enum.GetValues(typeof(TipoProducto)))
        {
            _generadosPorTipo[k] = 0;
            _despachadosPorTipo[k] = 0;
        }
    }

    void Start()
    {
        _catalogo = ProductoCatalogoSimple.LeerCatalogo(NombreArchivoCatalogo);

        if (_catalogo.Count == 0)
            Utilidades.SetTexto(TextEstado, "Catalogo vacio o no encontrado.");
        else
            Utilidades.SetTexto(TextEstado, $"Catalogo cargado ({_catalogo.Count}). Presiona Iniciar.");

        UpdatePilaUI();
        if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
        if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
    }

    // ===== Controles =====
    public void Iniciar()
    {
        if (_corriendo) { Utilidades.SetTexto(TextEstado, "Ya esta corriendo."); return; }
        if (_catalogo.Count == 0) { Utilidades.SetTexto(TextEstado, "No hay catalogo."); return; }

        ResetEstado();

        _corriendo = true;
        _inicioUTC = DateTime.UtcNow;

        _coGeneracion = StartCoroutine(LoopGeneracion());
        _coDespacho = StartCoroutine(LoopDespacho());

        Utilidades.SetTexto(TextEstado, "Simulacion iniciada.");
        if (TextMetricas) TextMetricas.text = "";
    }

    public void CerrarInteraccion()
    {
        if (!_corriendo) { Utilidades.SetTexto(TextEstado, "No hay simulacion activa."); return; }

        _corriendo = false;
        if (_coGeneracion != null) StopCoroutine(_coGeneracion);
        if (_coDespacho != null) StopCoroutine(_coDespacho);

        float duracion = (float)(DateTime.UtcNow - _inicioUTC).TotalSeconds;
        float promedio = _totalDespachados > 0 ? (_sumaTiempoDespacho / _totalDespachados) : 0f;

        if (TextMetricas)
        {
            TextMetricas.text =
                "== METRICAS FINALES ==\n" +
                $"Duracion: {duracion:F1} s\n" +
                $"Generados: {_totalGenerados}\n" +
                $"Despachados: {_totalDespachados}\n" +
                $"En Pila: {_pila.Count}\n" +
                $"Altura Maxima Pila: {_maxAlturaPila}\n" +
                $"Tiempo Promedio Despacho: {promedio:F2} s\n" +
                $"Peso Total Despachado: {_pesoTotalDespachado:F2}\n" +
                $"Ingreso Total Despachado: ${_ingresoTotalDespachado:F2}\n" +
                "Por Tipo (Generados / Despachados):\n" +
                Utilidades.FormatoTipos(_generadosPorTipo, _despachadosPorTipo);
        }

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
        Utilidades.SetTexto(TextEstado, $"Simulacion detenida. JSON: {path}");
    }

    // ===== Corrutinas =====
    private IEnumerator LoopGeneracion()
    {
        int serie = 0;
        while (_corriendo)
        {
            int cantidad = UnityEngine.Random.Range(1, 4);
            for (int i = 0; i < cantidad; i++)
            {
                var plantilla = _catalogo[UnityEngine.Random.Range(0, _catalogo.Count)];
                string idUnico = $"{plantilla.Id}-{DateTime.UtcNow.Ticks}-{serie++}";
                var inst = new InstanciaProducto(plantilla, idUnico);

                _pila.Push(inst);
                _totalGenerados++;
                _generadosPorTipo[inst.Tipo]++;

                if (_pila.Count > _maxAlturaPila) _maxAlturaPila = _pila.Count;
            }

            UpdatePilaUI();
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
                if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
                if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
                yield return null;
                continue;
            }

            var prod = _pila.Pop();
            UpdatePilaUI();

            float t = Mathf.Max(0f, prod.Tiempo);
            if (TextDespachadorProducto) TextDespachadorProducto.text = prod.Nombre;

            float remaining = t;
            while (remaining > 0f && _corriendo)
            {
                if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = $"{remaining:F1}s";
                yield return new WaitForSeconds(0.1f);
                remaining -= 0.1f;
            }

            if (!_corriendo)
            {
                _pila.Push(prod);
                UpdatePilaUI();
                yield break;
            }

            _totalDespachados++;
            if (!_despachadosPorTipo.ContainsKey(prod.Tipo)) _despachadosPorTipo[prod.Tipo] = 0;
            _despachadosPorTipo[prod.Tipo]++;

            _sumaTiempoDespacho += t;
            _pesoTotalDespachado += prod.Peso;
            _ingresoTotalDespachado += prod.Precio;

            Utilidades.SetTexto(TextEstado, $"Despachado: {prod.Nombre} ({t:F2}s) | Pila: {_pila.Count}");

            if (TextDespachadorProducto) TextDespachadorProducto.text = (_pila.Count > 0) ? _pila.Peek().Nombre : "---";
            if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
        }
    }

    // ===== Utils =====
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

        UpdatePilaUI();
        if (TextDespachadorProducto) TextDespachadorProducto.text = "---";
        if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
        if (TextMetricas) TextMetricas.text = "";
    }

    // ===== Update UI Pila (con scroll condicional) =====
    private void UpdatePilaUI()
    {
        if (TextPila == null) return;

        var arr = _pila.ToArray();
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < arr.Length; i++)
        {
            sb.AppendLine($"{i + 1}. {arr[i].Nombre} ({arr[i].IdUnico})");
        }
        TextPila.text = sb.ToString();

        if (ScrollRectPila != null)
        {
            Canvas.ForceUpdateCanvases();

            // Solo auto-scroll si ya estabas casi abajo
            if (ScrollRectPila.verticalNormalizedPosition <= 0.05f)
            {
                ScrollRectPila.verticalNormalizedPosition = 0f;
            }
        }
    }
}


