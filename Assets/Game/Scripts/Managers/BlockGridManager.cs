using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
public class BlockGridManager : MonoBehaviour
{
    public static BlockGridManager Instance { get; private set; }

    [System.Serializable]
    public class ColorPrefabPair
    {
        public BlockColor color;
        public GameObject prefab;
    }

    [Header("Grid Settings")]
    [SerializeField] private LevelLayout layout;
    [SerializeField] private float cellSize = 0.6f;
    [SerializeField] private Vector3 origin = Vector3.zero;

    [Header("Prefabs By Color")]
    [SerializeField] private ColorPrefabPair[] prefabsByColor;

    private Dictionary<BlockColor, GameObject> prefabMap;
    private Block[,] gridBlocks;

    public float CellSize
    {
        get { return cellSize; }
    }

    public Bounds GridBounds
    {
        get
        {
            if (layout == null)
            {
                return new Bounds(origin, Vector3.zero);
            }

            float sizeX = layout.width * cellSize;
            float sizeZ = layout.height * cellSize;

            Vector3 center = origin + new Vector3((layout.width - 1) * cellSize * 0.5f, 0f, (layout.height - 1) * cellSize * 0.5f);
            Vector3 size = new Vector3(sizeX, 5f, sizeZ);

            return new Bounds(center, size);
        }
    }

    private void Awake()
    {
        Instance = this;
        BuildPrefabMap();
    }

    private void Start()
    {
        BuildLevel();
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

    private GameObject GetPrefab(BlockColor colorr)
    {
        if (prefabMap == null)
        {
            BuildPrefabMap();
        }

        if (prefabMap != null && prefabMap.ContainsKey(colorr))
        {
            return prefabMap[colorr];
        }

        return null;
    }

    public void BuildLevel()
    {
        if (layout == null)
        {
            return;
        }

        ClearChildren();

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

                Vector3 positionn = origin + new Vector3(x * cellSize, 0f, y * cellSize);
                GameObject instancee = Instantiate(prefab, positionn, Quaternion.identity, transform);

                Block blockk = instancee.GetComponent<Block>();
                if (blockk != null)
                {
                    blockk.gridPos = new Vector2Int(x, y);
                    gridBlocks[x, y] = blockk;
                }
            }
        }
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

        float distanceToLeft = Mathf.Abs(shooterPosition.x - boundss.min.x);
        float distanceToRight = Mathf.Abs(boundss.max.x - shooterPosition.x);
        float distanceToBottom = Mathf.Abs(shooterPosition.z - boundss.min.z);
        float distanceToTop = Mathf.Abs(boundss.max.z - shooterPosition.z);

        int zIndex = WorldToGridZ(shooterPosition.z);
        int xIndex = WorldToGridX(shooterPosition.x);

        float minDistance = distanceToLeft;
        side = 0;

        if (distanceToRight < minDistance)
        {
            minDistance = distanceToRight;
            side = 1;
        }

        if (distanceToBottom < minDistance)
        {
            minDistance = distanceToBottom;
            side = 2;
        }

        if (distanceToTop < minDistance)
        {
            minDistance = distanceToTop;
            side = 3;
        }

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
            Block blockk = gridBlocks[x, zIndex];
            if (blockk != null)
            {
                return blockk;
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
            Block blockk = gridBlocks[x, zIndex];
            if (blockk != null)
            {
                return blockk;
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
            Block blockk = gridBlocks[xIndex, z];
            if (blockk != null)
            {
                return blockk;
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
            Block blockk = gridBlocks[xIndex, z];
            if (blockk != null)
            {
                return blockk;
            }
        }

        return null;
    }

    private int WorldToGridX(float worldX)
    {
        float localX = (worldX - origin.x) / cellSize;
        int index = Mathf.FloorToInt(localX + 0.5f);

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
        float localZ = (worldZ - origin.z) / cellSize;
        int index = Mathf.FloorToInt(localZ + 0.5f);

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

    public void DestroyBlockTween(Block blockk, float duration, float delay)
    {
        if (blockk == null)
        {
            return;
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

        GameObject tileObjectCopy = blockk.gameObject;

        if (tileObjectCopy == null)
        {
            return;
        }

        Transform tileTransform = tileObjectCopy.transform;

        DOTween.Kill(tileTransform);

        tileTransform
            .DOScale(Vector3.zero, duration)
            .SetEase(Ease.InBack)
            .SetDelay(delay)
            .OnComplete(() =>
            {
                if (tileObjectCopy != null)
                {
                    Destroy(tileObjectCopy);
                }
            });
    }
    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Destroy(transform.GetChild(i).gameObject);
        }
    }
}