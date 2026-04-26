using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

public class BlockGridManager : MonoBehaviour
{
    public static BlockGridManager Instance;

    [System.Serializable]
    public class ColorPrefabPair
    {
        public BlockColor color;
        public GameObject prefab;
    }

    public LevelLayout layout;

    public Vector3 gridCenterWorld = Vector3.zero;
    public float gridWorldSizeX = 8f;
    public float gridWorldSizeZ = 8f;

    public float blockFill = 0.92f;
    public float blockHeight = 1f;

    public ColorPrefabPair[] prefabsByColor;

    public int aliveBlockCount;

    public System.Action OnAllBlocksCleared;

    // ---------------------------------------------------------------
    private Dictionary<BlockColor, GameObject> prefabMap;
    private Block[,] gridBlocks;

    private float cellSizeX;
    private float cellSizeZ;
    private Vector3 gridOrigin;
    // ---------------------------------------------------------------

    public Bounds GridBounds =>
        new Bounds(gridCenterWorld, new Vector3(gridWorldSizeX, 5f, gridWorldSizeZ));


    public int BuildLineKey(int side, int lineIndex) => side * 100_000 + lineIndex;

    // ---------------------------------------------------------------

    private void Awake()
    {
        Instance = this;
        BuildPrefabMap();
    }

    // ---------------------------------------------------------------
    

    private void BuildPrefabMap()
    {
        prefabMap = new Dictionary<BlockColor, GameObject>();

        if (prefabsByColor == null) return;

        foreach (var pair in prefabsByColor)
        {
            if (pair != null && pair.prefab != null)
                prefabMap[pair.color] = pair.prefab;
        }
    }

    private GameObject GetPrefab(BlockColor color)
    {
        if (prefabMap == null) BuildPrefabMap();
        prefabMap.TryGetValue(color, out GameObject prefab);
        return prefab;
    }

    // ---------------------------------------------------------------
 

    private void RecalcGridMetrics()
    {
        layout.width = Mathf.Max(1, layout.width);
        layout.height = Mathf.Max(1, layout.height);
        gridWorldSizeX = Mathf.Max(0.01f, gridWorldSizeX);
        gridWorldSizeZ = Mathf.Max(0.01f, gridWorldSizeZ);

        cellSizeX = gridWorldSizeX / layout.width;
        cellSizeZ = gridWorldSizeZ / layout.height;
        gridOrigin = gridCenterWorld - new Vector3(gridWorldSizeX * 0.5f, 0f, gridWorldSizeZ * 0.5f);
    }

    private Vector3 GridToWorld(int x, int y)
    {
        float px = gridOrigin.x + (x + 0.5f) * cellSizeX;
        float pz = gridOrigin.z + (y + 0.5f) * cellSizeZ;
        return new Vector3(px, gridCenterWorld.y, pz);
    }

  
    private int WorldToGridX(float worldX)
    {
        int index = Mathf.FloorToInt((worldX - gridOrigin.x) / cellSizeX);
        return Mathf.Clamp(index, 0, layout.width - 1);
    }

    private int WorldToGridZ(float worldZ)
    {
        int index = Mathf.FloorToInt((worldZ - gridOrigin.z) / cellSizeZ);
        return Mathf.Clamp(index, 0, layout.height - 1);
    }

    // ---------------------------------------------------------------
  

