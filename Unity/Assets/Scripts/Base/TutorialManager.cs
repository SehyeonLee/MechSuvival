using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TutorialManager : NetworkBehaviour
{
    [Header("Operator UI")]
    public GameObject operatorUIContainer;
    public TextMeshProUGUI operatorText;

    [Header("Dummy System")]
    public GameObject dummyPlayerPrefab; 
    public Transform dummySpawnPoint;

    private PlayerController localPlayerController;
    private PlayerStats localPlayerStats;
    private GameObject spawnedDummy;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            StartCoroutine(TutorialSequence());
        }
    }

    private IEnumerator TutorialSequence()
    {
        yield return new WaitForSeconds(1f);
        FindLocalPlayer();

        operatorUIContainer.SetActive(true);

        // 2-1. 전진(W) 훈련
        operatorText.text = "[오퍼레이터]\n시스템 부팅 완료. 전장 진입 전 기체 제어 점검을 시작합니다.\n먼저 W 키를 사용하여 '앞'으로 이동하십시오.";
        Vector3 startPos = localPlayerController.transform.position;
        Vector3 testDir = localPlayerController.transform.forward;
        yield return new WaitUntil(() => Vector3.Dot(localPlayerController.transform.position - startPos, testDir) > 2f);
        
        // 2-2. 후진(S) 훈련
        operatorText.text = "[오퍼레이터]\n다음은 S 키를 사용하여 '뒤'로 이동하십시오.";
        startPos = localPlayerController.transform.position;
        testDir = -localPlayerController.transform.forward;
        yield return new WaitUntil(() => Vector3.Dot(localPlayerController.transform.position - startPos, testDir) > 2f);

        // 2-3. 좌측(A) 훈련
        operatorText.text = "[오퍼레이터]\n다음은 A 키를 사용하여 '좌측'으로 이동하십시오.";
        startPos = localPlayerController.transform.position;
        testDir = -localPlayerController.transform.right;
        yield return new WaitUntil(() => Vector3.Dot(localPlayerController.transform.position - startPos, testDir) > 2f);

        // 2-4. 우측(D) 훈련
        operatorText.text = "[오퍼레이터]\n마지막으로 D 키를 사용하여 '우측'으로 이동하십시오.";
        startPos = localPlayerController.transform.position;
        testDir = localPlayerController.transform.right;
        yield return new WaitUntil(() => Vector3.Dot(localPlayerController.transform.position - startPos, testDir) > 2f);

        operatorText.text = "[오퍼레이터]\n이동 모터 정상 확인.";
        yield return new WaitForSeconds(1.5f);

        // 3-1. 좌회전(Q) 훈련
        operatorText.text = "[오퍼레이터]\n다음은 방향 전환 점검입니다.\nQ 키를 눌러 좌측으로 90도 선회하십시오.";
        float startRot = localPlayerController.transform.eulerAngles.y;
        yield return new WaitUntil(() => Mathf.DeltaAngle(startRot, localPlayerController.transform.eulerAngles.y) <= -80f);
        yield return new WaitForSeconds(1f);

        // 3-2. 우회전(E) 훈련
        operatorText.text = "[오퍼레이터]\nE 키를 눌러 우측으로 90도 선회하십시오.";
        startRot = localPlayerController.transform.eulerAngles.y;
        yield return new WaitUntil(() => Mathf.DeltaAngle(startRot, localPlayerController.transform.eulerAngles.y) >= 80f);
        yield return new WaitForSeconds(1f);

        operatorText.text = "[오퍼레이터]\n방향 제어 시스템 정상 확인.";
        yield return new WaitForSeconds(1.5f);

        // 3-3. 부스터 점프 훈련
        operatorText.text = "[오퍼레이터]\n부스터 도약 점검을 진행합니다.\nSpace 키를 눌러 수직으로 도약하십시오.";
        
        yield return new WaitUntil(() => localPlayerController.GetBoostCooldownTimer() > 0f);
        yield return new WaitForSeconds(1.5f);
        operatorText.text = "[오퍼레이터]\n도약을 통해 적의 포위망을 탈출할 수 있습니다.";
        
        yield return new WaitForSeconds(3.5f);

        operatorText.text = "[오퍼레이터]\n부스터 사용 후에는\n추진기 냉각을 위한\n재충전 시간이 요구됩니다.\n화면 우측 상단의\n쿨타임 표시기를 확인하십시오.";
        yield return new WaitForSeconds(5f);

        // 레이더 설명
        operatorText.text = "[오퍼레이터]\n다음은 전술 레이더 시스템 점검입니다.\n곧 나타날 화면 중앙에\n위치한 레이더 인터페이스를\n확인하십시오.";
        yield return new WaitForSeconds(2.5f);

        var radar = PlayerHUD.Instance.radarSystem;
        radar.enabled = false;
        
        UnityEngine.UI.Image[] radarImages = new UnityEngine.UI.Image[] 
        {
            radar.frontRadar,
            radar.backRadar,
            radar.leftRadar,
            radar.rightRadar
        };

        SetRadarColor(radarImages, Color.red);
        operatorText.text = "[오퍼레이터]\n레이더의 4면은 각각 기체를 기준으로 [전, 후, 좌, 우] 방향을 나타냅니다.";
        yield return new WaitForSeconds(4f);

        SetRadarColor(radarImages, new Color(1f, 0f, 0f, 0f));
        operatorText.text = "[오퍼레이터]\n레이더가 투명하게 유지된다면, 해당 방향에 접근하는 위협 요소가 없음을 의미합니다.";
        yield return new WaitForSeconds(4f);

        SetRadarColor(radarImages, Color.red);
        operatorText.text = "[오퍼레이터]\n하지만 이처럼 특정 방향이 붉게 켜진다면, 해당 위치에서 적 개체가 접근 중이라는 경고입니다.";
        yield return new WaitForSeconds(5f);

        SetRadarColor(radarImages, new Color(1f, 0f, 0f, 0f));
        operatorText.text = "[오퍼레이터]\n전술 레이더 시스템 정상 확인 및 안내 완료.";
        radar.enabled = true;
        yield return new WaitForSeconds(2f);

        // 보급 포드 투하 훈련
        GameManager.Instance.availablePodPodTokens.Value = 1; 
        operatorText.text = "[오퍼레이터]\n임시 보급 권한을 인가했습니다.\n포드 호출 키 R을 눌러 보급을 요청하십시오.";
        
        yield return new WaitUntil(() => GameManager.Instance.availablePodPodTokens.Value == 0);
        yield return new WaitForSeconds(1.5f);
        
        operatorText.text = "[오퍼레이터]\n포드 투하가 확인되었습니다.\n해당 포드는 지면에 착탄 시 반경 내 적에게 강력한 물리 피해를 입힙니다.\n포드에 접근하여 업그레이드를 선택하십시오.";
        
        yield return new WaitUntil(() => Time.timeScale == 0f);
        yield return new WaitUntil(() => Time.timeScale > 0f);
        yield return new WaitForSeconds(1.5f);

        operatorText.text = "[오퍼레이터]\n보급 수령 확인. 멀티플레이 시 포드 호출 규정을 안내합니다.\n레벨 업 시 전원에게 호출권이 개별 지급되지만, 한 레벨당 보상 포드는 1번씩만 제공됩니다.\n이후 다른 인원이 호출하는 포드는 내구도 회복용으로 대체됩니다.";
        yield return new WaitForSeconds(8f);

        // 동료 부활 훈련
        operatorText.text = "[오퍼레이터]\n다음은 동료 부활 훈련입니다.\n잠시 후, 전장에 다운된 아군 기체가 나타날 것입니다.\n해당 기체에 접근하여 부활 프로세스를 완료하십시오.";
        SpawnDummy();
        PlayerStats dummyStats = spawnedDummy.GetComponent<PlayerStats>();
        yield return new WaitUntil(() => dummyStats.isDown.Value == false);

        operatorText.text = "[오퍼레이터]\n아군 기체 재부팅 확인.";
        yield return new WaitForSeconds(1.5f);

        // 종료
        operatorText.text = "[오퍼레이터]\n모든 점검이 완료되었습니다.\n수고하셨습니다.\n실전 투입을 준비하십시오.";
        yield return new WaitForSeconds(4f);
        
        operatorUIContainer.SetActive(false);
        EndTutorial();
    }

    private void SetRadarColor(UnityEngine.UI.Image[] images, Color targetColor)
    {
        foreach (var img in images)
        {
            if (img != null)
            {
                img.color = targetColor;
            }
        }
    }

    private void FindLocalPlayer()
    {
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
            {
                localPlayerController = client.PlayerObject.GetComponent<PlayerController>();
                localPlayerStats = client.PlayerObject.GetComponent<PlayerStats>();
                break;
            }
        }
    }

    private void SpawnDummy()
    {
        spawnedDummy = Instantiate(dummyPlayerPrefab, dummySpawnPoint.position, Quaternion.identity);
        spawnedDummy.GetComponent<NetworkObject>().Spawn();
        
        PlayerStats stats = spawnedDummy.GetComponent<PlayerStats>();
        stats.currentHealth.Value = 0;
        stats.isDown.Value = true;
    }

    private void EndTutorial()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene("MainMenuScene");
    }
}