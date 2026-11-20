using UnityEngine;

// Ponlo en un objeto vacío. Reparte estrellas al azar dentro de una zona.
// Haz un objeto por cada capa de estrellas (lejos, medio, cerca).
public class CampoEstrellas : MonoBehaviour
{
    [Header("Estrellas")]
    public Sprite[] sprites;                       // uno o varios dibujos de estrella
    public int cantidad = 30;
    public Vector2 zona = new Vector2(11f, 2.5f);  // ancho y alto de la zona (unidades)
    public int semilla = 1;                        // cambia el número para otro reparto

    [Header("Brillo (más bajo = más lejos)")]
    [Range(0f, 1f)] public float brilloMin = 0.3f;
    [Range(0f, 1f)] public float brilloMax = 0.5f;
    public Color color = Color.white;

    [Header("Parpadeo")]
    public bool parpadear = true;
    public float velocidadMin = 0.3f;
    public float velocidadMax = 1.2f;
    [Range(0f, 1f)] public float cuantoParpadea = 0.5f; // 0 = nada, 1 = se apagan del todo

    [Header("Dibujado")]
    public Material material;                      // pon Sprite-Unlit-Default
    public string sortingLayer = "Cielo";
    public int orderInLayer = -95;

    private SpriteRenderer[] estrellas;
    private float[] brillo, velocidad, desfase;

    void Awake()
    {
        if (sprites == null || sprites.Length == 0) return;

        System.Random azar = new System.Random(semilla);
        estrellas = new SpriteRenderer[cantidad];
        brillo = new float[cantidad];
        velocidad = new float[cantidad];
        desfase = new float[cantidad];

        for (int i = 0; i < cantidad; i++)
        {
            Sprite s = sprites[azar.Next(sprites.Length)];

            // Posición al azar dentro de la zona, ajustada a píxeles exactos
            float x = Azar(azar, -zona.x / 2f, zona.x / 2f);
            float y = Azar(azar, -zona.y / 2f, zona.y / 2f);
            float ppu = s.pixelsPerUnit;
            x = Mathf.Round(x * ppu) / ppu;
            y = Mathf.Round(y * ppu) / ppu;

            GameObject go = new GameObject("Estrella");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = orderInLayer;

            brillo[i] = Azar(azar, brilloMin, brilloMax);
            velocidad[i] = Azar(azar, velocidadMin, velocidadMax);
            desfase[i] = Azar(azar, 0f, 10f);

            Color c = color;
            c.a = brillo[i];
            sr.color = c;
            estrellas[i] = sr;
        }
    }

    void Update()
    {
        if (!parpadear || estrellas == null) return;

        for (int i = 0; i < estrellas.Length; i++)
        {
            // Onda suave entre 0 y 1, distinta para cada estrella
            float onda = (Mathf.Sin((Time.time + desfase[i]) * velocidad[i] * Mathf.PI) + 1f) / 2f;
            Color c = estrellas[i].color;
            c.a = brillo[i] * Mathf.Lerp(1f - cuantoParpadea, 1f, onda);
            estrellas[i].color = c;
        }
    }

    // Dibuja la zona en amarillo en la vista Scene para que veas dónde saldrán
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, zona);
    }

    private static float Azar(System.Random azar, float min, float max)
    {
        return min + (float)azar.NextDouble() * (max - min);
    }
}