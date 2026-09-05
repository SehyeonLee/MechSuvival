using UnityEngine;
using System.Collections.Generic;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance { get; private set; }

    [Header("Pool Settings")]
    public int poolSize = 20; // 생성해둘 예비 오디오 소스 개수
    
    [Header("Culling Settings")]
    public int maxSameClipConcurrent = 3; // ★ 핵심: 동일한 효과음이 동시에 겹칠 수 있는 최대 한도

    private List<AudioSource> sources = new List<AudioSource>();

    private void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시 파괴 방지
            InitializePool();
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = new GameObject($"SFX_Source_{i}");
            obj.transform.SetParent(transform);
            
            AudioSource src = obj.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f; // 3D 사운드
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 5f;
            src.maxDistance = 30f;
            
            sources.Add(src);
        }
    }

    public void PlaySFX(AudioClip clip, Vector3 position, float volume = 0.035f)
    {
        if (clip == null) return;

        int playingCount = 0;
        AudioSource availableSource = null;

        // 1. 현재 재생 중인 클립 상태 검사 및 빈 슬롯 탐색
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i].isPlaying)
            {
                if (sources[i].clip == clip) 
                {
                    playingCount++;
                }
            }
            else if (availableSource == null)
            {
                availableSource = sources[i];
            }
        }

        // 2. 동일한 사운드가 이미 최대 허용치(3개)만큼 재생 중이라면 출력 강제 취소 (볼륨 폭주 차단)
        if (playingCount >= maxSameClipConcurrent) return;

        // 3. 가용 소스가 없다면 취소 (CPU 최적화)
        if (availableSource == null) return;

        availableSource.transform.position = position;
        availableSource.clip = clip;
        availableSource.volume = volume;
        availableSource.pitch = Random.Range(0.85f, 1.15f); // 위상 중첩 방지용 피치 변형
        availableSource.Play();
    }
}