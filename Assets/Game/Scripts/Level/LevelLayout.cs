using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlowClone/LevelLayout")]
public class LevelLayout : ScriptableObject
{
    public int width = 10;
    public int height = 10;
    public BlockColor[] cells;

    public BlockColor Get(int x, int y)
    {
        int index = y * width + x;
        return IsValidIndex(index) ? cells[index] : BlockColor.None;
    }

    public void Set(int x, int y, BlockColor value)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return;

        EnsureCellsSize();

        cells[y * width + x] = value;
    }

    public void EnsureCellsSize()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        int targetSize = width * height;

        if (cells == null)
        {
            cells = new BlockColor[targetSize];
            return;
        }

        if (cells.Length == targetSize) return;

        BlockColor[] old = cells;
        cells = new BlockColor[targetSize];

        int copyCount = Mathf.Min(old.Length, cells.Length);
        for (int i = 0; i < copyCount; i++)
            cells[i] = old[i];
    }

    public void Resize(int newWidth, int newHeight)
    {
        newWidth = Mathf.Max(1, newWidth);
        newHeight = Mathf.Max(1, newHeight);

        int oldWidth = width;
        int oldHeight = height;
        BlockColor[] oldCells = cells;

        width = newWidth;
        height = newHeight;
        cells = new BlockColor[width * height];

        if (oldCells == null) return;

        int copyWidth = Mathf.Min(oldWidth, width);
        int copyHeight = Mathf.Min(oldHeight, height);

        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                int oldIndex = y * oldWidth + x;
                int newIndex = y * width + x;

                if (oldIndex < oldCells.Length && newIndex < cells.Length)
                    cells[newIndex] = oldCells[oldIndex];
            }
        }
    }

    public void ClearAll()
    {
        EnsureCellsSize();
        for (int i = 0; i < cells.Length; i++)
            cells[i] = BlockColor.None;
    }

    public void FillAll(BlockColor value)
    {
        EnsureCellsSize();
        for (int i = 0; i < cells.Length; i++)
            cells[i] = value;
    }

    private bool IsValidIndex(int index) =>
        cells != null && index >= 0 && index < cells.Length;
}
