using UnityEngine;
using TMPro;
using Unity.Netcode;

public class RewardUIManager : MonoBehaviour
{
    public static RewardUIManager Instance { get; private set; }

    public GameObject rewardPanel;
    public TextMeshProUGUI[] rewardTitleTexts; 
    public TextMeshProUGUI[] rewardDescriptionTexts;

    // 화면에 띄워진 3개 버튼이 각각 'DB의 몇 번 보상'을 가리키는지 저장
    private int[] currentDisplayedIndices = new int[3];

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        rewardPanel.SetActive(false);
    }

    public void OpenRewardUI()
    {
        // 1. 마우스 커서 잠금 해제 및 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentDisplayedIndices = RewardDatabase.Instance.GetRandomRewardIndices(3);

        for (int i = 0; i < 3; i++)
        {
            RewardItem item = RewardDatabase.Instance.GetRewardByIndex(currentDisplayedIndices[i]);
            rewardTitleTexts[i].text = item.rewardName;
            rewardDescriptionTexts[i].text = item.description;
        }

        rewardPanel.SetActive(true);
    }

    // 인자 0, 1, 2
    public void OnRewardButtonClicked(int buttonIndex)
    {
        int chosenDBIndex = currentDisplayedIndices[buttonIndex];

        PlayerStats myPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();
        if (myPlayer != null)
        {
            myPlayer.SubmitRewardChoiceRpc(chosenDBIndex);
        }

        rewardPanel.SetActive(false);

        // 2. 마우스 커서 다시 잠금 및 숨김
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}