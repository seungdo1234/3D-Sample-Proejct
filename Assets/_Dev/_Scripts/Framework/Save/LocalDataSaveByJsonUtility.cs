using System;
using UnityEngine;
using System.IO;

public class LocalDataSaveByJsonUtility
{
    private AESCrypto crypto = new();
    private string path;
    
    
    public void DataSave <T>(T playerData, string dataName)
    {
        path = Path.Combine(Application.persistentDataPath, $"{dataName}.json");
        string json = JsonUtility.ToJson(playerData, true); // 데이터 직렬화
        string encryptedJson = crypto.EncryptString(json); // 직렬화 된 데이터 암호화
        File.WriteAllText(path, encryptedJson);
    }

    public T DataLoad<T>( string dataName)
    {
        path = Path.Combine(Application.persistentDataPath, $"{dataName}.json");
        
        if (!File.Exists(path))
            return default(T);

        try
        {
            string encryptedJson = File.ReadAllText(path);
            string json = crypto.DecryptString(encryptedJson);
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            try
            {
                File.Delete(path);
                Debug.Log($"에러 데이터 파일 삭제 완료: {path}");
            }
            catch (Exception deleteEx)
            {
                Debug.Log($"파일 삭제 실패: {deleteEx.Message}");
            }
            return default(T);
        }
    }
    
    public void DataDelete(string dataName)
    {
        path = Path.Combine(Application.persistentDataPath, $"{dataName}.json");

        if (!File.Exists(path))
        {
             return;
        }
        
        File.Delete(path);
    }
//     #if UNITY_EDITOR
//     
//     [MenuItem("Tools/SaveData/Delete Save Data/Player Data")]
//     private static void PlayerDataDeleteInEditor()
//     {
//         DeleteData("SaveData");
//     }
//
//     private static void DeleteData(string dataName)
//     {
//         string path = Path.Combine(Application.persistentDataPath, $"{dataName}.json");
//         
//         if (!File.Exists(path))
//         {
//             Debug.LogError($"{path} 경로에 데이터 파일이 존재하지 않습니다 !");
//             return;
//         }
//         
//         File.Delete(path);
//         Debug.Log($"데이터 파일 삭제 완료: {path}");
//     }
//     
//     
// #endif

}
