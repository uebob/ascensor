using UnityEngine;

// Pon este script en cada CAPA del parallax (el objeto vacío que contiene la imagen).
[DefaultExecutionOrder(1000)] // Se ejecuta después de que se mueva la cámara -> sin temblores
public class Parallax : MonoBehaviour
{
    [Header("Cuánto sigue la capa a la cámara")]
    [Tooltip("1 = pegada a la cámara (muy lejos). 0 = como el suelo. Negativo = primer plano (más rápido que el suelo).")]
    [Range(-1f, 1f)] public float efectoX = 0.5f;
    [Range(-1f, 1f)] public float efectoY = 0.5f;

    [Header("Repetición infinita")]
    public bool repetir = true;
    [Tooltip("Ancho EXACTO de la imagen en unidades (píxeles de ancho ÷ Pixels Per Unit). 0 = lo mide solo.")]
    public float anchoManual = 0f;
    [Tooltip("Espacio extra entre una copia de la capa y la siguiente.")]
    public float separacion = 0f;

    private Transform camara;
    private Vector3 inicioCamara;   // dónde estaba la cámara al empezar
    private Vector3 inicioCapa;     // dónde estaba la capa al empezar
    private float ancho;            // ancho de la capa (para repetirla)
    private float centro;           // centro de la imagen respecto a la capa

    void Start()
    {
        camara = Camera.main.transform;
        inicioCamara = camara.position;
        inicioCapa = transform.position;

        if (repetir) CrearCopias();
    }

    void LateUpdate()
    {
        // 1. Cuánto se ha movido la cámara desde el principio
        Vector3 recorrido = camara.position - inicioCamara;

        // 2. La capa se mueve solo una parte de ese recorrido
        float x = inicioCapa.x + recorrido.x * efectoX;
        float y = inicioCapa.y + recorrido.y * efectoY;

        // 3. Si la cámara se aleja más de medio tramo, la capa salta un tramo entero
        //    (como hay copias idénticas a los lados, el salto no se nota)
        if (repetir && ancho > 0f)
        {
            float distancia = camara.position.x - (x + centro);
            x += Mathf.Round(distancia / ancho) * ancho;
        }

        transform.position = new Vector3(x, y, transform.position.z);
    }

    void CrearCopias()
    {
        // Aviso si hay restos de "Tramo" guardados en la escena
        foreach (Transform hijo in transform)
            if (hijo.name.StartsWith("Tramo"))
                Debug.LogWarning($"[Parallax] '{name}' ya tiene un objeto '{hijo.name}' guardado en la escena. Bórralo con el juego parado: el script crea los tramos solo.", this);

        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        if (sprites.Length == 0) return;

        // Mide lo que ocupan las imágenes de la capa
        Bounds limites = sprites[0].bounds;
        foreach (SpriteRenderer s in sprites) limites.Encapsulate(s.bounds);

        ancho = (anchoManual > 0f ? anchoManual : limites.size.x) + separacion;
        centro = limites.center.x - transform.position.x;

        Debug.Log($"[Parallax] '{name}': ancho del tramo = {ancho} unidades. Debe ser igual al ancho de tu imagen (píxeles ÷ PPU).", this);

        // Mete todo lo de la capa en un grupo llamado "Tramo"
        Transform tramo = new GameObject("Tramo").transform;
        tramo.position = transform.position;
        while (transform.childCount > 0)
            transform.GetChild(0).SetParent(tramo, true);
        tramo.SetParent(transform, true);

        // Copia el tramo a la izquierda y a la derecha, pegado exactamente
        Instantiate(tramo, tramo.position + Vector3.left * ancho, Quaternion.identity, transform).name = "Tramo (izquierda)";
        Instantiate(tramo, tramo.position + Vector3.right * ancho, Quaternion.identity, transform).name = "Tramo (derecha)";
    }

    // Dibuja en la vista Scene dónde empieza y acaba cada tramo (líneas verdes)
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || ancho <= 0f) return;
        Gizmos.color = Color.green;
        float c = transform.position.x + centro;
        for (int i = -1; i <= 2; i++)
        {
            float bx = c - ancho / 2f + (i - 0) * ancho;
            Gizmos.DrawLine(new Vector3(bx, transform.position.y - 10f, 0f), new Vector3(bx, transform.position.y + 10f, 0f));
        }
    }
}