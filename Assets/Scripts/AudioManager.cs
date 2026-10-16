using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    public AudioSource bgmSource;
    public AudioSource sfxSource;

    [Header("BGM Tracks")]
    public AudioClip bgmCyberNeon;
    public AudioClip bgmChiptuneRush;
    public AudioClip bgmDeepGravity;

    [Header("SFX Clips")]
    public AudioClip sfxJump;
    public AudioClip sfxGem;
    public AudioClip sfxFall;
    public AudioClip sfxVictory;

    [Header("BGM Selection (0: Cyber Neon, 1: Chiptune, 2: Deep Gravity)")]
    public int currentBgmIndex = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 初始化音訊播放器
        // 讀取音量設定
        float bgmVol = PlayerPrefs.GetFloat("BGM_Volume", 0.65f);
        float sfxVol = PlayerPrefs.GetFloat("SFX_Volume", 0.85f);

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = bgmVol;
            bgmSource.playOnAwake = false;
        }
        else bgmSource.volume = bgmVol;

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.volume = sfxVol;
            sfxSource.playOnAwake = false;
        }
        else sfxSource.volume = sfxVol;

        // 讀取玩家偏好的 BGM 版本
        currentBgmIndex = PlayerPrefs.GetInt("SelectedBGM", 0);

        // 自動載入音訊檔案
        StartCoroutine(LoadAllClipsRoutine());
    }

    IEnumerator LoadAllClipsRoutine()
    {
        string audioDir = Path.Combine(Application.dataPath, "Audio");

        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "bgm_cyber_neon.wav"), clip => bgmCyberNeon = clip));
        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "bgm_chiptune_rush.wav"), clip => bgmChiptuneRush = clip));
        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "bgm_deep_gravity.wav"), clip => bgmDeepGravity = clip));

        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "sfx_jump.wav"), clip => sfxJump = clip));
        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "sfx_gem.wav"), clip => sfxGem = clip));
        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "sfx_fall.wav"), clip => sfxFall = clip));
        yield return StartCoroutine(LoadClip(Path.Combine(audioDir, "sfx_victory.wav"), clip => sfxVictory = clip));

        // 載入完成後播放選定的 BGM
        PlaySelectedBGM();
    }

    IEnumerator LoadClip(string filePath, System.Action<AudioClip> onLoaded)
    {
        if (!File.Exists(filePath)) yield break;

        string url = "file:///" + filePath.Replace("\\", "/");
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                onLoaded?.Invoke(clip);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] 音訊載入失敗: {filePath} - {www.error}");
            }
        }
    }

    public void PlaySelectedBGM()
    {
        AudioClip clipToPlay = bgmCyberNeon;
        if (currentBgmIndex == 1) clipToPlay = bgmChiptuneRush;
        else if (currentBgmIndex == 2) clipToPlay = bgmDeepGravity;

        if (clipToPlay != null && bgmSource != null)
        {
            if (bgmSource.clip == clipToPlay && bgmSource.isPlaying) return;
            bgmSource.clip = clipToPlay;
            bgmSource.Play();
            Debug.Log($"[AudioManager] 正在播放 BGM 版本 {currentBgmIndex}: {clipToPlay.name}");
        }
    }

    public void SwitchBGM(int index)
    {
        currentBgmIndex = Mathf.Clamp(index, 0, 2);
        PlayerPrefs.SetInt("SelectedBGM", currentBgmIndex);
        PlayerPrefs.Save();
        PlaySelectedBGM();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void SetBGMVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (bgmSource != null) bgmSource.volume = volume;
        PlayerPrefs.SetFloat("BGM_Volume", volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = volume;
        PlayerPrefs.SetFloat("SFX_Volume", volume);
        PlayerPrefs.Save();
    }

    public void PlayJump()
    {
        if (sfxSource != null && sfxJump != null) sfxSource.PlayOneShot(sfxJump, 0.9f);
    }

    public void PlayGem()
    {
        if (sfxSource != null && sfxGem != null) sfxSource.PlayOneShot(sfxGem, 1.0f);
    }

    public void PlayFall()
    {
        if (sfxSource != null && sfxFall != null) sfxSource.PlayOneShot(sfxFall, 0.95f);
    }

    public void PlayVictory()
    {
        if (bgmSource != null) bgmSource.Stop();
        if (sfxSource != null && sfxVictory != null) sfxSource.PlayOneShot(sfxVictory, 1.0f);
    }
}
