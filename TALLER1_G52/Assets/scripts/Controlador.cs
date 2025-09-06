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
    public float CicloGeneracionSeg = 2.5f; // 2.5s por ciclo


    List<PlantillaProducto> catalogo = new List<PlantillaProducto>();
    Stack<InstanciaProducto> pila = new Stack<InstanciaProducto>();


    Coroutine coGeneracion;
    Coroutine coDespacho;
    bool corriendo = false;

    InstanciaProducto productoEnProceso = null;


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

        if (productoEnProceso != null)
        {
            pila.Push(productoEnProceso);
            productoEnProceso = null;
            UpdatePilaUI();
        }

        if (coDespacho != null) StopCoroutine(coDespacho);

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


    IEnumerator LoopGeneracion()
    {
        int serie = 0;
        while (corriendo)
        {

            int cantidad = UnityEngine.Random.Range(1, 4); 
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


            var prod = pila.Pop();
            productoEnProceso = prod; 
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

                pila.Push(prod);
                productoEnProceso = null;
                UpdatePilaUI();
                yield break;
            }


            totalDespachados++;
            if (!despachadosPorTipo.ContainsKey(prod.Tipo)) despachadosPorTipo[prod.Tipo] = 0;
            despachadosPorTipo[prod.Tipo]++;

            sumaTiempoDespacho += t;
            pesoTotalDespachado += prod.Peso;
            ingresoTotalDespachado += prod.Precio;

            if (TextEstado) TextEstado.text = $"Despachado: {prod.Nombre} ({t:F2}s)";

            productoEnProceso = null; 
            if (TextDespachadorProducto) TextDespachadorProducto.text = (pila.Count > 0) ? pila.Peek().Nombre : "---";
            if (TextDespachadorTemporizador) TextDespachadorTemporizador.text = "";
        }
    }


    private void UpdatePilaUI()
    {

        if (TextPila != null) TextPila.gameObject.SetActive(false);
        if (ScrollRectPila == null || ScrollRectPila.content == null) return;

        var content = ScrollRectPila.content;


        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(10, 10, 10, 10);

        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;


        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);


        var arr = pila.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            var prod = arr[i];

            string plantillaId = prod.IdUnico ?? "";
            if (!string.IsNullOrEmpty(plantillaId))
            {
                int last = plantillaId.LastIndexOf('-');
                if (last > 0)
                {
                    int prev = plantillaId.LastIndexOf('-', last - 1);
                    if (prev > 0) plantillaId = plantillaId.Substring(0, prev);
                    else if (last > 0) plantillaId = plantillaId.Substring(0, last);
                }
            }


            var goItem = new GameObject($"Item_{i + 1}", typeof(RectTransform));
            goItem.transform.SetParent(content, false);
            var leItem = goItem.AddComponent<LayoutElement>();
            leItem.minHeight = 20f;

 
            var goTxt = new GameObject("Info", typeof(RectTransform));
            goTxt.transform.SetParent(goItem.transform, false);
            var tmp = goTxt.AddComponent<TextMeshProUGUI>();
            tmp.enableWordWrapping = true;
            tmp.fontSize = 24;
            tmp.text = $"{i + 1}. {prod.Nombre} | {prod.Tipo} | {prod.Peso}kg | ${prod.Precio} | ({plantillaId})";
            var tmpLE = goTxt.AddComponent<LayoutElement>();
            tmpLE.preferredHeight = -1;


            var tex = Utilidades.CargarTexturaProducto(prod.Nombre);
            var goImg = new GameObject("Imagen", typeof(RectTransform));
            goImg.transform.SetParent(goItem.transform, false);
            var raw = goImg.AddComponent<RawImage>();
            var imgLE = goImg.AddComponent<LayoutElement>();

            if (tex != null)
            {
                raw.texture = tex;
                raw.raycastTarget = false;


                float anchoDeseado = 200f;
                float altoCalculado = 250f;

                imgLE.preferredWidth = anchoDeseado;
                imgLE.preferredHeight = altoCalculado;
            }
            else
            {
                imgLE.preferredHeight = 0f;
                imgLE.minHeight = 0f;
            }


            var itemV = goItem.AddComponent<VerticalLayoutGroup>();
            itemV.childControlHeight = true;
            itemV.childControlWidth = true;
            itemV.childForceExpandHeight = false;
            itemV.childForceExpandWidth = false;
            itemV.spacing = 20f;
            itemV.padding = new RectOffset(0, 0, 0, 0);
        }


        Canvas.ForceUpdateCanvases();
        if (ScrollRectPila.verticalNormalizedPosition <= 0.05f)
            ScrollRectPila.verticalNormalizedPosition = 0f;
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






