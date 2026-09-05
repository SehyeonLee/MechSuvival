using UnityEngine;
using UnityEngine.Pool;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class PooledExplosionEffect : MonoBehaviour
{
    public float lifeTime = 2f; // 파티클 재생 지속 시간
    private ObjectPool<GameObject> myPool;
    private AudioSource audioSource;
    
    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // 3D 사운드 처리 (폭발 위치에서 소리가 나도록 설정)
        audioSource.spatialBlend = 1f; 
        audioSource.playOnAwake = false;
    }

    public void Setup(ObjectPool<GameObject> pool, AudioClip explosionSound)
    {
        myPool = pool;
        
        if (audioSource != null && explosionSound != null)
        {
            audioSource.PlayOneShot(explosionSound);
        }
        
        StartCoroutine(AutoReleaseRoutine());
    }

    private IEnumerator AutoReleaseRoutine()
    {
        yield return new WaitForSeconds(lifeTime);
        if (myPool != null) myPool.Release(gameObject);
    }
}