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

}
