using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using System.Linq;

[System.Serializable]
public class AudioClipData
{
    public string key;
    public AudioClip clip;
}

public class SoundManager : Singleton<SoundManager>
{
    public enum SoundType { BGM, Sfx, UI, LoopSfx }

    [Header("Settings")]
    public AudioMixer audioMixer;
    public int sfxPoolSize = 20; // SFX 풀 크기 (동시 재생 가능 수)

    [Header("Audio Clips (Inspector)")]
    [SerializeField] private List<AudioClipData> audioClipDataList;

    // 런타임 최적화용 딕셔너리 (Key: 이름, Value: 클립 리스트(랜덤재생용))
    private Dictionary<string, List<AudioClip>> clipDictionary = new Dictionary<string, List<AudioClip>>();

    // 오디오 소스 레퍼런스
    private AudioSource bgmSource;
    private AudioSource loopSfxSource; // 지속 효과음용

    // SFX 오브젝트 풀
    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();

    // 믹서 그룹 캐싱
    private Dictionary<SoundType, AudioMixerGroup> mixerGroups = new Dictionary<SoundType, AudioMixerGroup>();

    protected override void Awake()
    {
        base.Awake(); // Singleton의 Awake 호출
        InitializeManager();
    }

    private void InitializeManager()
    {
        // 1. 딕셔너리 구축 (검색 속도 최적화)
        foreach (var data in audioClipDataList)
        {
            if (!clipDictionary.ContainsKey(data.key))
                clipDictionary[data.key] = new List<AudioClip>();

            clipDictionary[data.key].Add(data.clip);
        }

        // 2. 믹서 그룹 캐싱
        mixerGroups.Add(SoundType.BGM, audioMixer.FindMatchingGroups("BGM")[0]);
        mixerGroups.Add(SoundType.Sfx, audioMixer.FindMatchingGroups("Sfx")[0]);
        mixerGroups.Add(SoundType.UI, audioMixer.FindMatchingGroups("UI")[0]);
        mixerGroups.Add(SoundType.LoopSfx, audioMixer.FindMatchingGroups("LoopSfx")[0]);

        // 3. 고정 오디오 소스 생성 (BGM, Loop)
        GameObject bgmObj = new GameObject("@BGM_Source");
        bgmObj.transform.SetParent(transform);
        bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.outputAudioMixerGroup = mixerGroups[SoundType.BGM];
        bgmSource.loop = true;

        GameObject loopObj = new GameObject("@LoopSFX_Source");
        loopObj.transform.SetParent(transform);
        loopSfxSource = loopObj.AddComponent<AudioSource>();
        loopSfxSource.outputAudioMixerGroup = mixerGroups[SoundType.LoopSfx];
        loopSfxSource.loop = true;

        // 4. SFX 오브젝트 풀 생성 (핵심 최적화)
        GameObject poolRoot = new GameObject("@SFX_Pool");
        poolRoot.transform.SetParent(transform);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject go = new GameObject($"SFX_Player_{i}");
            go.transform.SetParent(poolRoot.transform);
            AudioSource source = go.AddComponent<AudioSource>();
            source.outputAudioMixerGroup = mixerGroups[SoundType.Sfx];
            source.playOnAwake = false;
            go.SetActive(false); // 비활성 상태로 대기
            sfxPool.Enqueue(source);
        }
    }

    // =========================================================================
    // Play Functions
    // =========================================================================

    public void PlayBGM(string key, float volume = 1f)
    {
        AudioClip clip = GetClip(key);
        if (clip == null) return;

        if (bgmSource.clip == clip && bgmSource.isPlaying) return; // 이미 재생 중이면 패스

        bgmSource.clip = clip;
        bgmSource.volume = volume;
        bgmSource.Play();
    }

    // 짧은 효과음 (UI, 타격음 등) - 오브젝트 풀 사용
    public void PlaySFX(string key, float volume = 1f, float pitch = 1f)
    {
        AudioClip clip = GetClip(key);
        if (clip == null) return;

        // 풀에서 꺼내오기
        if (sfxPool.Count > 0)
        {
            AudioSource source = sfxPool.Dequeue();
            source.gameObject.SetActive(true);

            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch; // 타격감 변화를 위해 피치 조절 가능
            source.outputAudioMixerGroup = mixerGroups[SoundType.Sfx];
            source.Play();

            // 재생이 끝나면 반납하는 코루틴 실행
            StartCoroutine(ReturnToPool(source, clip.length));
        }
        else
        {
            // 풀이 모자라면 임시로 PlayOneShot 사용 (비상 대책)
            // 혹은 풀 사이즈를 늘리는 로직 추가 가능
            bgmSource.PlayOneShot(clip, volume);
            Debug.LogWarning("SFX Pool is empty! Playing with OneShot.");
        }
    }

    // 풀 반납 코루틴
    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
        source.gameObject.SetActive(false);
        sfxPool.Enqueue(source);
    }

    // =========================================================================
    // Utility Functions
    // =========================================================================

    // 내부적으로 랜덤 선택 로직 포함
    private AudioClip GetClip(string key)
    {
        if (string.IsNullOrEmpty(key) || !clipDictionary.ContainsKey(key))
        {
            Debug.LogWarning($"[SoundManager] Audio Key Not Found: {key}");
            return null;
        }

        List<AudioClip> clips = clipDictionary[key];
        if (clips.Count == 0) return null;

        if (clips.Count == 1) return clips[0];
        return clips[Random.Range(0, clips.Count)]; // 랜덤 재생
    }

    public void SetVolume(SoundType type, float value)
    {
        // -80 ~ 0 ~ 20 (dB) 범위 조절 필요. 슬라이더 값(0~1)을 dB로 변환
        // value가 0.0001 이하면 -80dB(Mute) 처리
        float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20;
        audioMixer.SetFloat(type.ToString(), db);
    }
}