using System;
using System.IO;
using UnityEngine;

public static class SaveWav
{
    const int HEADER_SIZE = 44;

    public static bool Save(string path, AudioClip clip)
    {
        if (!path.ToLower().EndsWith(".wav"))
        {
            path = path.Substring(0, path.LastIndexOf(".")) + ".wav";
        }

        // 특정 플랫폼에서 경로 문제 해결
        path = path.Replace('/', Path.DirectorySeparatorChar);
        
        // 임시 WAV 파일 생성
        var filepath = Path.Combine(Application.temporaryCachePath, "temp.wav");
        
        // 모든 샘플 데이터 확보
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        // 샘플을 Int16 데이터로 변환
        Int16[] intData = new Int16[samples.Length];
        
        // Convert to Int16
        Byte[] bytesData = new Byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * 32767);
            Byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        // WAV 파일 생성
        using (var fileStream = new FileStream(filepath, FileMode.Create))
        {
            using (var writer = new BinaryWriter(fileStream))
            {
                writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(36 + bytesData.Length);
                writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1); // PCM 형식 (1)
                writer.Write((short)clip.channels);
                writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2); // 바이트 레이트
                writer.Write((short)(clip.channels * 2)); // 블록 정렬
                writer.Write((short)16); // 비트 퍼 샘플
                writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
                writer.Write(bytesData.Length);
                writer.Write(bytesData);
            }
        }
        
        // 임시 파일을 최종 경로로 복사
        try 
        {
            File.Copy(filepath, path, true);
            File.Delete(filepath); // 임시 파일 삭제
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("Error saving WAV file: " + e.Message);
            return false;
        }
    }
}