using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Pool;

public class EnemySpawner : NetworkBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Enemy Prefabs")]
    public GameObject normalMeleePrefab;
    public GameObject normalRangedPrefab;
    public GameObject veteranMeleePrefab;   
    public GameObject veteranRangedPrefab;  
    public GameObject elitePrefab;

    [Header("Enemy Bullet Pool Settings")]
    public GameObject enemyBulletPrefab;
    private ObjectPool<GameObject> enemyBulletPool;

    [Header("Spawn Settings")]
    public float minSpawnDistance = 15f;
    public float maxSpawnDistance = 25f;
    public float spawnInterval = 1f;
    
    private float nextSpawnTime = 0f;

    [Header("Phase Settings")]
    public float phase2EndTime = 60f; 

    private ObjectPool<GameObject> meleeEnemyPool;
    private ObjectPool<GameObject> rangedEnemyPool;
    private ObjectPool<GameObject> veteranMeleePool;
    private ObjectPool<GameObject> veteranRangedPool;

    private bool hasSpawnedElite = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        enemyBulletPool = new ObjectPool<GameObject>(
            createFunc: () => Instantiate(enemyBulletPrefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 50,
            maxSize: 200
        );

        meleeEnemyPool = CreateEnemyPool(normalMeleePrefab);
        rangedEnemyPool = CreateEnemyPool(normalRangedPrefab);
        veteranMeleePool = CreateEnemyPool(veteranMeleePrefab);
        veteranRangedPool = CreateEnemyPool(veteranRangedPrefab);
    }

    private ObjectPool<GameObject> CreateEnemyPool(GameObject prefab)
    {
        return new ObjectPool<GameObject>(
            createFunc: () => {
                if (prefab == null) return new GameObject("Empty_Enemy"); 
                return Instantiate(prefab);
            },
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => obj.SetActive(false),
            actionOnDestroy: Destroy,
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public void SpawnEnemyBullet(Vector3 position, Quaternion rotation, int damage)
    {
        if (!IsServer) return;

        GameObject bullet = enemyBulletPool.Get();
        bullet.transform.position = position;
        bullet.transform.rotation = rotation;

        EnemyBulletProjectile projectile = bullet.GetComponent<EnemyBulletProjectile>();
        if (projectile != null)
        {
            projectile.Setup(enemyBulletPool, damage);
        }

        bullet.GetComponent<NetworkObject>().Spawn();
    }

    private void Update()
    {
        if (!IsServer) return;
        if (GameManager.Instance == null || GameManager.Instance.isGameEnded) return;

        float currentTime = GameManager.Instance.gameTimer.Value;

        if (currentTime >= phase2EndTime)
        {
            if (!hasSpawnedElite)
            {
                hasSpawnedElite = true;
                SpawnElite();
            }
            return;
        }

        if (Time.time >= nextSpawnTime)
        {
            nextSpawnTime = Time.time + spawnInterval;
            SpawnNormalEnemy(currentTime);
        }
    }

    private void SpawnNormalEnemy(float currentTime)
    {
        // 0.0 ~ 1.0 사이의 진행 비율 산출
        float ratio = Mathf.Clamp01(currentTime / phase2EndTime);
        bool spawnMelee = true;

        // 1. 0.25n 도달 시 원거리 개체 스폰 시작 (근접과 50:50 비율)
        if (ratio >= 0.25f)
        {
            spawnMelee = Random.value < 0.5f;
        }

        ObjectPool<GameObject> selectedPool = null;

        if (spawnMelee)
        {
            // 2. 0.5n 도달 시 강화 근접 개체 스폰 비율 변동
            if (ratio >= 0.5f)
            {
                // 0.5n ~ 0.75n 구간에서 강화 근접 비율이 25% -> 75%로 선형 증가
                float veteranChance = Mathf.Lerp(0.25f, 0.75f, (ratio - 0.5f) / 0.25f);
                selectedPool = (Random.value < veteranChance) ? veteranMeleePool : meleeEnemyPool;
            }
            else
            {
                selectedPool = meleeEnemyPool;
            }
        }
        else
        {
            // 3. 0.75n 도달 시 강화 원거리 개체 스폰 비율 변동
            if (ratio >= 0.75f)
            {
                // 0.75n ~ 1.0n 구간에서 강화 원거리 비율이 25% -> 75%로 선형 증가
                float veteranChance = Mathf.Lerp(0.25f, 0.75f, (ratio - 0.75f) / 0.25f);
                selectedPool = (Random.value < veteranChance) ? veteranRangedPool : rangedEnemyPool;
            }
            else
            {
                selectedPool = rangedEnemyPool;
            }
        }

        Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0f, randomCircle.y);

        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
        {
            GameObject enemy = selectedPool.Get();
            enemy.transform.position = hit.position + Vector3.up * 0.5f;
            enemy.transform.rotation = Quaternion.identity;

            EnemyBase enemyScript = enemy.GetComponent<EnemyBase>();
            if (enemyScript != null)
            {
                // ★ 시간에 비례하여 모든 일반/강화 개체의 체력을 1배(0n) ~ 2배(1n)로 동적 스케일링
                float hpMultiplier = Mathf.Lerp(1f, 2f, ratio);
                enemyScript.Setup(selectedPool, hpMultiplier); 
            }
            enemy.GetComponent<NetworkObject>().Spawn();
        }
    }

    private void SpawnElite()
    {
        if (elitePrefab == null) return;

        Vector3 targetSpawnPos = Vector3.zero;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(targetSpawnPos, out hit, 10f, NavMesh.AllAreas))
        {
            Vector3 finalSpawnPos = hit.position + Vector3.up * 20f;
            GameObject elite = Instantiate(elitePrefab, finalSpawnPos, Quaternion.identity);
            
            EnemyBase eliteScript = elite.GetComponent<EnemyBase>();
            if (eliteScript != null)
            {
                // 보스 개체는 동적 체력 배수를 적용받지 않고 프리팹 기본값을 사용합니다.
                eliteScript.Setup(null, 1f); 
            }
            elite.GetComponent<NetworkObject>().Spawn();
        }
    }

    public void SpawnMeleeEnemiesAround(Vector3 centerPosition, int count)
    {
        if (!IsServer) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(3f, 8f);
            Vector3 spawnPos = centerPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPos, out hit, 5f, NavMesh.AllAreas))
            {
                GameObject enemy = meleeEnemyPool.Get();
                enemy.transform.position = hit.position + Vector3.up * 0.5f;
                enemy.transform.rotation = Quaternion.identity;

                EnemyBase enemyScript = enemy.GetComponent<EnemyBase>();
                if (enemyScript != null)
                {
                    // 보스전 호위 병력은 1n 시점에 소환되므로 2배수 체력을 고정 적용합니다.
                    enemyScript.Setup(meleeEnemyPool, 2f); 
                }
                enemy.GetComponent<NetworkObject>().Spawn();
            }
        }
    }
}