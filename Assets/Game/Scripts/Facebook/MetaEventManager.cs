/*
using UnityEngine;
using Facebook.Unity;
using System.Collections.Generic;

public class MetaEventManager : MonoBehaviour
{
    public static MetaEventManager Instance;

    private bool sdkReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!FB.IsInitialized)
        {
            FB.Init(OnFacebookInitialized, OnHideUnity);
        }
        else
        {
            sdkReady = true;
            ActivateApp();
        }
    }

    private void OnFacebookInitialized()
    {
        if (!FB.IsInitialized)
        {
            Debug.LogError("Facebook SDK initialization failed.");
            return;
        }

        sdkReady = true;
        ActivateApp();
        Debug.Log("Facebook SDK initialized.");
    }

    private void ActivateApp()
    {
        if (!FB.IsInitialized)
        {
            return;
        }

        FB.ActivateApp();
        Debug.Log("Meta ActivateApp sent.");
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && sdkReady)
        {
            ActivateApp();
        }
    }

    private void OnHideUnity(bool isGameShown)
    {
        Time.timeScale = isGameShown ? 1f : 0f;
    }

    private void LogEvent(string eventName)
    {
        if (!sdkReady)
        {
            return;
        }

        FB.LogAppEvent(eventName);
        Debug.Log("Meta Event: " + eventName);
    }

    private void LogEvent(string eventName, Dictionary<string, object> parameters)
    {
        if (!sdkReady)
        {
            return;
        }

        FB.LogAppEvent(eventName, null, parameters);
        Debug.Log("Meta Event: " + eventName);
    }

    public void MainMenuPlayPressed()
    {
        var parameters = new Dictionary<string, object>();
        parameters["level"] = PlayerPrefs.GetInt("level_index", 0) + 1;

        LogEvent("main_menu_play_pressed", parameters);
    }

    public void LevelStart(int level)
    {
        var parameters = new Dictionary<string, object>();
        parameters["level"] = level;

        LogEvent("level_start", parameters);
    }

    public void LevelComplete(int level)
    {
        var parameters = new Dictionary<string, object>();
        parameters["level"] = level;

        LogEvent("level_complete", parameters);
    }

    public void LevelFail(int level)
    {
        var parameters = new Dictionary<string, object>();
        parameters["level"] = level;

        LogEvent("level_fail", parameters);
    }

    public void AdWatched(string placement)
    {
        var parameters = new Dictionary<string, object>();
        parameters["placement"] = placement;
        parameters["level"] = PlayerPrefs.GetInt("level_index", 0) + 1;

        LogEvent("ad_watched", parameters);
    }
}
*/