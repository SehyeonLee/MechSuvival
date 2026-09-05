using System.Collections.Generic;
using UnityEngine;

public class RewardDatabase : MonoBehaviour
{
    public static RewardDatabase Instance { get; private set; }

    [Header("All Available Rewards")]
    // 유니티 인스펙터에서 보상을 좌르륵 추가할 수 있는 리스트
    public List<RewardItem> rewardPool; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 인덱스로 보상 정보 가져오기
    public RewardItem GetRewardByIndex(int index)
    {
        if (index >= 0 && index < rewardPool.Count)
        {
            return rewardPool[index];
        }
        return default;
    }

    // 겹치지 않는 랜덤 보상 3개의 인덱스 뽑기
    public int[] GetRandomRewardIndices(int count = 3)
    {
        List<int> availableIndices = new List<int>();
        for (int i = 0; i < rewardPool.Count; i++) availableIndices.Add(i);

        int[] selected = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break;
            
            int randomIndex = Random.Range(0, availableIndices.Count);
            selected[i] = availableIndices[randomIndex];
            availableIndices.RemoveAt(randomIndex); // 중복 방지
        }
        return selected;
    }
}