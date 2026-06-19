// using Newtonsoft.Json;
// using System;
// using System.IO;
// using UnityEngine;
//
// public class LocalDataSaveByNewtonsoft
// {
//     private readonly AESCrypto crypto = new AESCrypto();
//     private static string PathOf(string name) => Path.Combine(Application.persistentDataPath, $"{name}.json");
//     private static string BakOf(string name)  => Path.Combine(Application.persistentDataPath, $"{name}.json.bak");
//     private static string TmpOf(string name)  => Path.Combine(Application.persistentDataPath, $"{name}.json.tmp");
//
//     // 관대한 설정
//     private static readonly JsonSerializerSettings Settings;
//     static LocalDataSaveByNewtonsoft()
//     {
//         Settings = new JsonSerializerSettings
//         {
//             MissingMemberHandling = MissingMemberHandling.Ignore,
//             NullValueHandling     = NullValueHandling.Ignore,
//             DefaultValueHandling  = DefaultValueHandling.Populate,
//             Formatting            = Formatting.None
//         };
//
//         // 역직렬화 중 발생하는 개별 에러는 삼키고 진행(문제 필드만 default/null)
//         Settings.Error += (sender, args) =>
//         {
//             args.ErrorContext.Handled = true;
//         };
//     }
//
//     public void DataSave<T>(T playerData, string dataName)
//     {
//         string path = PathOf(dataName);
//         string bak  = BakOf(dataName);
//         string tmp  = TmpOf(dataName);
//
//         try
//         {
//             string json = JsonConvert.SerializeObject(playerData, Settings);
//             string enc  = crypto.EncryptString(json);
//
//             // 1) tmp에 먼저 기록
//             File.WriteAllText(tmp, enc);
//
//             // 2) 기존 파일이 있으면 .bak로 백업(덮어쓰기 허용)
//             if (File.Exists(path))
//                 File.Copy(path, bak, overwrite: true);
//
//             // 3) tmp를 본 파일로 원자적 교체
//             //    (일부 플랫폼은 Move가 원자적이지 않을 수 있으므로 Copy+Replace도 고려)
//             if (File.Exists(path))
//                 File.Delete(path);
//             File.Move(tmp, path);
//
// #if UNITY_EDITOR
//             Debug.Log($"[Save] OK: {path}");
// #endif
//         }
//         catch (Exception e)
//         {
// #if UNITY_EDITOR
//             Debug.LogError($"[Save] Failed: {e}");
// #endif
//             // tmp는 실패 시 남아있을 수 있으니 정리
//             if (File.Exists(tmp)) File.Delete(tmp);
//             throw;
//         }
//     }
//
//     public T DataLoad<T>(string dataName)
//     {
//         string path = PathOf(dataName);
//         string bak  = BakOf(dataName);
//
//         if (!File.Exists(path))
//         {
//             // 본파일 없으면 .bak 시도
//             if (File.Exists(bak))
//             {
//                 try
//                 {
//                     string encBak = File.ReadAllText(bak);
//                     string jsonBak = crypto.DecryptString(encBak);
//                     return JsonConvert.DeserializeObject<T>(jsonBak, Settings);
//                 }
//                 catch (Exception e)
//                 {
//                     Debug.LogError($"[Load] .bak 복구 실패: {e}");
//                 }
//             }
//             return default;
//         }
//
//         try
//         {
//             string enc = File.ReadAllText(path);
//             string json = crypto.DecryptString(enc);
//             return JsonConvert.DeserializeObject<T>(json, Settings);
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"[Load] 본파일 로드 실패: {e}");
//
//             // ❗ 삭제하지 말고, 보존
//             try
//             {
//                 string corrupt = Path.Combine(
//                     Application.persistentDataPath,
//                     $"{dataName}.json.corrupt_{DateTime.UtcNow:yyyyMMdd_HHmmss}"
//                 );
//                 File.Copy(path, corrupt, overwrite:false);
//                 Debug.Log($"손상본 보존: {corrupt}");
//             }
//             catch (Exception ex)
//             {
//                 Debug.LogError($"손상본 보존 실패: {ex.Message}");
//             }
//
//             // .bak 복구 시도
//             if (File.Exists(bak))
//             {
//                 try
//                 {
//                     string encBak = File.ReadAllText(bak);
//                     string jsonBak = crypto.DecryptString(encBak);
//                     return JsonConvert.DeserializeObject<T>(jsonBak, Settings);
//                 }
//                 catch (Exception ex)
//                 {
//                     Debug.LogError($"[Load] .bak 복구 실패: {ex}");
//                 }
//             }
//
//             // 끝까지 실패하면 default 반환 (파일은 보존됨)
//             return default;
//         }
//     }
//
// #if UNITY_EDITOR
//     // [UnityEditor.MenuItem("Tools/SaveData/Delete Save Data/Player Data")]
//     public static void DeleteData()
//     {
//         DataDelete("SaveData");
//     }
// #endif
//    
//     public static void DataDelete(string dataName)
//     {
//         string path = PathOf(dataName);
//         if (File.Exists(path)) File.Delete(path);
//         string bak = BakOf(dataName);
//         if (File.Exists(bak)) File.Delete(bak);
//         string tem = TmpOf(dataName);
//         if (File.Exists(tem)) File.Delete(tem);
//         
//         Debug.Log($"[Delete] OK\n{path}\n{bak}\n{tem}");
//     }
// }
