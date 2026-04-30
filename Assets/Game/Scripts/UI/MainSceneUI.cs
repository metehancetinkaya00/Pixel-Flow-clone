using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainSceneUI : MonoBehaviour
{
    [Header("Scene")]
    public string levelsSceneName = "LevelsScene";

    [Header("Play Button")]
    public TMP_Text playButtonText;

    [Header("Level Labels")]
    public TMP_Text[] levelTexts;

    private void OnEnable()
    {
        RefreshUI();
    }

    private void Start()
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        RefreshPlayButtonText();
        RefreshLevelTexts();
    }

    public void RefreshPlayButtonText()
    {
      

        int idx = PlayerPrefs.GetInt("level_index", 0);
        int displayLevel = idx + 1;

       
    }

    public void RefreshLevelTexts()
    {
        if (levelTexts == null || levelTexts.Length == 0)
        {
            return;
        }

        int idx = PlayerPrefs.GetInt("level_index", 0);
        int currentLevel = idx + 1;

        for (int i = 0; i < levelTexts.Length; i++)
        {
            if (levelTexts[i] == null)
            {
                continue;
            }

            int levelNumber = currentLevel + i;
            levelTexts[i].text = levelNumber.ToString();
        }
    }

    public void Play()
    {
        SceneManager.LoadScene(levelsSceneName);
    }
}