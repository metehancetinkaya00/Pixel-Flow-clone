using UnityEngine;

[CreateAssetMenu(menuName = "PixelFlowClone/LevelData")]
public class LevelData : ScriptableObject
{
    public string levelId;

    public LevelLayout layout;

    public int splinePathIndex = 0;

    public int firstRowSelectableCount = 2;

    public ShooterGroup[] shooterGroups;
}

[System.Serializable]
public struct ShooterGroup
{
    public Shooter prefab;
    public int count;
    public int shots;
}
