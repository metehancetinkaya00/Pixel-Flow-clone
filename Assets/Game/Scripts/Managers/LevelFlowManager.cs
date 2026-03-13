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

        PlayerPrefs.SetInt("level_index", currentLevelIndex);
        PlayerPrefs.Save();

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
            ShooterQueueManager.Instance.firstRowSelectableCount = data.firstRowSelectableCount;
            ShooterQueueManager.Instance.defaultSplinePath = chosenSpline;
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
    public void CompleteLevelAndSaveProgress()
    {
        int currentsaved = PlayerPrefs.GetInt("level_index",0);
        int nextindex = currentLevelIndex + 1;
        if(nextindex<0)
        {
            nextindex = 0;
        }
        if(database !=null &&database.levels !=null)
        {
            if(nextindex >=database.levels.Length)
            {
                nextindex = database.levels.Length - 1;
            }
        }
        if(nextindex>currentsaved)
        {
            PlayerPrefs.SetInt("level_index", nextindex);
            PlayerPrefs.Save();
        }
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

        if (data == null)
        {
            ShooterQueueManager.Instance.InitializeQueue(new Shooter[0]);
            return;
        }

        if (data.shooterGroups == null)
        {
            ShooterQueueManager.Instance.InitializeQueue(new Shooter[0]);
            return;
        }

        Vector3 spawnPos = Vector3.zero;

        if (ShooterQueueManager.Instance.queueSlots != null && ShooterQueueManager.Instance.queueSlots.Length > 0)
        {
            spawnPos = ShooterQueueManager.Instance.queueSlots[0].position;
        }

        List<Shooter> list = new List<Shooter>();

        for (int g = 0; g < data.shooterGroups.Length; g++)
        {
            ShooterGroup group = data.shooterGroups[g];

            if (group.prefab == null)
            {
                continue;
            }

            int count = group.count;
            if (count < 0)
            {
                count = 0;
            }

            for (int i = 0; i < count; i++)
            {
                Shooter inst = Instantiate(group.prefab, spawnPos, group.prefab.transform.rotation);
                inst.ApplyShots(group.shots);
                list.Add(inst);
            }
        }

        ShooterQueueManager.Instance.InitializeQueue(list.ToArray());
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