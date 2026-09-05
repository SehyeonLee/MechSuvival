using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class RailgunProjectile : NetworkBehaviour
{
    public float speed = 40f; 
    public float lifeTime = 3f; 
    
    private ObjectPool<GameObject> myPool;
    private Rigidbody rb;
    private float currentLifeTime;
    private int finalCalculatedDamage;
    private TrailRenderer trailRenderer;
    
    private bool isReleased = false; // ★ 중복 반납 방지 플래그

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; 
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        trailRenderer = GetComponent<TrailRenderer>();
    }

    public void Setup(ObjectPool<GameObject> pool, int damage)
    {
        myPool = pool;
        currentLifeTime = lifeTime;
        finalCalculatedDamage = damage;
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

        if (other.CompareTag("Enemy"))
        {
            if (other.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.TakeDamage(finalCalculatedDamage);
            }
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Ground"))
        {
            ReturnToPool();
        }
    }

    private void ReturnToPool()
    {
        if (isReleased) return; // ★ 중복 방지
        isReleased = true;

        NetworkObject netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            netObj.Despawn(false); 
        }
        
        if (myPool != null)
        {
            myPool.Release(gameObject);
        }
    }
}