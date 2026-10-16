using System;
using System.Collections;
using System.Collections.Generic;
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
    public AudioClip sfxBuy;
    public AudioClip sfxEquip;
    public AudioClip sfxHighscore;

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

        // 讀取音量設定 (相容 BGM_Volume 與 SFX_Volume)
        float bgmVol = PlayerPrefs.GetFloat("BGM_Volume", 0.7f);
        float sfxVol = PlayerPrefs.GetFloat("SFX_Volume", 0.8f);

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.volume = bgmVol;
            bgmSource.spatialBlend = 0f; // 純 2D 音訊，避免 3D 距離衰減
            bgmSource.playOnAwake = false;
        }
        else
        {
            bgmSource.volume = bgmVol;
            bgmSource.spatialBlend = 0f;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.volume = sfxVol;
            sfxSource.spatialBlend = 0f; // 純 2D 音訊
            sfxSource.playOnAwake = false;
        }
        else
        {
            sfxSource.volume = sfxVol;
            sfxSource.spatialBlend = 0f;
        }

        // 讀取玩家偏好的 BGM 版本 (相容 BGM_Track_Index 與 SelectedBGM)
        currentBgmIndex = PlayerPrefs.GetInt("BGM_Track_Index", PlayerPrefs.GetInt("SelectedBGM", 0));

        // 啟動全方位音訊載入
        StartCoroutine(LoadAllClipsRoutine());
    }

    IEnumerator LoadAllClipsRoutine()
    {
        // 1. 同步加載：優先透過 Resources 或直接二進位解析 (無 WebRequest 延遲與路徑問題)
        bgmCyberNeon = LoadClipSmart("bgm_cyber_neon", "bgm_cyber_neon.wav");
        bgmChiptuneRush = LoadClipSmart("bgm_chiptune_rush", "bgm_chiptune_rush.wav");
        bgmDeepGravity = LoadClipSmart("bgm_deep_gravity", "bgm_deep_gravity.wav");

        sfxJump = LoadClipSmart("sfx_jump", "sfx_jump.wav");
        sfxGem = LoadClipSmart("sfx_gem", "sfx_gem.wav");
        sfxFall = LoadClipSmart("sfx_fall", "sfx_fall.wav");
        sfxVictory = LoadClipSmart("sfx_victory", "sfx_victory.wav");
        sfxBuy = LoadClipSmart("sfx_shop_buy", "sfx_shop_buy.wav");
        sfxEquip = LoadClipSmart("sfx_equip", "sfx_equip.wav");
        sfxHighscore = LoadClipSmart("sfx_highscore", "sfx_highscore.wav");

        // 2. 若二進位解析失敗，嘗試使用 URI 編碼後的 UnityWebRequest 協程補強
        if (bgmCyberNeon == null || sfxJump == null)
        {
            yield return StartCoroutine(FallbackWebRequestRoutine());
        }

        // 3. 防護網：若連實體檔案都沒有，即時由演算法合成高品質 8-bit 音效備案，確保永不無聲！
        EnsureFallbackSfx();

        // 4. 載入完成後立即播放玩家選定的 BGM
        PlaySelectedBGM();
    }

    AudioClip LoadClipSmart(string resName, string fileName)
    {
        // 策略 A: Unity Resources (專案打包內建)
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + resName);
        if (clip != null)
        {
            Debug.Log($"[AudioManager] 成功自 Resources 載入音效: {resName}");
            return clip;
        }

        // 策略 B: 磁碟多路徑直接二進位讀取 (支援 StreamingAssets, Data目錄, 遊戲主目錄)
        string[] searchDirs = new string[]
        {
            Path.Combine(Application.streamingAssetsPath, "Audio"),
            Application.streamingAssetsPath,
            Path.Combine(Application.dataPath, "StreamingAssets", "Audio"),
            Path.Combine(Application.dataPath, "Audio"),
            Path.Combine(Application.dataPath, "..", "Audio"),
            Path.Combine(Application.dataPath, "..", "StreamingAssets", "Audio"),
            Path.Combine(Application.dataPath, "..", "ThiefGameUnity_Data", "Audio"),
            Path.Combine(Application.dataPath, "..", "ThiefGameUnity_Data", "StreamingAssets", "Audio")
        };

        foreach (string dir in searchDirs)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            string fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                try
                {
                    byte[] rawBytes = File.ReadAllBytes(fullPath);
                    clip = LoadWavBytes(resName, rawBytes);
                    if (clip != null)
                    {
                        Debug.Log($"[AudioManager] 成功自磁碟解析 PCM WAV: {fullPath}");
                        return clip;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AudioManager] 解析 {fullPath} 失敗: {ex.Message}");
                }
            }
        }

        return null;
    }

    IEnumerator FallbackWebRequestRoutine()
    {
        string[] searchDirs = new string[]
        {
            Path.Combine(Application.streamingAssetsPath, "Audio"),
            Path.Combine(Application.dataPath, "Audio"),
            Path.Combine(Application.dataPath, "..", "Audio")
        };

        foreach (string dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;

            if (bgmCyberNeon == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "bgm_cyber_neon.wav"), c => bgmCyberNeon = c));
            if (bgmChiptuneRush == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "bgm_chiptune_rush.wav"), c => bgmChiptuneRush = c));
            if (bgmDeepGravity == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "bgm_deep_gravity.wav"), c => bgmDeepGravity = c));
            if (sfxJump == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_jump.wav"), c => sfxJump = c));
            if (sfxGem == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_gem.wav"), c => sfxGem = c));
            if (sfxFall == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_fall.wav"), c => sfxFall = c));
            if (sfxVictory == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_victory.wav"), c => sfxVictory = c));
            if (sfxBuy == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_shop_buy.wav"), c => sfxBuy = c));
            if (sfxEquip == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_equip.wav"), c => sfxEquip = c));
            if (sfxHighscore == null) yield return StartCoroutine(LoadClipViaWebRequest(Path.Combine(dir, "sfx_highscore.wav"), c => sfxHighscore = c));
        }
    }

    IEnumerator LoadClipViaWebRequest(string filePath, Action<AudioClip> onLoaded)
    {
        if (!File.Exists(filePath)) yield break;

        // 使用 Uri.AbsoluteUri 正確編碼中文與特殊字元路徑
        string url = new Uri(filePath).AbsoluteUri;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                onLoaded?.Invoke(clip);
            }
        }
    }

    // ==================== 純 C# PCM WAV 二進位解析器 ====================
    public static AudioClip LoadWavBytes(string clipName, byte[] wavBytes)
    {
        if (wavBytes == null || wavBytes.Length < 44) return null;

        // 驗證 RIFF 與 WAVE 標頭
        if (wavBytes[0] != 'R' || wavBytes[1] != 'I' || wavBytes[2] != 'F' || wavBytes[3] != 'F') return null;
        if (wavBytes[8] != 'W' || wavBytes[9] != 'A' || wavBytes[10] != 'V' || wavBytes[11] != 'E') return null;

        int channels = 2;
        int sampleRate = 44100;
        int bitDepth = 16;
        int pos = 12;

        while (pos < wavBytes.Length - 8)
        {
            string chunkId = System.Text.Encoding.ASCII.GetString(wavBytes, pos, 4);
            int chunkSize = BitConverter.ToInt32(wavBytes, pos + 4);
            pos += 8;

            if (chunkId == "fmt ")
            {
                channels = BitConverter.ToInt16(wavBytes, pos + 2);
                sampleRate = BitConverter.ToInt32(wavBytes, pos + 4);
                bitDepth = BitConverter.ToInt16(wavBytes, pos + 14);
            }
            else if (chunkId == "data")
            {
                int bytesPerSample = bitDepth / 8;
                if (bytesPerSample <= 0) bytesPerSample = 2;
                int sampleCount = chunkSize / bytesPerSample;
                float[] samples = new float[sampleCount];

                if (bitDepth == 16)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int byteIdx = pos + i * 2;
                        if (byteIdx + 1 < wavBytes.Length)
                        {
                            short val = BitConverter.ToInt16(wavBytes, byteIdx);
                            samples[i] = val / 32768f;
                        }
                    }
                }
                else if (bitDepth == 24)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int byteIdx = pos + i * 3;
                        if (byteIdx + 2 < wavBytes.Length)
                        {
                            int val = (wavBytes[byteIdx] | (wavBytes[byteIdx + 1] << 8) | ((sbyte)wavBytes[byteIdx + 2] << 16));
                            samples[i] = val / 8388608f;
                        }
                    }
                }
                else if (bitDepth == 32)
                {
                    for (int i = 0; i < sampleCount; i++)
                    {
                        int byteIdx = pos + i * 4;
                        if (byteIdx + 3 < wavBytes.Length)
                        {
                            samples[i] = BitConverter.ToSingle(wavBytes, byteIdx);
                        }
                    }
                }

                int totalFrames = sampleCount / Mathf.Max(1, channels);
                AudioClip clip = AudioClip.Create(clipName, totalFrames, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }

            pos += chunkSize;
        }

        return null;
    }

    // ==================== 演算法備用音效生成 (保證 100% 不靜音) ====================
    void EnsureFallbackSfx()
    {
        if (sfxJump == null) sfxJump = GenerateToneClip("sfx_jump_fallback", 0.15f, f => Mathf.Lerp(320f, 680f, f));
        if (sfxGem == null) sfxGem = GenerateChimeClip("sfx_gem_fallback", 0.22f, 880f, 1320f);
        if (sfxFall == null) sfxFall = GenerateToneClip("sfx_fall_fallback", 0.4f, f => Mathf.Lerp(380f, 90f, f));
        if (sfxVictory == null) sfxVictory = GenerateArpeggioClip("sfx_victory_fallback", new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }, 0.5f);
        if (sfxBuy == null) sfxBuy = GenerateChimeClip("sfx_buy_fallback", 0.2f, 587f, 880f);
        if (sfxEquip == null) sfxEquip = GenerateToneClip("sfx_equip_fallback", 0.15f, f => Mathf.Lerp(440f, 880f, f));
        if (sfxHighscore == null) sfxHighscore = GenerateArpeggioClip("sfx_highscore_fallback", new float[] { 440f, 554.37f, 659.25f, 880f }, 0.6f);
    }

    AudioClip GenerateToneClip(string name, float duration, Func<float, float> freqCurve)
    {
        int sampleRate = 44100;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];
        float phase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float freq = freqCurve(t);
            phase += 2f * Mathf.PI * freq / sampleRate;
            float envelope = 1f - t;
            samples[i] = Mathf.Sin(phase) * envelope * 0.7f;
        }

        AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip GenerateChimeClip(string name, float duration, float freq1, float freq2)
    {
        int sampleRate = 44100;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];
        int half = totalSamples / 2;
        float phase = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / totalSamples;
            float freq = (i < half) ? freq1 : freq2;
            phase += 2f * Mathf.PI * freq / sampleRate;
            float envelope = 1f - (i < half ? (float)i / half * 0.4f : (float)(i - half) / (totalSamples - half));
            samples[i] = Mathf.Sin(phase) * envelope * 0.65f;
        }

        AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    AudioClip GenerateArpeggioClip(string name, float[] notes, float duration)
    {
        int sampleRate = 44100;
        int totalSamples = Mathf.FloorToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];
        int noteSamples = totalSamples / notes.Length;

        for (int n = 0; n < notes.Length; n++)
        {
            float freq = notes[n];
            float phase = 0f;
            int start = n * noteSamples;
            int end = Mathf.Min(start + noteSamples, totalSamples);

            for (int i = start; i < end; i++)
            {
                float t = (float)(i - start) / noteSamples;
                phase += 2f * Mathf.PI * freq / sampleRate;
                float envelope = 1f - t * 0.5f;
                samples[i] = Mathf.Sin(phase) * envelope * 0.7f;
            }
        }

        AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ==================== 播放控制介面 ====================
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
        PlayerPrefs.SetInt("BGM_Track_Index", currentBgmIndex);
        PlayerPrefs.SetInt("SelectedBGM", currentBgmIndex);
        PlayerPrefs.Save();
        PlaySelectedBGM();
    }

    public void PlayBgmTrack(int index) => SwitchBGM(index);

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

    public void SetBgmVolume(float volume) => SetBGMVolume(volume);

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (sfxSource != null) sfxSource.volume = volume;
        PlayerPrefs.SetFloat("SFX_Volume", volume);
        PlayerPrefs.Save();
    }

    public void SetSfxVolume(float volume) => SetSFXVolume(volume);

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

    public void PlayBuy()
    {
        if (sfxSource != null && sfxBuy != null) sfxSource.PlayOneShot(sfxBuy, 1.0f);
    }

    public void PlayEquip()
    {
        if (sfxSource != null && sfxEquip != null) sfxSource.PlayOneShot(sfxEquip, 1.0f);
    }

    public void PlayHighscore()
    {
        if (sfxSource != null && sfxHighscore != null) sfxSource.PlayOneShot(sfxHighscore, 1.0f);
    }
}
