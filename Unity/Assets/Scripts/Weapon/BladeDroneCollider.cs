using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BladeDroneCollider : MonoBehaviour
{
    private BladeDrone parentWeapon;
    
    // 하나의 적에게 프레임 단위로 다단 히트가 들어가는 것을 방지하기 위한 쿨타임
    public float damageTickRate = 0.5f; 
    private float nextDamageTime = 0f;

    public void Setup(BladeDrone weapon)
    {
        parentWeapon = weapon;
        
        // 회전하는 무기이므로 트리거 판정 필수
        Collider col = GetComponent<Collider>();
        col.isTrigger = true; 
    }

    private void OnTriggerStay(Collider other)
    {
        // 데미지 연산은 서버에서만 처리
        if (parentWeapon != null && parentWeapon.IsServer)
        {
            if (other.CompareTag("Enemy") && Time.time >= nextDamageTime)
            {
                if (other.TryGetComponent<EnemyBase>(out var enemy))
                {
                    enemy.TakeDamage(parentWeapon.GetFinalDamage());
                    nextDamageTime = Time.time + damageTickRate;
                }
            }
        }
    }
}