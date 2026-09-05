using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class TimeBombProjectile : NetworkBehaviour
{
    [Header("Bomb Settings")]
    public float explosionDelay = 2.5f; // 투하 후 폭발까지의 대기 시간

    private ObjectPool<GameObject> myPool;
    private BombDropperWeapon weaponRef;
    private int finalDamage;
    private float splashRadius;
    private LayerMask enemyLayer;
    private float currentTimer;
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true; // 바닥으로 떨어지도록 중력 활성화
    }

    public void Setup(ObjectPool<GameObject> pool, BombDropperWeapon weapon, int damage, float radius, LayerMask layer)
    {
        myPool = pool;
        weaponRef = weapon;
        finalDamage = damage;
        splashRadius = radius;
        enemyLayer = layer;
        currentTimer = explosionDelay;

        // 풀에서 재사용 시 이전 관성이 남아있지 않도록 물리량 초기화
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void Update()
    {
        if (!IsServer) return;

        currentTimer -= Time.deltaTime;
        if (currentTimer <= 0f)
        {
            Explode();
        }
    }

    private void Explode()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, splashRadius, enemyLayer);
        foreach (var col in hitEnemies)
        {
            if (col.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.TakeDamage(finalDamage);
            }
        }

        // 무기 본체에 폭발 이펙트 및 사운드 출력 요청
        if (weaponRef != null)
        {
            weaponRef.PlayExplosionEffectRpc(transform.position);
        }

        // 오브젝트 풀 반환 처리
        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj.IsSpawned) netObj.Despawn(false);
        
        if (myPool != null) myPool.Release(gameObject);
    }
}