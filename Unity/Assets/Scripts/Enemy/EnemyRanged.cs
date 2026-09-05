using UnityEngine;
using Unity.Netcode;

public class EnemyRanged : EnemyBase
{
    [Header("Ranged Settings")]
    public int damage = 3; // 총알에 전달할 자체 데미지 추가
    public float stopDistance = 10f;
    public float fireRate = 3f;
    public Transform firePoint;

    [Header("Audio Settings")]
    public AudioClip fireSoundClip; 

    private float nextFireTime = 0f;

    protected override void Update()
    {
        base.Update(); 

        if (!IsServer || isDead || currentTarget == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        if (distanceToTarget > stopDistance)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(currentTarget.position);
            }
        }
        else
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            direction.y = 0; 
            transform.rotation = Quaternion.LookRotation(direction);

            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + (10f / fireRate);
                Shoot();
            }
        }
    }

    private void Shoot()
    {
        // 발사 주체의 데미지를 스포너를 통해 총알에 전달합니다.
        EnemySpawner.Instance.SpawnEnemyBullet(firePoint.position, firePoint.rotation, damage);
        PlayShootSoundRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayShootSoundRpc()
    {
        if (SFXManager.Instance != null && fireSoundClip != null)
        {
            SFXManager.Instance.PlaySFX(fireSoundClip, transform.position, 0.035f);
        }
    }
}