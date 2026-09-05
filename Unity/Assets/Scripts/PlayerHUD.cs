using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class PlayerHUD : NetworkBehaviour
{
    public static PlayerHUD Instance { get; private set; }
    
    [Header("UI Canvas")]
    public GameObject hudCanvas;

    [Header("Top Bar UI")]
    public Slider expBar;
    public TextMeshProUGUI timeText;
    public TextMeshProUGUI tokenText;

    [Header("Left Bar UI")]
    public Slider myHpBar;
    public TextMeshProUGUI myHpText;
    public Slider allyHpBar;
    public TextMeshProUGUI allyHpText;
    public GameObject allyHpUIContainer;

    [Header("Bottom Left UI (Weapon Info)")]
    public TextMeshProUGUI weaponListText;
    public TextMeshProUGUI fireModeText;
    public TextMeshProUGUI boostSkillText; 

    [Header("Bottom Right UI (Stat Info)")]
    public TextMeshProUGUI statInfoText;

    [Header("Radar System")]
    public RadarSystem radarSystem;

    [Header("Fade Effect")]
    public Image fadeBlackImage; 

    [Header("Operator Alert UI")]
    public GameObject operatorUIContainer;
    public TextMeshProUGUI operatorText;
    public float bossWarningLeadTime = 10f; // 보스 출현 10초 전 경고

    [Header("Pause UI")]
    public GameObject pausePanel;

    private PlayerStats myStats;
    private PlayerStats allyStats;
    private PlayerController myController; 

    // 레벨업 추적용 변수
    private int previousRequiredExp = -1;

    // 오퍼레이터 대사 데이터베이스
    private string[] levelUpMessages = new string[]
    {
        "실적 충족 확인.\n보급 호출권을 지급합니다.",
        "실적을 달성했습니다.\n보급 호출권을 지급합니다.",
        "실적 달성 확인\n보급 포드 호출이\n가능합니다.",
        "실적 달성.\n보급 호출권을 지급.\n필요시 호출하십시오."
    };
    private string[] consecutiveLevelUpMessages = new string[]
    {
        "호출권을 지급, 아니\n그 사이에 또 실적을?\n호출권을 지급합니다.",
        "또 실적을 달성했군요.\n보급 호출권을 추가로 지급합니다.",
        "연속 실적 달성 확인.\n보급 호출권을 추가로 지급합니다.",
        "실적 연속 달성.\n몰이사냥 전법이네요.\n호출권 지급합니다."
    };

    private string[] hp50Messages = new string[] { "경고.\n기체 손상률 50% 돌파.\n파손에 주의하십시오." };
    private string[] hp25Messages = new string[] { "위험.\n기체 손상률 75% 이상.\n시스템 정지가 임박했습니다." };
    private string[] bossMessages = new string[] { "경고.\n고위험군 개체의 강하를\n감지했습니다.\n대비하십시오." };
    // ★ 신규 추가: 게임 오버 및 강화 개체 대사
    private string[] gameOverMessages = new string[] 
    { 
        "기체 완파 확인.\n작전 실패.\n강제 귀환 프로토콜을 가동합니다.",
        "전 기체 신호 단절.\n임무를 실패했습니다.\n철수합니다." 
    };
    private string[] veteranMeleeMessages = new string[] { "경고.\n강화 개체의 접근이\n확인되었습니다." };
    private string[] veteranRangedMessages = new string[] { "경고.\n원거리 강화 개체가\n투입되었습니다." };

    private bool warnedVeteranMelee = false;
    private bool warnedVeteranRanged = false;
    private string[] eliteDefeatMessages = new string[] 
    { 
        "고위험군 개체\n신호 단절 확인.\n귀환 절차를 준비하십시오.",
        "목표 개체 파괴 확인.\n수고하셨습니다.\n복귀를 허가합니다." 
    };

    private Coroutine typingCoroutine;
    private Coroutine hideCoroutine;
    
    // 코루틴 통제 및 상태 식별 변수
    private bool isOperatorActive = false;
    private bool isShowingLevelUp = false; 

    // 경고 트리거 중복 방지 플래그
    private bool warnedHP50 = false;
    private bool warnedHP25 = false;
    private bool warnedBoss = false;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Instance = this; 
            hudCanvas.SetActive(true);
            
            if (fadeBlackImage != null)
            {
                Color c = fadeBlackImage.color;
                c.a = 0f;
                fadeBlackImage.color = c;
                fadeBlackImage.gameObject.SetActive(false);
            }

            if (operatorUIContainer != null)
            {
                operatorUIContainer.SetActive(false);
            }
            
            myStats = GetComponent<PlayerStats>();
            myController = GetComponent<PlayerController>(); 
            
            if (radarSystem != null) 
            {
                radarSystem.Setup(transform);
            }
        }
        else
        {
            hudCanvas.SetActive(false); 
        }
    }

    void Update()
    {
        if (!IsOwner || myStats == null) return;

        UpdateTopBar();
        UpdateHPBars();
        UpdateBottomInfo(); 
        CheckOperatorAlerts(); // 신규: 실시간 경고 조건 검사
    }

    // --- 오퍼레이터 알림 시스템 ---

    private void CheckOperatorAlerts()
    {
        // 1. 체력 손상 경고
        float hpPercent = (float)myStats.currentHealth.Value / myStats.maxHealth.Value;
        
        if (hpPercent <= 0.25f && !warnedHP25)
        {
            warnedHP25 = true;
            warnedHP50 = true; 
            ShowAlert(hp25Messages[0]);
        }
        else if (hpPercent <= 0.50f && hpPercent > 0.25f && !warnedHP50)
        {
            warnedHP50 = true;
            ShowAlert(hp50Messages[0]);
        }

        if (hpPercent > 0.25f) warnedHP25 = false;
        if (hpPercent > 0.50f) warnedHP50 = false;

        // 2. 시간에 따른 적 출현 경고
        if (EnemySpawner.Instance != null && GameManager.Instance != null)
        {
            float currentTime = GameManager.Instance.gameTimer.Value;
            float phase2End = EnemySpawner.Instance.phase2EndTime;

            // 0.5n: 강화 근접 개체 출현 경고
            if (currentTime >= phase2End * 0.5f && !warnedVeteranMelee)
            {
                warnedVeteranMelee = true;
                ShowAlert(veteranMeleeMessages[0]);
            }

            // 0.75n: 강화 원거리 개체 출현 경고
            if (currentTime >= phase2End * 0.75f && !warnedVeteranRanged)
            {
                warnedVeteranRanged = true;
                ShowAlert(veteranRangedMessages[0]);
            }

            // 보스 강하 경고
            if (!warnedBoss && currentTime >= phase2End - bossWarningLeadTime && currentTime < phase2End)
            {
                warnedBoss = true;
                ShowAlert(bossMessages[0]);
            }
        }
    }

    public void ShowLevelUpAlert()
    {
        string msg;
        // 알림 패널이 떠있는 상태이면서, 그 알림이 레벨업 알림일 경우에만 연속 레벨업으로 판정합니다.
        if (isOperatorActive && isShowingLevelUp)
        {
            msg = consecutiveLevelUpMessages[Random.Range(0, consecutiveLevelUpMessages.Length)];
        }
        else
        {
            msg = levelUpMessages[Random.Range(0, levelUpMessages.Length)];
        }
        ShowAlert(msg, true);
    }
    public void ShowEliteDefeatAlert()
    {
        string msg = eliteDefeatMessages[Random.Range(0, eliteDefeatMessages.Length)];
        // 일반 경고 메시지 규격을 사용하므로 isLevelUpMessage는 false로 전달합니다.
        ShowAlert(msg, false); 
    }
    public void ShowGameOverAlert()
    {
        string msg = gameOverMessages[Random.Range(0, gameOverMessages.Length)];
        ShowAlert(msg, false); 
    }

    public void ShowAlert(string message, bool isLevelUpMessage = false)
    {
        if (operatorUIContainer == null || operatorText == null) return;

        // 기존 진행 중인 타이핑 및 창 닫기 코루틴 강제 중단
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);

        isOperatorActive = true;
        isShowingLevelUp = isLevelUpMessage; // 상태 식별자 갱신
        operatorUIContainer.SetActive(true);
        
        typingCoroutine = StartCoroutine(TypeTextRoutine(message));
    }

    private IEnumerator TypeTextRoutine(string message)
    {
        operatorText.text = "";
        
        foreach (char c in message)
        {
            operatorText.text += c;
            yield return new WaitForSeconds(0.05f); 
        }

        hideCoroutine = StartCoroutine(HideOperatorRoutine(3f));
    }

    private IEnumerator HideOperatorRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        operatorUIContainer.SetActive(false);
        isOperatorActive = false;
        isShowingLevelUp = false; // 패널 은닉 시 상태 초기화
    }

    private void UpdateTopBar()
    {
        if (GameManager.Instance == null) return;

        if (previousRequiredExp == -1)
        {
            previousRequiredExp = GameManager.Instance.requiredExp.Value;
        }
        else if (GameManager.Instance.requiredExp.Value > previousRequiredExp)
        {
            previousRequiredExp = GameManager.Instance.requiredExp.Value;
            ShowLevelUpAlert();
        }

        expBar.maxValue = GameManager.Instance.requiredExp.Value;
        expBar.value = GameManager.Instance.sharedExp.Value;
        tokenText.text = $"호출권: {GameManager.Instance.availablePodPodTokens.Value}";

        float currentTime = GameManager.Instance.gameTimer.Value;
        int minutes = Mathf.FloorToInt(currentTime / 60f);
        int seconds = Mathf.FloorToInt(currentTime % 60f);
        timeText.text = string.Format("진행 시간: {0:00}:{1:00}", minutes, seconds);
    }

    private void UpdateBottomInfo()
    {
        if (statInfoText != null)
        {
            float atkPercent = myStats.attackPowerMultiplier * 100f;
            statInfoText.text = $"공격력: {atkPercent:0}%\n이동 속도: {myStats.GetMoveSpeed():0.0}";
        }

        if (fireModeText != null)
        {
            // 수동 조작이 제거되었으므로 고정 텍스트를 출력합니다.
            fireModeText.text = "<color=#00FF00>사격: [자동]</color>";
        }

        if (boostSkillText != null && myController != null)
        {
            float cd = myController.GetBoostCooldownTimer();
            if (cd <= 0)
            {
                boostSkillText.text = "<color=#00FFFF>부스터\n[READY]</color>";
            }
            else
            {
                boostSkillText.text = $"<color=#FF8800>부스터\n[{cd:0.0}s]</color>";
            }
        }

        if (weaponListText != null)
        {
            var weapons = myStats.GetEquippedWeapons();
            if (weapons.Count == 0)
            {
                weaponListText.text = "장착 무장 없음";
            }
            else
            {
                string weaponStr = "[장착 무장]\n";
                foreach (var w in weapons)
                {
                    weaponStr += $"- {w.weaponID} <color=#FFFF00>(Lv.{w.currentLevel.Value})</color>\n";
                }
                weaponListText.text = weaponStr;
            }
        }
    }

    public void StartFadeOut(float duration)
    {
        if (fadeBlackImage != null)
        {
            StartCoroutine(FadeOutCoroutine(duration));
        }
    }

    private IEnumerator FadeOutCoroutine(float duration)
    {
        fadeBlackImage.gameObject.SetActive(true);
        Color c = fadeBlackImage.color;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(0f, 1f, timer / duration);
            fadeBlackImage.color = c;
            yield return null;
        }
    }

    private void UpdateHPBars()
    {
        if (myStats.isDown.Value)
        {
            myHpBar.maxValue = myStats.timeToRevive;
            myHpBar.value = myStats.reviveProgress.Value;
            
            float progressPercent = (myStats.reviveProgress.Value / myStats.timeToRevive) * 100f;
            myHpText.text = $"<color=yellow>시스템 재부팅 중... {progressPercent:0}%</color>";
        }
        else
        {
            myHpBar.maxValue = myStats.maxHealth.Value;
            myHpBar.value = myStats.currentHealth.Value;
            myHpText.text = $"{myStats.currentHealth.Value} / {myStats.maxHealth.Value}";
        }

        if (allyStats == null)
        {
            PlayerStats[] allPlayers = UnityEngine.Object.FindObjectsByType<PlayerStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            
            foreach (var stats in allPlayers)
            {
                if (stats != myStats)
                {
                    allyStats = stats;
                    break;
                }
            }
        }

        if (allyStats != null)
        {
            if (!allyHpUIContainer.activeSelf) allyHpUIContainer.SetActive(true);

            if (allyStats.isDown.Value)
            {
                allyHpBar.maxValue = allyStats.timeToRevive;
                allyHpBar.value = allyStats.reviveProgress.Value;
                
                if (allyStats.reviveProgress.Value > 0)
                {
                    float progressPercent = (allyStats.reviveProgress.Value / allyStats.timeToRevive) * 100f;
                    allyHpText.text = $"<color=yellow>재부팅 진행 중... {progressPercent:0}%</color>";
                }
                else
                {
                    allyHpText.text = "<color=red>DOWN! (구조 요망)</color>";
                }
            }
            else
            {
                allyHpBar.maxValue = allyStats.maxHealth.Value;
                allyHpBar.value = allyStats.currentHealth.Value;
                allyHpText.text = $"{allyStats.currentHealth.Value} / {allyStats.maxHealth.Value}";
            }
        }
        else
        {
            if (allyHpUIContainer.activeSelf) allyHpUIContainer.SetActive(false);
        }
    }

    public void SetPauseUI(bool isPaused)
    {
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }

        if (isPaused)
        {
            // 일시정지 시 커서 해제
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 재개 시, 보상 창이 열려있지 않을 때만 마우스를 다시 잠금 처리
            if (RewardUIManager.Instance != null && !RewardUIManager.Instance.rewardPanel.activeSelf)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }

    // 일시정지 UI의 '재개' 버튼과 연결
    public void OnClickResume()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestTogglePauseRpc();
        }
    }

    // 일시정지 UI의 '게임 중단' 버튼과 연결
    public void OnClickAbort()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RequestAbortGameRpc();
        }
    }
}