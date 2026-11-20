using UnityEngine;

// Ponlo en un objeto vacío dentro de "Fondo", junto con el script Parallax.
// Haz un objeto por cada capa de nubes.
// Este script SOLO reparte las nubes y las mueve con el viento.
// El parallax y la repetición los hace el script Parallax.
// IMPORTANTE: en el Parallax de esta capa pon "Ancho Manual" = "Ancho Zona" de aquí.
public class CampoNubes : MonoBehaviour
{
    [Header("Nubes")]
    public Sprite[] sprites;                 // tus dibujos de nubes de esta capa
    public int cantidad = 5;
    public int semilla = 1;                  // cambia el número para otro reparto

    [Header("Zona")]
    [Tooltip("Ancho del tramo de nubes. Pon el MISMO número en 'Ancho Manual' del script Parallax.")]
    public float anchoZona = 18f;
    public float alturaMin = 0.5f;           // altura (Y) más baja de una nube
    public float alturaMax = 2f;             // altura (Y) más alta de una nube

    [Header("Viento")]
    [Tooltip("Unidades por segundo. Negativo = hacia la izquierda.")]
    public float viento = -0.1f;

    [Header("Aspecto")]
    public Color color = Color.white;        // baja la A (transparencia) para nubes lejanas
    public bool voltearAlAzar = true;
    public Material material;                // pon Sprite-Unlit-Default
    public string sortingLayer = "Cielo";
    public int orderInLayer = -70;

    private Transform[] todas;               // todas las nubes (también las copias del Parallax)
    private float[] posicionX;
    private float vientoAcumulado;

    // Awake: así las nubes ya existen cuando el Parallax hace sus copias
    void Awake()
    {
        if (sprites == null || sprites.Length == 0) return;

        System.Random azar = new System.Random(semilla);
        float hueco = anchoZona / cantidad;

        for (int i = 0; i < cantidad; i++)
        {
            Sprite s = sprites[azar.Next(sprites.Length)];

            GameObject go = new GameObject("Nube");
            go.transform.SetParent(transform, false);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.color = color;
            sr.flipX = voltearAlAzar && azar.NextDouble() < 0.5;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = orderInLayer;

            // Repartidas a lo ancho del tramo, con algo de azar
            float x = -anchoZona / 2f + hueco * i + Azar(azar, 0f, hueco * 0.8f);
            float y = Azar(azar, alturaMin, alturaMax);
            go.transform.localPosition = new Vector3(x, y, 0f);
        }
    }

    void LateUpdate()
    {
        // La primera vez, busca todas las nubes (las originales y las copias del Parallax)
        if (todas == null)
        {
            SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
            todas = new Transform[srs.Length];
            posicionX = new float[srs.Length];
            for (int i = 0; i < srs.Length; i++)
            {
                todas[i] = srs[i].transform;
                posicionX[i] = todas[i].localPosition.x;
            }
        }

        // El viento mueve cada nube dentro de su tramo.
        // Si sale por un lado, entra por el otro (y la copia de al lado tapa el salto).
        vientoAcumulado += viento * Time.deltaTime;

        for (int i = 0; i < todas.Length; i++)
        {
            float x = Mathf.Repeat(posicionX[i] + vientoAcumulado + anchoZona / 2f, anchoZona) - anchoZona / 2f;
            Vector3 p = todas[i].localPosition;
            todas[i].localPosition = new Vector3(x, p.y, p.z);
        }
    }

    // Dibuja la zona en la vista Scene (rectángulo cian) para que veas dónde saldrán
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 centro = transform.position + new Vector3(0f, (alturaMin + alturaMax) / 2f, 0f);
        Gizmos.DrawWireCube(centro, new Vector3(anchoZona, Mathf.Max(0.1f, alturaMax - alturaMin), 0f));
    }

    private static float Azar(System.Random azar, float min, float max)
    {
        return min + (float)azar.NextDouble() * (max - min);
    }
}