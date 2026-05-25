using UnityEngine;
using UnityEngine.Tilemaps;

public class MapChunk : MonoBehaviour
{
    public Vector2Int chunkCoord;
    public int chunkSize;
    public Tilemap tilemap;

    private MapGenerator generator;

    public void Init(MapGenerator generator, Vector2Int chunkCoord, int chunkSize)
    {
        this.generator = generator;
        this.chunkCoord = chunkCoord;
        this.chunkSize = chunkSize;

        tilemap = gameObject.AddComponent<Tilemap>();
        gameObject.AddComponent<TilemapRenderer>();

        Refresh();
    }

    public void Refresh()
    {
        if (generator == null || generator.Data == null || tilemap == null)
            return;

        tilemap.ClearAllTiles();

        int startX = chunkCoord.x * chunkSize;
        int startY = chunkCoord.y * chunkSize;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkSize; y++)
            {
                Vector2Int dataCell = new Vector2Int(startX + x, startY + y);

                if (!generator.Data.InBounds(dataCell))
                    continue;

                if (!generator.Data.IsBlocked(dataCell))
                    continue;

                int dirtType = generator.Data.GetDirtType(dataCell);
                Vector2 world = generator.Data.CellToWorld(dataCell);
                Vector3Int tileCell = tilemap.WorldToCell(world);

                tilemap.SetTile(tileCell, generator.GetDirtTileByType(dirtType));
            }
        }
    }
}