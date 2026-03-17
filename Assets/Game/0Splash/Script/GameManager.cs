using UnityEngine;
using System.IO;

public class GameManager : Singleton<GameManager>
{
    [Header("유저 데이터")]
    public UserData currentUser; // 현재 세션의 유저 데이터

    private string savePath;

    void Awake()
    {
        savePath = Path.Combine(Application.persistentDataPath, "userData.json");
        LoadUserData(); // 게임 시작 시 로드 [cite: 29]
    }

    // [데이터 저장] 앱 종료 시나 특정 시점에 호출
    public void SaveUserData()
    {
        string json = JsonUtility.ToJson(currentUser, true);
        File.WriteAllText(savePath, json);
        Debug.Log("데이터 저장 완료: " + savePath); 
    }

    // [데이터 로드]
    public void LoadUserData()
    {
        if (File.Exists(savePath))
        {
            string json = File.ReadAllText(savePath);
            currentUser = JsonUtility.FromJson<UserData>(json);
        }
        else
        {
            currentUser = new UserData(); // 파일이 없으면 새 데이터 생성
        }
    }

    private void OnApplicationQuit()
    {
        SaveUserData(); // 게임 종료 시 자동 저장 [cite: 29]
    }
}