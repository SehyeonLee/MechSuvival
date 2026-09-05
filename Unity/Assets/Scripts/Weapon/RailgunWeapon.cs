using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class RailgunWeapon : WeaponBase
{
    [Header("Railgun Settings")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float baseFireRate = 2f; 

    [Header("Audio Settings")]
    public AudioClip fireSoundClip; 

    private float currentFireRate;
    private float nextFireTime = 0f;
    private ObjectPool<GameObject> projectilePool;
    
    private PlayerAimAssist aimAssist;
    private AudioSource fireAudioSource;

    private void Awake()
    {
        projectilePool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(projectilePrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 10,
            maxSize: 50
        );
        fireAudioSource = GetComponent<AudioSource>();
    }

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats); 
        
        // 위치를 발밑(0,0,0)으로 강제 고정하던 코드를 삭제하여 에디터상에 배치된 높이를 유지합니다.
        aimAssist = GetComponentInParent<PlayerAimAssist>();
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;
        
        if (level == 1) currentFireRate = baseFireRate;
        else if (level == 2) currentFireRate = baseFireRate * 0.8f;
        else if (level == 3) currentFireRate = baseFireRate * 0.6f;
        else if (level >= 4) currentFireRate = baseFireRate * 0.45f;
    }

    void Update()
    {
        if (!IsOwner) return;

        if (playerStats != null && playerStats.isDown.Value) return;

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + currentFireRate;

            Vector3 targetPoint;

            if (aimAssist != null)
            {
                aimAssist.GetTargetedObject(out targetPoint);
            }
            else
            {
                targetPoint = firePoint.position + firePoint.forward * 50f;
            }

            // ★ 궤적이 위로 꺾이는 것을 방지하고 완벽한 수평 발사를 위해 Y축 좌표를 총구의 높이와 일치시킵니다.
            targetPoint.y = firePoint.position.y;

            Vector3 fireDirection = (targetPoint - firePoint.position).normalized;
            Quaternion correctedRotation = Quaternion.LookRotation(fireDirection);

            FireRpc(firePoint.position, correctedRotation);
        }
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        GameObject projObj = projectilePool.Get();
        
        projObj.transform.position = spawnPos;
        projObj.transform.rotation = spawnRot;

        RailgunProjectile projectile = projObj.GetComponent<RailgunProjectile>();
        if (projectile != null)
        {
            projectile.Setup(projectilePool, GetFinalDamage());
        }

        projObj.GetComponent<NetworkObject>().Spawn();
        
        PlayFireSoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayFireSoundRpc()
    {
        if (fireAudioSource != null && fireSoundClip != null)
        {
            fireAudioSource.PlayOneShot(fireSoundClip);
        }
    }
}