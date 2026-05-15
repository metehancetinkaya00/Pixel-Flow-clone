using UnityEngine;
using System.Collections.Generic;

public class LevelFlowManager : MonoBehaviour
{
    public static LevelFlowManager Instance;

    [Header("Database")]
    public LevelDatabase database;
    public int startLevelIndex = 0;

    [Header("Splines")]
    public SplinePathDefinition[] splinePathsInScene;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip startSound;
    [Min(0f)] public float volume = 1f;
    public bool use2DSound = true;

    private int currentLevelIndex;

    private void Awake()
    {
        Instance = this;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = use2DSound ? 0f : 1f;
            audioSource.volume = 1f;
        }
    }

    private void Start()
    {
        currentLevelIndex = PlayerPrefs.GetInt("level_index", startLevelIndex);
        LoadLevel(currentLevelIndex);

        audioSource?.PlayOneShot(startSound, volume);
    }

    private void OnDisable()
    {
        UnhookGridEvents();
    }

    public void LoadNextLevel() => LoadLevel(currentLevelIndex + 1);
    public void ReloadCurrentLevel() => LoadLevel(currentLevelIndex);

    public void CompleteLevelAndSaveProgress()
    {
        int savedIndex = PlayerPrefs.GetInt("level_index", startLevelIndex);
        int nextIndex = currentLevelIndex + 1;

        if (database?.levels != null)
            nextIndex = Mathf.Min(nextIndex, database.levels.Length - 1);

        if (nextIndex > savedIndex)
        {
            PlayerPrefs.SetInt("level_index", nextIndex);
            PlayerPrefs.Save();
        }
    }

    private void LoadLevel(int index)
    {
        if (database?.levels == null || database.levels.Length == 0) return;

        currentLevelIndex = Mathf.Clamp(index, 0, database.levels.Length - 1);

        LevelData data = database.levels[currentLevelIndex];
        if (data == null) return;

        LevelUIManager.Instance?.HideWin();

        CleanupDynamicObjects();

        SplinePathDefinition chosenSpline = ResolveSpline(data.splinePathIndex);

        if (ShooterQueueManager.Instance != null)
        {
            ShooterQueueManager.Instance.defaultSplinePath = chosenSpline;

            if (data.queueLayout.columnCount > 0 && data.queueLayout.depthCount > 0)
                ShooterQueueManager.Instance.ApplyLayout(data.queueLayout);
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
        LevelUIManager.Instance?.ShowWin();
    }

    private void SpawnShooters(LevelData data)
    {
        if (ShooterQueueManager.Instance == null || data.queuePlacements == null) return;

        Vector3 spawnPos = ShooterQueueManager.Instance.GetQueueSpawnPosition();
        var placements = new List<ShooterQueueManager.SpawnedPlacement>();

        foreach (var p in data.queuePlacements)
        {
            if (p.prefab == null) continue;

            Shooter shooter = Instantiate(p.prefab, spawnPos, p.prefab.transform.rotation);
            shooter.ApplyShots(p.shots);
            shooter.linkGroupId = p.groupId > 0 ? p.groupId : 0;

            placements.Add(new ShooterQueueManager.SpawnedPlacement
            {
                shooter = shooter,
                column = p.column,
                depth = p.depth
            });
        }

        ShooterQueueManager.Instance.InitializeQueueFromPlacements(placements);
    }

    private SplinePathDefinition ResolveSpline(int index)
    {
        if (splinePathsInScene == null || splinePathsInScene.Length == 0) return null;
        return splinePathsInScene[Mathf.Clamp(index, 0, splinePathsInScene.Length - 1)];
    }

    private void CleanupDynamicObjects()
    {
        foreach (var bullet in FindObjectsOfType<Bullet>(true))
            Destroy(bullet.gameObject);

        foreach (var shooter in FindObjectsOfType<Shooter>(true))
            Destroy(shooter.gameObject);
    }

    private void UnhookGridEvents()
    {
        if (BlockGridManager.Instance != null)
            BlockGridManager.Instance.OnAllBlocksCleared -= HandleLevelCompleted;
    }
}
