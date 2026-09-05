using UnityEngine;

public class BossBeamDamage : MonoBehaviour
{
    public int beamDamage = 15;
    public float damageTickRate = 0.5f; // 0.5초마다 다단 히트
    
    private float nextDamageTime = 0f;

    private void OnTriggerStay(Collider other)
    {
        if (Time.time >= nextDamageTime && other.CompareTag("Player"))
        {
            if (other.TryGetComponent<PlayerStats>(out var stats))
            {
                // 로컬 플레이어 본인일 때만 데미지 판정 (중복 적용 방지)
                if (stats.IsOwner) 
                {
                    stats.TakeDamage(beamDamage);
                    nextDamageTime = Time.time + damageTickRate;
                }
            }
        }
    }
}