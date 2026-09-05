using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerStats : NetworkBehaviour
{
    [Header("Player Attributes")]
    public float baseMoveSpeed = 8f;
    public float currentMoveSpeed = 8f;
    
    // ★ 체력을 네트워크 변수로 변경하여 서버-클라이언트 완벽 동기화
    public NetworkVariable<int> maxHealth = new NetworkVariable<int>(100);
    public NetworkVariable<int> currentHealth = new NetworkVariable<int>(100);
    
    public float attackPowerMultiplier = 1f;

    private int myPodCallCount = 0;

    [Header("Down & Revive System")]
    public NetworkVariable<bool> isDown = new NetworkVariable<bool>(false);
    public NetworkVariable<float> reviveProgress = new NetworkVariable<float>(0f);
    
    public float timeToRevive = 3f; 
    public float reviveRange = 3f;  

    [Header("Invincibility (I-Frame)")]
    private float invincibilityDuration = 0.5f;
    private float lastDamageTime = -10f;
    private Renderer[] renderers; // 깜빡임 연출용
    [Header("Audio System")]
    public AudioSource localAudioSource; 
    // ※ 인스펙터에서 할당할 오디오 클립은 향후 사운드 매니저가 생기면 구조를 변경할 수 있습니다.
    public AudioClip hitSoundClip;
    public AudioClip expSoundClip;



    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        localAudioSource = GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 서버 측에서 스폰 시 기본 장착된 무기들의 초기화 진행
        if (IsServer)
        {
            WeaponBase[] preAttachedWeapons = GetComponentsInChildren<WeaponBase>();
            foreach (var weapon in preAttachedWeapons)
            {
                weapon.Setup(this);
            }
        }
    }

    void Update()
    {
        if (!IsServer) return;

        if (isDown.Value)
        {
            HandleReviveProgress();
        }
    }

    public void TakeDamage(int damage)
    {
        if (!IsServer || isDown.Value) return;

        // ★ 피격 무적 시간 체크
        if (Time.time < lastDamageTime + invincibilityDuration) return;
        lastDamageTime = Time.time;

        currentHealth.Value -= damage;
        NotifyPlayerHitClientRpc();
        if (currentHealth.Value <= 0)
        {
            currentHealth.Value = 0;
            isDown.Value = true;
            reviveProgress.Value = 0f;
            
            GameManager.Instance.CheckGameOver(); 
        }
    }

    private void HandleReviveProgress()
    {
        bool isSomeoneRevivingMe = false;

        // 접속 클라이언트 목록 대신, 씬에 활성화된 모든 PlayerStats 객체를 스캔합니다. (유니티 6 최신 규격 적용)
        PlayerStats[] allPlayers = UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (var otherPlayer in allPlayers)
        {
            // 네트워크 ID 비교가 아닌, 물리적 메모리 객체 비교로 자기 자신을 정확히 제외합니다.
            if (otherPlayer == this) continue; 

            // 상대방이 살아있는 상태일 때만 구조자로 판정합니다.
            if (!otherPlayer.isDown.Value)
            {
                float dist = Vector3.Distance(transform.position, otherPlayer.transform.position);
                if (dist <= reviveRange)
                {
                    isSomeoneRevivingMe = true;
                    break;
                }
            }
        }

        if (isSomeoneRevivingMe)
        {
            reviveProgress.Value += Time.deltaTime;
            if (reviveProgress.Value >= timeToRevive)
            {
                isDown.Value = false;
                currentHealth.Value = maxHealth.Value / 2; 
                reviveProgress.Value = 0f;
            }
        }
        else
        {
            if (reviveProgress.Value > 0)
            {
                reviveProgress.Value = Mathf.Max(0, reviveProgress.Value - Time.deltaTime);
            }
        }
    }

    public float GetMoveSpeed() => currentMoveSpeed;

    // ★ UI가 호출하는 무기 목록 반환 함수를 '실시간 자식 스캔'으로 변경
    public System.Collections.Generic.List<WeaponBase> GetEquippedWeapons()
    {
        // 네트워크를 통해 자식으로 붙은 무기들까지 클라이언트가 알아서 모두 감지합니다.
        return new System.Collections.Generic.List<WeaponBase>(GetComponentsInChildren<WeaponBase>());
    }

    public void CallDropPod()
    {
        if (!IsOwner) return;

        if (GameManager.Instance.availablePodPodTokens.Value <= 0) return;

        myPodCallCount++;
        GameManager.Instance.RequestPodSpawnRpc(transform.position, myPodCallCount);
    }

    public void EarnExp(int amount)
    {
        if (!IsServer) return; 

        // 1. 공용 경험치 증가 연산 (GameManager 내부는 ServerRpc가 아닌 일반 공용 함수로 구조 변경 권장)
        GameManager.Instance.AddExperienceServerRpc(amount); 
        
        // 2. 이 캐릭터의 '소유자(클라이언트)'에게만 사운드를 재생하라고 전용 RPC를 발송합니다.
        PlayExpSoundRpc();
    }

    [Rpc(SendTo.Server)]
    public void SubmitRewardChoiceRpc(int rewardIndex)
    {
        RewardItem chosenReward = RewardDatabase.Instance.GetRewardByIndex(rewardIndex);

        if (chosenReward.category == RewardCategory.Stat) ApplyStatReward(chosenReward);
        else if (chosenReward.category == RewardCategory.Weapon) ApplyWeaponReward(chosenReward);

        GameManager.Instance.PlayerFinishedRewardSelection();
    }

    private void ApplyStatReward(RewardItem item)
    {
        switch (item.statType)
        {
            case StatType.MaxHealth:
                int hpIncrease = Mathf.RoundToInt(item.statValue);
                maxHealth.Value += hpIncrease;
                currentHealth.Value += hpIncrease;
                break;
            case StatType.FullHeal: // ★ 신규 보상 타입 처리
                int slightIncrease = Mathf.RoundToInt(item.statValue);
                maxHealth.Value += slightIncrease;
                currentHealth.Value = maxHealth.Value; // 최대 체력 갱신 후 즉시 완전 회복 처리
                break;
            case StatType.MoveSpeed:
                currentMoveSpeed += (baseMoveSpeed * item.statValue); 
                break;
            case StatType.AttackPower:
                attackPowerMultiplier += item.statValue;
                break;
        }
    }

    private void ApplyWeaponReward(RewardItem item)
    {
        // 획득 시 중복 검사 로직도 실시간 스캔 기반으로 변경
        WeaponBase[] currentWeapons = GetComponentsInChildren<WeaponBase>();
        WeaponBase existingWeapon = null;

        foreach (var w in currentWeapons)
        {
            if (w.weaponID == item.weaponID) 
            {
                existingWeapon = w;
                break;
            }
        }

        if (existingWeapon != null)
        {
            existingWeapon.LevelUp();
        }
        else if (item.weaponPrefab != null)
        {
            GameObject newWeaponObj = Instantiate(item.weaponPrefab, transform.position, Quaternion.identity);
            NetworkObject weaponNetObj = newWeaponObj.GetComponent<NetworkObject>();
            weaponNetObj.Spawn();
            
            weaponNetObj.TrySetParent(transform); 
            newWeaponObj.transform.localPosition = Vector3.zero; 

            WeaponBase newWeapon = newWeaponObj.GetComponent<WeaponBase>();
            if (newWeapon != null)
            {
                newWeapon.Setup(this);
            }
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyPlayerHitClientRpc()
    {
        // ★ IsOwner(본인)일 때만 화면 진동과 피격음을 재생하여 타인과 분리합니다.
        if (IsOwner)
        {
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.15f);
            
            if (localAudioSource != null && hitSoundClip != null)
            {
                localAudioSource.PlayOneShot(hitSoundClip);
            }
        }
        StartCoroutine(BlinkRoutine()); 
    }

    // ★ 무적 시간 동안 캐릭터 모델링이 깜빡이는 시각적 피드백
    private IEnumerator BlinkRoutine()
    {
        float elapsed = 0f;
        bool isVisible = true;
        while(elapsed < invincibilityDuration)
        {
            isVisible = !isVisible;
            foreach(var r in renderers) { if (r != null) r.enabled = isVisible; }
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }
        foreach(var r in renderers) { if (r != null) r.enabled = true; }
    }

    [Rpc(SendTo.Owner)] 
    private void PlayExpSoundRpc()
    {
        if (localAudioSource != null && expSoundClip != null)
        {
            localAudioSource.PlayOneShot(expSoundClip);
        }
    }
}