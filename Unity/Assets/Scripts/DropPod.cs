using Unity.Netcode;
using UnityEngine;
using UnityEngine.Pool;

[RequireComponent(typeof(Rigidbody))]
public class DropPod : NetworkBehaviour
{
    public enum PodType { Reward, Heal }
    [Header("Pod Settings")]
    public PodType podType;
    public float healAmount = 50f;

    [Header("Impact Settings")]
    public float impactRadius = 5f;
    public float impactDamage = 100f;
    public LayerMask enemyLayer;
    public GameObject impactEffectPrefab;

    private Rigidbody rb;
    private bool hasHitGround = false;
    private bool isCollected = false;
    private ObjectPool<GameObject> myPool;
    private AudioSource ImpactAudioSource;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ImpactAudioSource = GetComponent<AudioSource>();
    }

    public void Setup(ObjectPool<GameObject> pool)
    {
        myPool = pool;
        hasHitGround = false;
        isCollected = false;
        
        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    private void ReturnToPool()
    {
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

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (hasHitGround) return;

        if (collision.gameObject.CompareTag("Ground"))
        {
            hasHitGround = true;
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;

            ApplyImpactDamage();
            ImpactAudioSource.Play();
            PlayImpactEffectRpc(transform.position); // 변경된 함수 호출
        }
    }

    private void ApplyImpactDamage()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, impactRadius, enemyLayer);
        foreach (Collider enemyCollider in hitEnemies)
        {
            if (enemyCollider.TryGetComponent<EnemyBase>(out var enemy)) 
            { 
                enemy.TakeDamage(Mathf.RoundToInt(impactDamage)); 
            }
        }
    }

    // ★ 최신 RPC 문법 적용 및 이펙트 파괴 로직 추가
    [Rpc(SendTo.ClientsAndHost)]
    public void PlayImpactEffectRpc(Vector3 position)
    {
        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f); // 2초 뒤 메모리에서 해제
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!hasHitGround || isCollected) return;

        if (other.CompareTag("Player"))
        {
            isCollected = true;

            if (other.TryGetComponent<PlayerStats>(out var playerStats))
            {
                // ★ NetworkVariable 값 수정 (.Value 사용)
                int current = playerStats.currentHealth.Value;
                int max = playerStats.maxHealth.Value;
                playerStats.currentHealth.Value = Mathf.Min(max, current + Mathf.RoundToInt(healAmount));
            }

            if (podType == PodType.Reward)
            {
                TriggerRewardSequence(); 
            }
            else
            {
                ReturnToPool(); 
            }
        }
    }
    
    private void TriggerRewardSequence()
    {
        GameManager.Instance.StartRewardSequence();
        ReturnToPool(); 
    }
}