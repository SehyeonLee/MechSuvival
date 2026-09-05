using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

public class MicroMissilePod : WeaponBase
{
    [Header("Missile Settings")]
    public GameObject missilePrefab;
    public Transform[] launchPoints; 
    public float targetRange = 20f;
    public LayerMask enemyLayer;

    private float fireInterval = 3f;
    private int missileCount = 1;
    private bool hasSplash = false;
    private float nextFireTime = 0f;
    private int currentLaunchPointIndex = 0;

    // 미사일 풀 변수 추가
    private ObjectPool<GameObject> missilePool;

    private void Awake()
    {
        missilePool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(missilePrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 10,
            maxSize: 50
        );
    }

    public override void Setup(PlayerStats stats)
    {
        base.Setup(stats);
    }

    protected override void ApplyLevelEffect()
    {
        int level = currentLevel.Value;
        if (level == 1)      { missileCount = 1; fireInterval = 3f; hasSplash = false; }
        else if (level == 2) { missileCount = 2; fireInterval = 3f; hasSplash = false; }
        else if (level == 3) { missileCount = 2; fireInterval = 2f; hasSplash = false; }
        else if (level >= 4) { missileCount = 2; fireInterval = 2f; hasSplash = true; }
    }

    void Update()
    {
        if (!IsServer) return;

        if (Time.time >= nextFireTime)
        {
            Transform target = FindClosestEnemy();
            if (target != null)
            {
                nextFireTime = Time.time + fireInterval;
                for (int i = 0; i < missileCount; i++)
                {
                    LaunchMissile(target);
                }
            }
        }
    }

    private Transform FindClosestEnemy()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, targetRange, enemyLayer);
        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (var col in enemies)
        {
            float dist = Vector3.Distance(transform.position, col.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestTarget = col.transform;
            }
        }
        return closestTarget;
    }

    private void LaunchMissile(Transform target)
    {
        Transform spawnPoint = transform;
        if (launchPoints != null && launchPoints.Length > 0)
        {
            spawnPoint = launchPoints[currentLaunchPointIndex];
            currentLaunchPointIndex = (currentLaunchPointIndex + 1) % launchPoints.Length;
        }

        // Instantiate 대신 풀에서 가져옵니다.
        GameObject missileObj = missilePool.Get();
        missileObj.transform.position = spawnPoint.position;
        missileObj.transform.rotation = spawnPoint.rotation;
        
        MicroMissile missileScript = missileObj.GetComponent<MicroMissile>();
        if (missileScript != null)
        {
            // 풀 참조를 함께 넘겨줍니다.
            missileScript.Initialize(target, GetFinalDamage(), hasSplash, enemyLayer, missilePool);
        }

        missileObj.GetComponent<NetworkObject>().Spawn();
    }
}