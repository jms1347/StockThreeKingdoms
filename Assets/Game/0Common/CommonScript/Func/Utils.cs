using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System; // Math.Abs, Math.Floor, Math.Log10, Math.Pow 사용을 위해 필요합니다.

public static class Utils
{
    public static readonly WaitForFixedUpdate m_WaitForFixedUpdate = new WaitForFixedUpdate();
    public static readonly WaitForEndOfFrame m_WaitForEndOfFrame = new WaitForEndOfFrame();
    private static readonly Dictionary<float, WaitForSeconds> m_WaitForSecondsdict = new Dictionary<float, WaitForSeconds>();

    public static WaitForSeconds WaitForSecond(float pWaitTime)
    {
        WaitForSeconds wfs;

        if (m_WaitForSecondsdict.TryGetValue(pWaitTime, out wfs))
        {
            return wfs;
        }
        else
        {
            wfs = new WaitForSeconds(pWaitTime);
            m_WaitForSecondsdict.Add(pWaitTime, wfs);
            return wfs;
        }
    }

    #region 어드레서블 로드하기
    public static void LoadAssetAndHandle<T>(string address, System.Action<T> onLoaded)
    {
        Addressables.LoadAssetAsync<T>(address).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                T loadedAsset = handle.Result;
                onLoaded(loadedAsset);
            }
            else
            {
                Debug.LogError("자산 로드 실패: " + handle.OperationException);
            }
        };
    }

    public static T OnLoadComplete<T>(T param)
    {
        return param;
    }

    #endregion

    #region 어드레서블 라벨로 로드하기
    public static void LoadAssetsByLabelAndCache<T>(string label, System.Action<List<T>> onLoaded)
    {
        Addressables.LoadAssetsAsync<T>(label, null).Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                List<T> loadedAssets = new List<T>(handle.Result);
                onLoaded(loadedAssets);
            }
            else
            {
                Debug.LogError("자산 로드 실패: " + handle.OperationException);
            }
        };
    }
    #endregion

    #region 게임오브젝트 프리팹 생성 (나중에 필요할지 몰라서..)
    public static GameObject Istantiate(GameObject pPrefab, Transform parent = null)
    {
        GameObject tempPrefab = GameObject.Instantiate(pPrefab, parent);

        if (tempPrefab == null)
        {
            Debug.Log("생성할 프리팹이 존재하지 않습니다.");
        }
        return tempPrefab;
    }

    #endregion

    #region 돈의 단위
    // 단위 문자열 목록: 10^3마다 증가 (K, M, B, T, ...)
    private static readonly string[] Suffixes = new string[]
    {
         "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
    };

    /**
     * @brief 큰 숫자에 3자리마다 쉼표를 붙여 문자열로 반환하는 함수입니다.
     * @param score 포맷팅할 점수 (long 타입)
     * @return 쉼표가 붙은 문자열 (예: 1234567890 -> "1,234,567,890")
     */
    public static string FormatScore(long score)
    {
        // C#의 ToString("N0") 포맷 지정자를 사용하여 천 단위 쉼표를 쉽게 추가할 수 있습니다.
        return score.ToString("N0");
    }

    /**
     * @brief 점수를 K(천), M(백만), B(십억) 등의 약어 단위로 압축하여 표시하는 함수입니다.
     * @param score 압축할 점수 (long 타입)
     * @return 압축된 단위 문자열 (예: 1234567 -> "1.23M", 999 -> "999")
     */
    public static string AbbreviateScore(long score)
    {
        // 0점은 그대로 반환
        if (score == 0)
        {
            return "0";
        }

        // 절댓값으로 계산하여 음수도 처리할 수 있도록 합니다.
        long absScore = Math.Abs(score);

        // 점수가 1000 미만이면 쉼표만 붙여 반환 (압축하지 않음)
        if (absScore < 1000)
        {
            return score.ToString("N0");
        }

        // 단위(K, M, B 등)의 인덱스를 계산합니다.
        // Math.Log10(absScore) / 3 : 점수가 10^몇 승인지 확인 후, 3으로 나눠서 단위를 계산합니다.
        int suffixIndex = (int)(Math.Floor(Math.Log10(absScore) / 3));

        // 배열 범위를 초과하는지 확인 (최대 "Dc" 단위)
        if (suffixIndex >= Suffixes.Length)
        {
            suffixIndex = Suffixes.Length - 1; // 가장 큰 단위로 고정
        }

        // 단위를 나눌 값 (1000, 1000000, 1000000000 등)
        long powerOfTen = (long)Math.Pow(1000, suffixIndex);

        // 실제 표시할 숫자를 계산합니다. (나누기)
        double displayScore = (double)score / powerOfTen;

        // 최종 문자열 포맷팅 (소수점 2자리까지만 표시)
        // 예: 1.2345 * M -> "1.23" + "M"
        return displayScore.ToString("F2") + Suffixes[suffixIndex];
    }
    #endregion
}