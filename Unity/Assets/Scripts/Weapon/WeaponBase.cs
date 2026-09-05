using Unity.Netcode;
using UnityEngine;

public abstract class WeaponBase : NetworkBehaviour
{
    [Header("Weapon Identity")]
    public string weaponID; // 보상 DB의 ID와 매칭될 고유 이름

    [Header("Base Stats")]
    public NetworkVariable<int> currentLevel = new NetworkVariable<int>(1);
    public int baseDamage = 10;
    public int flatDamageGrowthPerLevel = 5; // 5레벨부터 오르는 순수 추가 데미지

    protected PlayerStats playerStats;

    // 무기 획득 시 초기화
    public virtual void Setup(PlayerStats stats)
    {
        playerStats = stats;
        ApplyLevelEffect(); // 1레벨 기믹 적용
        transform.localPosition = Vector3.zero; // 무기 위치를 플레이어 중심으로 고정
        transform.localRotation = Quaternion.identity; // 무기 회전을 초기화
    }

    // 레벨업 트리거 (서버에서만 호출됨)
    public void LevelUp()
    {
        if (!IsServer) return;
        currentLevel.Value++;
        ApplyLevelEffect();
        Debug.Log($"서버: {weaponID} 무기가 {currentLevel.Value}레벨로 상승!");
    }

    // 레벨에 따른 기믹 변화 (자식 클래스에서 오버라이드하여 구현)
    protected virtual void ApplyLevelEffect()
    {
        // 예시: 1~4레벨은 자식 클래스에서 드론 개수, 범위 등을 늘리도록 구현
        // 5레벨 이상은 기본적으로 GetFinalDamage()에서 자동으로 연산되므로 빈칸으로 두어도 무방합니다.
    }

    // ★ 최종 데미지 산출 공식 (자식 클래스들이 적을 때릴 때 이 함수를 호출하여 데미지를 받음)
    public int GetFinalDamage()
    {
        int rawDamage = baseDamage;
        int level = currentLevel.Value;

        // 5레벨 이상일 경우 추가 데미지 부여 (4레벨 초과분만큼)
        if (level > 4)
        {
            rawDamage += (level - 4) * flatDamageGrowthPerLevel;
        }

        // 플레이어의 공격력 스탯 배율(%)을 곱함
        float multiplier = playerStats != null ? playerStats.attackPowerMultiplier : 1f;
        
        return Mathf.RoundToInt(rawDamage * multiplier);
    }
}