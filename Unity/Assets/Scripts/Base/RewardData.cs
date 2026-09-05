using UnityEngine;

// 보상의 큰 카테고리
public enum RewardCategory { Stat, Weapon }

// 스탯 보상일 경우 어떤 스탯인지
public enum StatType { MaxHealth, MoveSpeed, AttackPower, FullHeal } // ★ FullHeal 추가

[System.Serializable]
public struct RewardItem
{
    public string rewardName;
    [TextArea] public string description;
    
    public RewardCategory category;

    [Header("Stat Reward Settings")]
    public StatType statType;
    public float statValue; // 체력은 20, 이속/공격력은 0.1(10%) 등

    [Header("Weapon Reward Settings (Placeholder)")]
    public string weaponID; // 무기 식별용 ID
    public GameObject weaponPrefab; // 새로 추가할 무기 프리팹
}