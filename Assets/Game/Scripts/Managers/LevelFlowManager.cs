using UnityEngine;
using System.Collections.Generic;

public class LevelFlowManager : MonoBehaviour
{
    public static LevelFlowManager Instance;

    public LevelDatabase database;
    public SplinePathDefinition[] splinePathsInScene;

    public int startLevelIndex = 0;

    private int currentLevelIndex;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        currentLevelIndex = PlayerPrefs.GetInt("level_index", startLevelIndex);
        LoadLevel(currentLevelIndex);
    }

    private void OnDisable()
    {
        UnhookGridEvents();
    }

    public void LoadNextLevel()
    {
        LoadLevel(currentLevelIndex + 1);
    }

    public void ReloadCurrentLevel()
    {
        LoadLevel(currentLevelIndex);
    }

    public void CompleteLevelAndSaveProgress()
    {
        int currentSaved = PlayerPrefs.GetInt("level_index", startLevelIndex);
        int nextIndex = currentLevelIndex + 1;

        if (nextIndex < 0)
        {
            nextIndex = 0;
        }

        if (database != null && database.levels != null && database.levels.Length > 0)
        {
            if (nextIndex >= database.levels.Length)
            {
                nextIndex = database.levels.Length - 1;
            }
        }

        if (nextIndex > currentSaved)
        {
            PlayerPrefs.SetInt("level_index", nextIndex);
            PlayerPrefs.Save();
        }
    }

    private void LoadLevel(int index)
    {
        if (database == null)
        {
            return;
        }

        if (database.levels == null || database.levels.Length == 0)
        {
            return;
        }

        if (index < 0)
        {
            index = 0;
        }

        if (index >= database.levels.Length)
        {
            index = database.levels.Length - 1;
        }

        currentLevelIndex = index;

        if (LevelUIManager.Instance != null)
        {
            LevelUIManager.Instance.HideWin();
        }

        CleanupDynamicObjects();

        LevelData data = database.levels[currentLevelIndex];
        if (data == null)
        {
            return;
        }

        SplinePathDefinition chosenSpline = ResolveSpline(data.splinePathIndex);

        if (ShooterQueueManager.Instance != null)
        {
            ShooterQueueManager.Instance.defaultSplinePath = chosenSpline;

            if (data.queueLayout.columnCount > 0 && data.queueLayout.depthCount > 0)
            {
                ShooterQueueManager.Instance.ApplyLayout(data.queueLayout);
            }
        }

        if (BlockGridManager.Instance != null)
        {
            UnhookGridEvents();

            BlockGridManager.Instance.OnAllBlocksCleared += HandleLevelCompleted;

            BlockGridManager.Instance.layout = data.layout;
            BlockGridManager.Instance.BuildLevel();
        }

        SpawnShooters(data);
    }

    private void HandleLevelCompleted()
    {
        if (LevelUIManager.Instance != null)
        {
            LevelUIManager.Instance.ShowWin();
        }
    }

    private SplinePathDefinition ResolveSpline(int idx)
    {
        if (splinePathsInScene == null || splinePathsInScene.Length == 0)
        {
            return null;
        }

        if (idx < 0)
        {
            idx = 0;
        }

        if (idx >= splinePathsInScene.Length)
        {
            idx = splinePathsInScene.Length - 1;
        }

        return splinePathsInScene[idx];
    }

    private void SpawnShooters(LevelData data)
    {
        if (ShooterQueueManager.Instance == null)
        {
            return;
        }

        Vector3 spawnPos = ShooterQueueManager.Instance.GetQueueSpawnPosition();

        List<ShooterQueueManager.SpawnedPlacement> placements = new List<ShooterQueueManager.SpawnedPlacement>();

        if (data != null && data.queuePlacements != null)
        {
            for (int i = 0; i < data.queuePlacements.Length; i++)
            {
                ShooterQueuePlacementData p = data.queuePlacements[i];

                if (p.prefab == null)
                {
                    continue;
                }

                Shooter inst = Instantiate(p.prefab, spawnPos, p.prefab.transform.rotation);

                int shots = p.shots;
                if (shots < 0)
                {
                    shots = 0;
                }

                inst.ApplyShots(shots);

                inst.linkGroupId = p.groupId > 0 ? p.groupId : 0;

                ShooterQueueManager.SpawnedPlacement sp;
                sp.shooter = inst;
                sp.column = p.column;
                sp.depth = p.depth;

                placements.Add(sp);
            }
        }

        ShooterQueueManager.Instance.InitializeQueueFromPlacements(placements);
    }

    private void CleanupDynamicObjects()
    {
        Bullet[] bullets = FindObjectsOfType<Bullet>(true);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                Destroy(bullets[i].gameObject);
            }
        }

        Shooter[] shooters = FindObjectsOfType<Shooter>(true);
        for (int i = 0; i < shooters.Length; i++)
        {
            if (shooters[i] != null)
            {
                Destroy(shooters[i].gameObject);
            }
        }
    }

    private void UnhookGridEvents()
    {
        if (BlockGridManager.Instance != null)
        {
            BlockGridManager.Instance.OnAllBlocksCleared -= HandleLevelCompleted;
        }
    }
}