    public void BuildLevel()
    {
        if (layout == null) return;

        layout.EnsureCellsSize();
        RecalcGridMetrics();
        ClearChildren();

        aliveBlockCount = 0;
        gridBlocks = new Block[layout.width, layout.height];

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                int srcY = (layout.height - 1) - y;
                BlockColor color = layout.Get(x, srcY);

                if (color == BlockColor.None) continue;

                GameObject prefab = GetPrefab(color);
                if (prefab == null) continue;

                Vector3 pos = GridToWorld(x, y);
                GameObject inst = Instantiate(prefab, pos, Quaternion.identity, transform);

                inst.transform.localScale = new Vector3(
                    cellSizeX * blockFill,
                    blockHeight > 0f ? blockHeight : inst.transform.localScale.y,
                    cellSizeZ * blockFill);

                Block block = inst.GetComponent<Block>() ?? inst.AddComponent<Block>();
                block.color = color;
                block.gridPos = new Vector2Int(x, y);
                block.IsDying = false;
                block.IsTargeted = false;

                gridBlocks[x, y] = block;
                aliveBlockCount++;
            }
        }

      
        if (aliveBlockCount == 0)
            OnAllBlocksCleared?.Invoke();
    }

    // ---------------------------------------------------------------
 

    public void DestroyBlockTween(Block block, float duration, float delay)
    {
        if (block == null || block.IsDying) return;

        block.IsDying = true;
        block.IsTargeted = false;

        var col = block.GetComponent<Collider>();
        if (col != null) col.enabled = false;

 
        Vector2Int pos = block.gridPos;
        if (gridBlocks != null &&
            pos.x >= 0 && pos.x < layout.width &&
            pos.y >= 0 && pos.y < layout.height &&
            gridBlocks[pos.x, pos.y] == block)
        {
            gridBlocks[pos.x, pos.y] = null;
        }

        GameObject obj = block.gameObject;
        Transform t = obj.transform;

        DOTween.Kill(t);

        DOTween.Sequence()
            .SetDelay(delay)
            .Append(t.DOScale(t.localScale * 1.18f, duration * 0.35f).SetEase(Ease.OutQuad))
            .Append(t.DOScale(Vector3.zero, duration * 0.65f).SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                aliveBlockCount = Mathf.Max(0, aliveBlockCount - 1);

                if (aliveBlockCount == 0)
                    OnAllBlocksCleared?.Invoke();

                if (obj != null) Destroy(obj);
            });
    }

    // ---------------------------------------------------------------


  
    public bool TryResolveShooterLine(Vector3 shooterPos, out int side, out int lineIndex)
    {
        side = 0;
        lineIndex = 0;

        if (layout == null) return false;

        Bounds bounds = GridBounds;

 
        if (shooterPos.x < bounds.min.x) { side = 0; lineIndex = WorldToGridZ(shooterPos.z); return true; }
        if (shooterPos.x > bounds.max.x) { side = 1; lineIndex = WorldToGridZ(shooterPos.z); return true; }
        if (shooterPos.z < bounds.min.z) { side = 2; lineIndex = WorldToGridX(shooterPos.x); return true; }
        if (shooterPos.z > bounds.max.z) { side = 3; lineIndex = WorldToGridX(shooterPos.x); return true; }

   
        float dl = shooterPos.x - bounds.min.x;
        float dr = bounds.max.x - shooterPos.x;
        float db = shooterPos.z - bounds.min.z;
        float dt = bounds.max.z - shooterPos.z;

        float minDist = Mathf.Min(dl, dr, db, dt);

        if (Mathf.Approximately(minDist, dl)) side = 0;
        else if (Mathf.Approximately(minDist, dr)) side = 1;
        else if (Mathf.Approximately(minDist, db)) side = 2;
        else side = 3;

        lineIndex = (side == 0 || side == 1)
            ? WorldToGridZ(shooterPos.z)
            : WorldToGridX(shooterPos.x);

        return true;
    }

   
    public bool TryReserveTargetByLine(BlockColor shooterColor, int side, int lineIndex, out Block target)
    {
        target = null;

        if (layout == null || gridBlocks == null) return false;

        Block candidate = side switch
        {
            0 => FindFirstInRowFromLeft(lineIndex),
            1 => FindFirstInRowFromRight(lineIndex),
            2 => FindFirstInColumnFromBottom(lineIndex),
            3 => FindFirstInColumnFromTop(lineIndex),
            _ => null
        };

        if (candidate == null || candidate.IsDying || candidate.IsTargeted) return false;
        if (candidate.color != shooterColor) return false;

        candidate.IsTargeted = true;
        target = candidate;
        return true;
    }

    // ---------------------------------------------------------------
 

    private Block FindFirstInRowFromLeft(int zIndex)
    {
        if (zIndex < 0 || zIndex >= layout.height) return null;
        for (int x = 0; x < layout.width; x++)
            if (gridBlocks[x, zIndex] is { IsDying: false } b) return b;
        return null;
    }

    private Block FindFirstInRowFromRight(int zIndex)
    {
        if (zIndex < 0 || zIndex >= layout.height) return null;
        for (int x = layout.width - 1; x >= 0; x--)
            if (gridBlocks[x, zIndex] is { IsDying: false } b) return b;
        return null;
    }

    private Block FindFirstInColumnFromBottom(int xIndex)
    {
        if (xIndex < 0 || xIndex >= layout.width) return null;
        for (int z = 0; z < layout.height; z++)
            if (gridBlocks[xIndex, z] is { IsDying: false } b) return b;
        return null;
    }

    private Block FindFirstInColumnFromTop(int xIndex)
    {
        if (xIndex < 0 || xIndex >= layout.width) return null;
        for (int z = layout.height - 1; z >= 0; z--)
            if (gridBlocks[xIndex, z] is { IsDying: false } b) return b;
        return null;
    }

    // ---------------------------------------------------------------

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }
}
