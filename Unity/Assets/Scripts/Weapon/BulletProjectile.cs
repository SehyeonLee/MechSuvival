using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(TrailRenderer))]
public class BulletProjectile : NetworkBehaviour
{
    public float speed = 20f;
    public int baseDamage = 10; 
    public float lifeTime = 3f; 
    
    private ObjectPool<GameObject> myPool;
    private Rigidbody rb;
    private float currentLifeTime;
    
    private float currentDamageMultiplier = 1f;
    private int finalCalculatedDamage;
    private TrailRenderer trailRenderer;
    
    private bool isReleased = false; // ★ 중복 반납 방지 플래그

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; 
        trailRenderer = GetComponent<TrailRenderer>();
    }

    public void Setup(ObjectPool<GameObject> pool, int damage)
    {
        myPool = pool;
        currentLifeTime = lifeTime;
        finalCalculatedDamage = damage;
        isReleased = false; // ★ 발사 시 플래그 초기화
        
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

        if (other.CompareTag("Enemy"))
        {
            if (other.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.TakeDamage(finalCalculatedDamage);
            }
        }

        ReturnToPool();
    }

    private void ReturnToPool()
    {
        if (isReleased) return; // ★ 이미 반납되었다면 연산 취소
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