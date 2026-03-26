using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlowClone/LevelData")]
public class LevelData : ScriptableObject
{
    public string levelId;

    public LevelLayout layout;

    public int splinePathIndex = 0;

    public QueueLayoutSettings queueLayout;

    public ShooterQueuePlacementData[] queuePlacements;
}

[System.Serializable]
public struct QueueLayoutSettings
{
    public int columnCount;
    public int depthCount;

    public Vector3 origin;
    public Vector3 columnStep;
    public Vector3 depthStep;

    public bool createSlotObjects;
}

[System.Serializable]
public struct ShooterQueuePlacementData
{
    public Shooter prefab;
    public int shots;
    public int groupId;

    public int column;
    public int depth;
}