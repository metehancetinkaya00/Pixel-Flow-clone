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

    [Header("Layout")]
    public LevelLayout layout;

    [Header("Grid World Space")]
    public Vector3 gridCenterWorld = Vector3.zero;
    public float gridWorldSizeX = 8f;
    public float gridWorldSizeZ = 8f;

    [Header("Block Appearance")]
    public float blockFill = 0.92f;
    public float blockHeight = 1f;
    public ColorPrefabPair[] prefabsByColor;

    public int aliveBlockCount { get; private set; }
    public event System.Action OnAllBlocksCleared;

    public Bounds GridBounds =>
        new Bounds(gridCenterWorld, new Vector3(gridWorldSizeX, 5f, gridWorldSizeZ));

    private Dictionary<BlockColor, GameObject> prefabMap;
    private Block[,] gridBlocks;

    private float cellSizeX;
    private float cellSizeZ;
    private Vector3 gridOrigin;

    private void Awake()
    {
        Instance = this;
        BuildPrefabMap();
    }

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
                // Layout'un üst satırı dünyada arkaya karşılık gelir,
                // bu yüzden y eksenini ters çeviriyoruz.
                int layoutY = (layout.height - 1) - y;
                BlockColor color = layout.Get(x, layoutY);

                if (color == BlockColor.None) continue;

                GameObject prefab = GetPrefab(color);
                if (prefab == null) continue;

                Vector3 pos = GridToWorld(x, y);
                GameObject instance = Instantiate(prefab, pos, Quaternion.identity, transform);

                instance.transform.localScale = new Vector3(
                    cellSizeX * blockFill,
                    blockHeight > 0f ? blockHeight : instance.transform.localScale.y,
                    cellSizeZ * blockFill);

                Block block = instance.GetComponent<Block>() ?? instance.AddComponent<Block>();
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

    public void DestroyBlockTween(Block block, float duration, float delay)
    {
        if (block == null || block.IsDying) return;

        block.IsDying = true;
        block.IsTargeted = false;

        var col = block.GetComponent<Collider>();
        if (col != null) col.enabled = false;

        RemoveFromGrid(block);

        Transform t = block.transform;
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

                Destroy(block.gameObject);
            });
    }

    // Shooter'ın dünya pozisyonuna göre hangi kenar ve satır/sütundan
    // ateş edeceğini belirler.
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

        // Grид içindeyse en yakın kenara ata
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

    // Shooter'ın rengi ile eşleşen, henüz hedeflenmemiş ilk bloğu rezerve eder.
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

    public int BuildLineKey(int side, int lineIndex) => side * 100_000 + lineIndex;

    private void BuildPrefabMap()
    {
        prefabMap = new Dictionary<BlockColor, GameObject>();
        if (prefabsByColor == null) return;

        foreach (var pair in prefabsByColor)
        {
            if (pair?.prefab != null)
                prefabMap[pair.color] = pair.prefab;
        }
    }

    private GameObject GetPrefab(BlockColor color)
    {
        if (prefabMap == null) BuildPrefabMap();
        prefabMap.TryGetValue(color, out GameObject prefab);
        return prefab;
    }

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

    private Vector3 GridToWorld(int x, int y) => new Vector3(
        gridOrigin.x + (x + 0.5f) * cellSizeX,
        gridCenterWorld.y,
        gridOrigin.z + (y + 0.5f) * cellSizeZ);

    private int WorldToGridX(float worldX) =>
        Mathf.Clamp(Mathf.FloorToInt((worldX - gridOrigin.x) / cellSizeX), 0, layout.width - 1);

    private int WorldToGridZ(float worldZ) =>
        Mathf.Clamp(Mathf.FloorToInt((worldZ - gridOrigin.z) / cellSizeZ), 0, layout.height - 1);

    private void RemoveFromGrid(Block block)
    {
        Vector2Int pos = block.gridPos;
        if (gridBlocks == null) return;
        if (pos.x < 0 || pos.x >= layout.width || pos.y < 0 || pos.y >= layout.height) return;
        if (gridBlocks[pos.x, pos.y] == block)
            gridBlocks[pos.x, pos.y] = null;
    }

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

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }
}
