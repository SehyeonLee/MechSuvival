using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(NetworkObject))]
public class MicroMissile : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float speed = 15f;
    public float rotateSpeed = 8f;
    public float initialLifeTime = 5f;

    [Header("Explosion Settings")]
    public float splashRadius = 2.5f;
    public GameObject explosionEffectPrefab;

    private Transform targetEnemy;
    private int finalDamage;
    private bool isSplashEnabled;
    private LayerMask enemyLayer;
    private Vector3 lastValidDirection;

    private ObjectPool<GameObject> myPool;
    private float currentLifeTime;
    private TrailRenderer trailRenderer;
    
    private bool isReleased = false;

    private void Awake()
    {
        trailRenderer = GetComponent<TrailRenderer>();
    }

    public void Initialize(Transform target, int damage, bool splash, LayerMask layer, ObjectPool<GameObject> pool)
    {
        targetEnemy = target;
        finalDamage = damage;
        isSplashEnabled = splash;
        enemyLayer = layer;
        myPool = pool;
        currentLifeTime = initialLifeTime;
        isReleased = false; 

        transform.rotation = Quaternion.LookRotation(Vector3.up + transform.forward);

        if (targetEnemy != null)
        {
            lastValidDirection = (targetEnemy.position - transform.position).normalized;
        }
        else
        {
            lastValidDirection = transform.forward;
        }

        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }
    }

    void Update()
    {
        if (!IsServer) return;

        currentLifeTime -= Time.deltaTime;
        if (currentLifeTime <= 0f)
        {
            Explode();
            return;
        }

        Vector3 targetDirection;
        if (targetEnemy != null)
        {
            targetDirection = (targetEnemy.position - transform.position).normalized;
            lastValidDirection = targetDirection; 
        }
        else
        {
            targetDirection = lastValidDirection;
        }

        float flightProgress = 1f - (currentLifeTime / initialLifeTime);
        Vector3 arcDirection = Vector3.Slerp(Vector3.up, targetDirection, Mathf.Clamp01(flightProgress * 2.5f));

        if (arcDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(arcDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotateSpeed * Time.deltaTime);
        }

        // ★ 터널링(Tunneling) 방지: 다음 프레임에 이동할 궤적을 사전에 검사하여 지형 충돌을 보장합니다.
        float moveDistance = speed * Time.deltaTime;
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, moveDistance))
        {
            if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("Wall"))
            {
                transform.position = hit.point; // 표면에 정확히 밀착
                Explode();
                return;
            }
        }

        transform.Translate(Vector3.forward * moveDistance);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        // 적격 타겟 혹은 지형지물 태그 인식 시 폭발 처리
        if (other.CompareTag("Enemy") || other.CompareTag("Ground") || other.CompareTag("Wall"))
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (isReleased) return; 
        isReleased = true;

        if (isSplashEnabled)
        {
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, splashRadius, enemyLayer);
            foreach (var col in hitEnemies)
            {
                if (col.TryGetComponent<EnemyBase>(out var enemy))
                {
                    enemy.TakeDamage(finalDamage);
                }
            }
        }
        else
        {
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, 1.5f, enemyLayer);
            if (hitEnemies.Length > 0 && hitEnemies[0].TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.TakeDamage(finalDamage);
            }
        }
        PlayExplosionEffectRpc(transform.position);

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

    [Rpc(SendTo.ClientsAndHost)]
    public void PlayExplosionEffectRpc(Vector3 position)
    {
        if (explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(explosionEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
}