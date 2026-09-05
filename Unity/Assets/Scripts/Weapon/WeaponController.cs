using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

public class WeaponController : WeaponBase
{
    [Header("Weapon Settings")]
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float fireRate = 0.5f;

    private float nextFireTime = 0f;
    private ObjectPool<GameObject> bulletPool;
    
    private PlayerAimAssist aimAssist;
    private AudioSource fireAudioSource;
    public bool canFire = true; 

    private void Awake()
    {
        bulletPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(bulletPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 20,
            maxSize: 100
        );
        fireAudioSource = GetComponent<AudioSource>();
        if (SceneManager.GetActiveScene().name == "MainMenuScene")
        {
            canFire = false;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CheckSceneState();
    }

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats); 
        aimAssist = GetComponentInParent<PlayerAimAssist>();
        
        CheckSceneState();
    }

    private void CheckSceneState()
    {
        if (SceneManager.GetActiveScene().name == "TutorialScene" || SceneManager.GetActiveScene().name == "MainMenuScene")
        {
            canFire = false;
        }
        else
        {
            canFire = true;
        }
    }

    void Update()
    {
        if (!IsOwner) return;

        if (playerStats != null && playerStats.isDown.Value) return;

        if (canFire && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + (1f / fireRate);

            Vector3 targetPoint;

            if (aimAssist != null)
            {
                aimAssist.GetTargetedObject(out targetPoint);
            }
            else
            {
                targetPoint = firePoint.position + firePoint.forward * 50f;
            }

            Vector3 fireDirection = (targetPoint - firePoint.position).normalized;
            Quaternion correctedRotation = Quaternion.LookRotation(fireDirection);

            FireRpc(firePoint.position, correctedRotation);
        }
    }

    [Rpc(SendTo.Server)]
    private void FireRpc(Vector3 spawnPos, Quaternion spawnRot)
    {
        GameObject bullet = bulletPool.Get();
        
        bullet.transform.position = spawnPos;
        bullet.transform.rotation = spawnRot;

        BulletProjectile projectile = bullet.GetComponent<BulletProjectile>();
        if (projectile != null)
        {
            projectile.Setup(bulletPool, GetFinalDamage());
        }

        bullet.GetComponent<NetworkObject>().Spawn();
        fireAudioSource.Play();
    }
}