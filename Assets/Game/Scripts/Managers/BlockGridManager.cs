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

    private Dictionary<BlockColor, GameObject> prefabMap;
    private Block[,] gridBlocks;

    private float cellSizeX;
    private float cellSizeZ;
    private Vector3 gridOrigin;

    public Bounds GridBounds
    {
        get
        {
            return new Bounds(gridCenterWorld, new Vector3(gridWorldSizeX, 5f, gridWorldSizeZ));
        }
    }

    private void Awake()
    {
        Instance = this;
        BuildPrefabMap();
    }

    private void BuildPrefabMap()
    {
        prefabMap = new Dictionary<BlockColor, GameObject>();

        if (prefabsByColor == null)
        {
            return;
        }

        for (int i = 0; i < prefabsByColor.Length; i++)
        {
            if (prefabsByColor[i] == null)
            {
                continue;
            }

            if (prefabsByColor[i].prefab == null)
            {
                continue;
            }

            prefabMap[prefabsByColor[i].color] = prefabsByColor[i].prefab;
        }
    }

    private GameObject GetPrefab(BlockColor c)
    {
        if (prefabMap == null)
        {
            BuildPrefabMap();
        }

        if (prefabMap != null && prefabMap.ContainsKey(c))
        {
            return prefabMap[c];
        }

        return null;
    }

    private void RecalcGridMetrics()
    {
        if (layout == null)
        {
            return;
        }

        if (layout.width < 1)
        {
            layout.width = 1;
        }

        if (layout.height < 1)
        {
            layout.height = 1;
        }

        if (gridWorldSizeX <= 0f)
        {
            gridWorldSizeX = 1f;
        }

        if (gridWorldSizeZ <= 0f)
        {
            gridWorldSizeZ = 1f;
        }

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

    public void BuildLevel()
    {
        if (layout == null)
        {
            return;
        }

        BuildPrefabMap();

        layout.EnsureCellsSize();

        RecalcGridMetrics();
        ClearChildren();

        aliveBlockCount = 0;

        gridBlocks = new Block[layout.width, layout.height];

        for (int y = 0; y < layout.height; y++)
        {
            for (int x = 0; x < layout.width; x++)
            {
                BlockColor cellColor = layout.Get(x, y);

                if (cellColor == BlockColor.None)
                {
                    continue;
                }

                GameObject prefab = GetPrefab(cellColor);
                if (prefab == null)
                {
                    continue;
                }

                Vector3 pos = GridToWorld(x, y);
                GameObject inst = Instantiate(prefab, pos, Quaternion.identity, transform);

                float sx = cellSizeX * blockFill;
                float sz = cellSizeZ * blockFill;

                Vector3 baseScale = inst.transform.localScale;
                float yScale = blockHeight > 0f ? blockHeight : baseScale.y;

                inst.transform.localScale = new Vector3(sx, yScale, sz);

                Block b = inst.GetComponent<Block>();
                if (b == null)
                {
                    b = inst.AddComponent<Block>();
                }

                b.color = cellColor;
                b.gridPos = new Vector2Int(x, y);
                b.IsDying = false;
                b.IsTargeted = false;

                gridBlocks[x, y] = b;
                aliveBlockCount += 1;
            }
        }

        if (aliveBlockCount <= 0)
        {
            aliveBlockCount = 0;

            if (OnAllBlocksCleared != null)
            {
                OnAllBlocksCleared();
            }
        }
    }

    public void DestroyBlockTween(Block blockk, float duration, float delay)
    {
        if (blockk == null)
        {
            return;
        }

        if (blockk.IsDying)
        {
            return;
        }

        blockk.IsDying = true;
        blockk.IsTargeted = false;

        Collider col = blockk.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }

        if (layout != null && gridBlocks != null)
        {
            Vector2Int pos = blockk.gridPos;

            if (pos.x >= 0 && pos.y >= 0 && pos.x < layout.width && pos.y < layout.height)
            {
                if (gridBlocks[pos.x, pos.y] == blockk)
                {
                    gridBlocks[pos.x, pos.y] = null;
                }
            }
        }

        GameObject obj = blockk.gameObject;
        if (obj == null)
        {
            return;
        }

        Transform t = obj.transform;

        DOTween.Kill(t);

        Sequence seq = DOTween.Sequence();

        seq.Join(t.DOScale(Vector3.zero, duration).SetEase(Ease.InBack));
        seq.Join(t.DOMoveY(t.position.y + 0.12f, duration).SetEase(Ease.OutQuad));
        seq.Join(t.DORotate(new Vector3(0f, 180f, 0f), duration, RotateMode.FastBeyond360).SetEase(Ease.OutQuad));

        seq.SetDelay(delay);

        seq.OnComplete(() =>
        {
            aliveBlockCount -= 1;

            if (aliveBlockCount <= 0)
            {
                aliveBlockCount = 0;

                if (OnAllBlocksCleared != null)
                {
                    OnAllBlocksCleared();
                }
            }

            if (obj != null)
            {
                Destroy(obj);
            }
        });
    }

    public int BuildLineKey(int side, int lineIndex)
    {
        return side * 100000 + lineIndex;
    }

    public bool TryResolveShooterLine(Vector3 shooterPosition, out int side, out int lineIndex)
    {
        side = 0;
        lineIndex = 0;

        if (layout == null)
        {
            return false;
        }

        RecalcGridMetrics();

        Bounds boundss = GridBounds;

        bool outsideLeft = shooterPosition.x < boundss.min.x;
        bool outsideRight = shooterPosition.x > boundss.max.x;
        bool outsideBottom = shooterPosition.z < boundss.min.z;
        bool outsideTop = shooterPosition.z > boundss.max.z;

        if (outsideLeft)
        {
            side = 0;
            lineIndex = WorldToGridZ(shooterPosition.z);
            return true;
        }

        if (outsideRight)
        {
            side = 1;
            lineIndex = WorldToGridZ(shooterPosition.z);
            return true;
        }

        if (outsideBottom)
        {
            side = 2;
            lineIndex = WorldToGridX(shooterPosition.x);
            return true;
        }

        if (outsideTop)
        {
            side = 3;
            lineIndex = WorldToGridX(shooterPosition.x);
            return true;
        }

        float dl = Mathf.Abs(shooterPosition.x - boundss.min.x);
        float dr = Mathf.Abs(boundss.max.x - shooterPosition.x);
        float db = Mathf.Abs(shooterPosition.z - boundss.min.z);
        float dt = Mathf.Abs(boundss.max.z - shooterPosition.z);

        float min = dl;
        side = 0;

        if (dr < min)
        {
            min = dr;
            side = 1;
        }

        if (db < min)
        {
            min = db;
            side = 2;
        }

        if (dt < min)
        {
            min = dt;
            side = 3;
        }

        int zIndex = WorldToGridZ(shooterPosition.z);
        int xIndex = WorldToGridX(shooterPosition.x);

        if (side == 0 || side == 1)
        {
            lineIndex = zIndex;
        }
        else
        {
            lineIndex = xIndex;
        }

        return true;
    }

    public bool TryReserveTargetByLine(BlockColor shooterColor, int side, int lineIndex, out Block target)
    {
        target = null;

        if (layout == null || gridBlocks == null)
        {
            return false;
        }

        Block candidate = null;

        if (side == 0)
        {
            candidate = FindFirstBlockInRowFromLeft(lineIndex);
        }
        else if (side == 1)
        {
            candidate = FindFirstBlockInRowFromRight(lineIndex);
        }
        else if (side == 2)
        {
            candidate = FindFirstBlockInColumnFromBottom(lineIndex);
        }
        else if (side == 3)
        {
            candidate = FindFirstBlockInColumnFromTop(lineIndex);
        }

        if (candidate == null)
        {
            return false;
        }

        if (candidate.color != shooterColor)
        {
            return false;
        }

        if (candidate.IsDying)
        {
            return false;
        }

        if (candidate.IsTargeted)
        {
            return false;
        }

        candidate.IsTargeted = true;
        target = candidate;
        return true;
    }

    public bool TryGetTargetByLine(BlockColor shooterColor, int side, int lineIndex, out Block target)
    {
        target = null;

        if (layout == null || gridBlocks == null)
        {
            return false;
        }

        Block candidate = null;

        if (side == 0)
        {
            candidate = FindFirstBlockInRowFromLeft(lineIndex);
        }
        else if (side == 1)
        {
            candidate = FindFirstBlockInRowFromRight(lineIndex);
        }
        else if (side == 2)
        {
            candidate = FindFirstBlockInColumnFromBottom(lineIndex);
        }
        else if (side == 3)
        {
            candidate = FindFirstBlockInColumnFromTop(lineIndex);
        }

        if (candidate == null)
        {
            return false;
        }

        if (candidate.IsDying || candidate.IsTargeted)
        {
            return false;
        }

        if (candidate.color != shooterColor)
        {
            return false;
        }

        target = candidate;
        return true;
    }

    private Block FindFirstBlockInRowFromLeft(int zIndex)
    {
        if (zIndex < 0 || zIndex >= layout.height)
        {
            return null;
        }

        for (int x = 0; x < layout.width; x++)
        {
            Block b = gridBlocks[x, zIndex];
            if (b != null && !b.IsDying)
            {
                return b;
            }
        }

        return null;
    }

    private Block FindFirstBlockInRowFromRight(int zIndex)
    {
        if (zIndex < 0 || zIndex >= layout.height)
        {
            return null;
        }

        for (int x = layout.width - 1; x >= 0; x--)
        {
            Block b = gridBlocks[x, zIndex];
            if (b != null && !b.IsDying)
            {
                return b;
            }
        }

        return null;
    }

    private Block FindFirstBlockInColumnFromBottom(int xIndex)
    {
        if (xIndex < 0 || xIndex >= layout.width)
        {
            return null;
        }

        for (int z = 0; z < layout.height; z++)
        {
            Block b = gridBlocks[xIndex, z];
            if (b != null && !b.IsDying)
            {
                return b;
            }
        }

        return null;
    }

    private Block FindFirstBlockInColumnFromTop(int xIndex)
    {
        if (xIndex < 0 || xIndex >= layout.width)
        {
            return null;
        }

        for (int z = layout.height - 1; z >= 0; z--)
        {
            Block b = gridBlocks[xIndex, z];
            if (b != null && !b.IsDying)
            {
                return b;
            }
        }

        return null;
    }

    private int WorldToGridX(float worldX)
    {
        float local = (worldX - gridOrigin.x) / cellSizeX;
        int index = Mathf.FloorToInt(local);

        if (index < 0)
        {
            index = 0;
        }

        if (index > layout.width - 1)
        {
            index = layout.width - 1;
        }

        return index;
    }

    private int WorldToGridZ(float worldZ)
    {
        float local = (worldZ - gridOrigin.z) / cellSizeZ;
        int index = Mathf.FloorToInt(local);

        if (index < 0)
        {
            index = 0;
        }

        if (index > layout.height - 1)
        {
            index = layout.height - 1;
        }

        return index;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}