using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(AudioSource))]
public class BombDropperWeapon : WeaponBase
{
    [Header("Bomb Settings")]
    public GameObject bombPrefab;
    public Transform dropPoint; // ★ 텅 빈, 좌표만 있는 오브젝트를 할당하여 투하 위치로 사용합니다.
    public float dropInterval = 2.5f;
    public LayerMask enemyLayer;

    [Header("Effect & Audio")]
    public GameObject explosionEffectPrefab;
    public AudioClip dropSoundClip;
    public AudioClip explosionSoundClip;
    
    private AudioSource localAudioSource;

    private float currentSplashRadius = 3f;
    private float nextDropTime = 0f;

    private ObjectPool<GameObject> bombPool;
    private ObjectPool<GameObject> effectPool;

    private void Awake()
    {
        localAudioSource = GetComponent<AudioSource>();
        localAudioSource.spatialBlend = 0f; 

        bombPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(bombPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 10,
            maxSize: 30
        );

        effectPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(explosionEffectPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 10,
            maxSize: 30
        );
    }

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats);
        
        // 무기 본체는 플레이어의 등 뒤에 부유하도록 위치를 고정합니다.
        transform.localPosition = new Vector3(0f, 0, 0f); 
        transform.localRotation = Quaternion.identity;
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;
        
        if (level == 1) currentSplashRadius = 3f;
        else if (level == 2) currentSplashRadius = 4f;
        else if (level == 3) currentSplashRadius = 5f;
        else if (level >= 4) currentSplashRadius = 6.5f;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (playerStats != null && playerStats.isDown.Value) return;

        if (Time.time >= nextDropTime)
        {
            nextDropTime = Time.time + dropInterval;
            
            // ★ 지정된 빈 오브젝트(dropPoint)의 좌표를 가져와 생성 위치로 사용합니다.
            Vector3 spawnPos = dropPoint != null ? dropPoint.position : transform.position;
            DropBombRpc(spawnPos, GetFinalDamage(), currentSplashRadius);
        }
    }

    [Rpc(SendTo.Server)]
    private void DropBombRpc(Vector3 pos, int damage, float radius)
    {
        GameObject bombObj = bombPool.Get();
        bombObj.transform.position = pos;
        bombObj.transform.rotation = Quaternion.identity;

        TimeBombProjectile bomb = bombObj.GetComponent<TimeBombProjectile>();
        if (bomb != null)
        {
            bomb.Setup(bombPool, this, damage, radius, enemyLayer);
        }

        bombObj.GetComponent<NetworkObject>().Spawn();
        PlayDropSoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayDropSoundRpc()
    {
        if (localAudioSource != null && dropSoundClip != null)
        {
            localAudioSource.PlayOneShot(dropSoundClip);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayExplosionEffectRpc(Vector3 pos)
    {
        if (effectPool != null)
        {
            GameObject fx = effectPool.Get();
            fx.transform.position = pos;
            
            PooledExplosionEffect pooledFx = fx.GetComponent<PooledExplosionEffect>();
            if (pooledFx != null)
            {
                pooledFx.Setup(effectPool, explosionSoundClip);
            }
        }
    }
}