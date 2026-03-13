using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainSceneUI : MonoBehaviour
{
    public string levelsSceneName = "LevelsScene";
    public TMP_Text playButtonText;

    private void OnEnable()
    {
        RefreshPlayButtonText();
    }

    private void Start()
    {
        RefreshPlayButtonText();
    }

    public void RefreshPlayButtonText()
    {
        if (playButtonText == null)
        {
            return;
        }

        int idx = PlayerPrefs.GetInt("level_index", 0);
        int displayLevel = idx + 1;

        playButtonText.text = "LEVEL " + displayLevel.ToString();
    }

    public void Play()
    {
        SceneManager.LoadScene(levelsSceneName);
    }
}