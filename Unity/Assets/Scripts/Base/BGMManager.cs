using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("Audio Clips")]
    public AudioClip normalBgmClip;
    public AudioClip eliteBgmClip;

    [Header("Volume Settings")]
    [Range(0f, 1f)]
    public float maxVolume = 0.5f; // BGM의 원래 크기를 제어하는 퍼블릭 변수
    public float fadeOutLeadTime = 15f; // 엘리트 스폰 15초 전부터 페이드아웃

    private AudioSource bgmSource;
    private bool isFadingOut = false;
    private bool isElitePhase = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        bgmSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        // 씬 진입 시 노멀 BGM 재생
        if (bgmSource != null && normalBgmClip != null)
        {
            bgmSource.clip = normalBgmClip;
            bgmSource.volume = maxVolume;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    private void Update()
    {
        if (GameManager.Instance == null || EnemySpawner.Instance == null) return;
        if (isElitePhase) return;

        float currentTime = GameManager.Instance.gameTimer.Value;
        float eliteSpawnTime = EnemySpawner.Instance.phase2EndTime;

        // 1단계: 엘리트 스폰 15초 전 페이드아웃 연산 시작
        if (currentTime >= eliteSpawnTime - fadeOutLeadTime && currentTime < eliteSpawnTime)
        {
            if (!isFadingOut)
            {
                isFadingOut = true;
                StartCoroutine(FadeOutRoutine(fadeOutLeadTime));
            }
        }
        // 2단계: 엘리트 스폰 시점 도달 시 BGM 강제 전환
        else if (currentTime >= eliteSpawnTime)
        {
            isElitePhase = true;
            StopAllCoroutines(); // 진행 중인 페이드아웃 코루틴을 강제 중지
            PlayEliteBGM();
        }
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; 
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        bgmSource.volume = 0f;
    }

    private void PlayEliteBGM()
    {
        if (bgmSource != null && eliteBgmClip != null)
        {
            bgmSource.Stop();
            bgmSource.clip = eliteBgmClip;
            bgmSource.volume = maxVolume;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }
    
    // 게임 클리어 또는 게임 오버 시 호출하여 BGM을 멈추는 공용 함수
    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }
}