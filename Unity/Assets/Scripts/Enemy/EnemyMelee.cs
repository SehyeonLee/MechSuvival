using UnityEngine;

public class EnemyMelee : EnemyBase
{
    [Header("Melee Settings")]
    public int damage = 5; // 10에서 5로 감소
    public float attackCooldown = 1.5f;
    
    private float nextAttackTime = 0f;

    protected override void Update()
    {
        base.Update(); 

        if (!IsServer || isDead || currentTarget == null) return;

        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.SetDestination(currentTarget.position);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!IsServer) return;

        if (Time.time >= nextAttackTime && collision.gameObject.CompareTag("Player"))
        {
            if (collision.gameObject.TryGetComponent<PlayerStats>(out var playerStats))
            {
                playerStats.TakeDamage(damage);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }
}