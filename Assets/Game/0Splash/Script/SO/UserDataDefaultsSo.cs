using UnityEngine;

/// <summary>
/// 신규 유저(세이브 없음) 시 <see cref="GameManager.LoadUserData"/>에서 복사해 쓰는 기본 <see cref="UserData"/>.
/// 실제 플레이 진행은 계속 <c>userData.json</c>에 저장됩니다.
/// </summary>
[CreateAssetMenu(fileName = "UserDataDefaultsSo", menuName = "ScriptableObject/UserDataDefaultsSo")]
public class UserDataDefaultsSo : ScriptableObject
{
    public UserData defaults = new UserData();

    public UserData CreateRuntimeCopy()
    {
        return JsonUtility.FromJson<UserData>(JsonUtility.ToJson(defaults));
    }
}
