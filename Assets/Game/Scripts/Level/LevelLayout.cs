using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlowClone/LevelLayout")]
public class LevelLayout : ScriptableObject
{
    public int width = 10;
    public int height = 10;

    public BlockColor[] cells;

    public BlockColor Get(int x, int y)
    {
        int idx = y * width + x;

        if (cells == null || idx < 0 || idx >= cells.Length)
        {
            return BlockColor.None;
        }

        return cells[idx];
    }

    public void Set(int x, int y, BlockColor value)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        EnsureCellsSize();

        int idx = y * width + x;
        cells[idx] = value;
    }

    public void Resize(int newWidth, int newHeight)
    {
        if (newWidth < 1)
        {
            newWidth = 1;
        }

        if (newHeight < 1)
        {
            newHeight = 1;
        }

        int oldWidth = width;
        int oldHeight = height;
        BlockColor[] oldCells = cells;

        width = newWidth;
        height = newHeight;

        int targetSize = width * height;
        cells = new BlockColor[targetSize];

        if (oldCells == null)
        {
            return;
        }

        int copyWidth = Mathf.Min(oldWidth, width);
        int copyHeight = Mathf.Min(oldHeight, height);

        for (int y = 0; y < copyHeight; y++)
        {
            for (int x = 0; x < copyWidth; x++)
            {
                int oldIdx = y * oldWidth + x;
                int newIdx = y * width + x;

                if (oldIdx >= 0 && oldIdx < oldCells.Length && newIdx >= 0 && newIdx < cells.Length)
                {
                    cells[newIdx] = oldCells[oldIdx];
                }
            }
        }
    }

    public void EnsureCellsSize()
    {
        if (width < 1)
        {
            width = 1;
        }

        if (height < 1)
        {
            height = 1;
        }

        int targetSize = width * height;

        if (cells == null)
        {
            cells = new BlockColor[targetSize];
            return;
        }

        if (cells.Length != targetSize)
        {
            Resize(width, height);
        }
    }

    public void ClearAll()
    {
        EnsureCellsSize();

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = BlockColor.None;
        }
    }

    public void FillAll(BlockColor value)
    {
        EnsureCellsSize();

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = value;
        }
    }
}