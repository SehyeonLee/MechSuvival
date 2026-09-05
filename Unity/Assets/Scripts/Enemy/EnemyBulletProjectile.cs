using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrailRenderer))]
public class EnemyBulletProjectile : NetworkBehaviour
{
    public float speed = 2.5f;
    public float lifeTime = 10f;
    
    private int currentDamage; // 발사체로부터 전달받은 데미지 저장용
    private ObjectPool<GameObject> myPool;
    private Rigidbody rb;
    private float currentLifeTime;
    private TrailRenderer trailRenderer;
    private bool isReleased = false; // ★ 중복 반납 방지 플래그

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; 
        trailRenderer = GetComponent<TrailRenderer>();
    }

    // ★ Setup 인자에 damage를 추가하여 발사 주체의 공격력을 부여받습니다.
    public void Setup(ObjectPool<GameObject> pool, int damage)
    {
        myPool = pool;
        currentDamage = damage;
        currentLifeTime = lifeTime;
        isReleased = false; // ★ 초기화
        
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = transform.forward * speed;

        if (!IsServer) return;

        currentLifeTime -= Time.fixedDeltaTime;
        if (currentLifeTime <= 0f)
        {
            ReturnToPool();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerStats>(out var playerStats))
            {
                playerStats.TakeDamage(currentDamage); // 부여받은 데미지 적용
                Debug.Log($"서버: 적 총알 적중! 플레이어 남은 체력: {playerStats.currentHealth}");
            }
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReleased) return; // ★ 중복 방지
        isReleased = true;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj.IsSpawned)
        {
            netObj.Despawn(false); 
        }
        
        if (myPool != null)
        {
            myPool.Release(gameObject);
        }
    }
}