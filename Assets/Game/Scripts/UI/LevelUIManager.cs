using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class LevelUIManager : MonoBehaviour
{
    public static LevelUIManager Instance;

    public CanvasGroup winPanel;
    public float winTweenDuration = 0.25f;

    public bool inputLocked;

    public string mainSceneName = "MainScene";

    private void Awake()
    {
        Instance = this;

        if (winPanel != null)
        {
            winPanel.gameObject.SetActive(false);
            winPanel.alpha = 0f;
            winPanel.interactable = false;
            winPanel.blocksRaycasts = false;
        }

        inputLocked = false;
    }

    public void ShowWin()
    {
        if (winPanel == null)
        {
            return;
        }

        inputLocked = true;

        DOTween.Kill(winPanel);
        DOTween.Kill(winPanel.transform);

        winPanel.gameObject.SetActive(true);
        winPanel.alpha = 0f;
        winPanel.interactable = false;
        winPanel.blocksRaycasts = true;

        winPanel.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);

        Sequence seq = DOTween.Sequence();
        seq.Join(winPanel.DOFade(1f, winTweenDuration));
        seq.Join(winPanel.transform.DOScale(1f, winTweenDuration).SetEase(Ease.OutBack));
        seq.SetUpdate(true);

        seq.OnComplete(() =>
        {
            if (winPanel != null)
            {
                winPanel.interactable = true;
                winPanel.blocksRaycasts = true;
            }
        });
    }

    public void HideWin()
    {
        if (winPanel == null)
        {
            return;
        }

        DOTween.Kill(winPanel);
        DOTween.Kill(winPanel.transform);

        winPanel.alpha = 0f;
        winPanel.interactable = false;
        winPanel.blocksRaycasts = false;
        winPanel.gameObject.SetActive(false);

        inputLocked = false;
    }

    public void OnNextPressed()
    {
        if(LevelFlowManager.Instance !=null)
        {
            LevelFlowManager.Instance.CompleteLevelAndSaveProgress();
        }
        if (LevelFlowManager.Instance != null)
        {
            HideWin();
            LevelFlowManager.Instance.LoadNextLevel();
        }

    }

    public void OnRetryPressed()
    {
        if (LevelFlowManager.Instance != null)
        {
            HideWin();
            LevelFlowManager.Instance.ReloadCurrentLevel();
        }
    }

    public void OnHomePressed()
    {
        if(LevelFlowManager.Instance !=null)
        {
            LevelFlowManager.Instance.CompleteLevelAndSaveProgress();
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(mainSceneName);
    }
}