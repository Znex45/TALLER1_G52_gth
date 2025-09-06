using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ListaProductos : MonoBehaviour
{
    // Esta clase representa cada producto
    [System.Serializable]
    public class Producto
    {
        public string nombre;        // Texto que aparecerá en la UI
        public string nombreImagen;  // Nombre del archivo en Resources/ImagenesProducto
    }

    [Header("Lista de productos")]
    public List<Producto> productos = new List<Producto>();

    [Header("Referencias de UI")]
    public GameObject prefabProducto; // Prefab del item (con imagen + texto)
    public Transform content;         // El Content del ScrollView

    void Start()
    {
        GenerarLista();
    }

    void GenerarLista()
    {
        foreach (Producto p in productos)
        {
            // Instanciar el prefab dentro del Content
            GameObject item = Instantiate(prefabProducto, content);

            // Buscar componentes dentro del prefab
            Image img = item.transform.Find("ImagenProducto").GetComponent<Image>();
            TMP_Text txt = item.transform.Find("TextoProducto").GetComponent<TMP_Text>();

            // Cargar imagen desde Resources
            Sprite sprite = Resources.Load<Sprite>("ImagenesProducto/" + p.nombreImagen);
            if (sprite != null)
                img.sprite = sprite;
            else
                Debug.LogWarning("No se encontró la imagen: " + p.nombreImagen);

            // Asignar texto
            txt.text = p.nombre;
        }
    }
}
