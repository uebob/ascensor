using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TerrainGenerator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private TileBase grassTile;
    [SerializeField] private TileBase dirtTile;

    [Header("Tamaño del mundo (en tiles)")]
    [SerializeField] private int width = 200;
    [SerializeField] private int height = 60;

    [Header("Superficie")]
    [SerializeField] private int baseHeight = 30;     // altura media de la superficie
    [SerializeField] private int amplitude = 8;       // variación arriba/abajo
    [SerializeField] private float noiseScale = 0.05f; // menor = colinas más suaves

    [Header("Semilla")]
    [SerializeField] private bool randomSeed = true;
    [SerializeField] private int seed = 12345;

    [Header("Bordes")]
    [SerializeField] private bool createSideWalls = true;

    private void Start()
    {
        Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        tilemap.ClearAllTiles();

        if (randomSeed) seed = Random.Range(0, 100000);
        float offset = new System.Random(seed).Next(0, 10000);

        var positions = new List<Vector3Int>();
        var tiles = new List<TileBase>();

        for (int x = 0; x < width; x++)
        {
            float noise = Mathf.PerlinNoise(x * noiseScale + offset, 0f); // 0..1
            int surface = baseHeight + Mathf.RoundToInt((noise - 0.5f) * 2f * amplitude);
            surface = Mathf.Clamp(surface, 1, height - 1);

            for (int y = 0; y <= surface; y++)
            {
                positions.Add(new Vector3Int(x, y, 0));
                tiles.Add(y == surface ? grassTile : dirtTile);
            }
        }

        // Una sola llamada para pintar todo: mucho más rápido que SetTile en bucle
        tilemap.SetTiles(positions.ToArray(), tiles.ToArray());

        if (createSideWalls) CreateSideWalls();
    }

    private void CreateSideWalls()
    {
        // Evita duplicados si se regenera
        var grid = tilemap.layoutGrid.transform;
        foreach (string n in new[] { "WallLeft", "WallRight" })
        {
            var old = grid.Find(n);
            if (old != null) Destroy(old.gameObject);
        }

        Vector3 cell = tilemap.layoutGrid.cellSize;
        float worldWidth = width * cell.x;
        float worldHeight = height * cell.y;
        float wallThickness = 1f;

        CreateWall("WallLeft",  new Vector2(-wallThickness / 2f, worldHeight / 2f), wallThickness, worldHeight * 3f);
        CreateWall("WallRight", new Vector2(worldWidth + wallThickness / 2f, worldHeight / 2f), wallThickness, worldHeight * 3f);
    }

    private void CreateWall(string wallName, Vector2 localPos, float w, float h)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(tilemap.layoutGrid.transform);
        go.transform.localPosition = localPos;

        var box = go.AddComponent<BoxCollider2D>();
        box.size = new Vector2(w, h);
    }
}