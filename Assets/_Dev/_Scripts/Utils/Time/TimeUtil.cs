using System;
using UnityEngine;

public static class TimeUtil
{
    // 현재 UTC 시간 반환 (초 단위)
    public static long UtcNowSec()
    {
        return DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
    }

    public static long GetDeviceUptimeSec()
    {
        return Environment.TickCount / 1000;
    }
    
    // 마감시간 체크 (endUtcSec이 현재보다 지났는지 확인)
    public static bool IsTimeOver(long nowUtcSec,long endUtcSec)
    {
        long remain = endUtcSec - nowUtcSec;
        return remain <= 0;
    }
    
    public static (bool,long) GetTimeOverWithRemainTime(long nowUtcSec,long endUtcSec)
    {
        long remain = endUtcSec - nowUtcSec;
        return (remain <= 0, remain);
    }

    // 주기성 체크 (lastUtcSec에서 interval초 이상 지났는지 확인)
    public static bool IsTimeElapsed(long nowUtcSec ,long lastUtcSec, long intervalSec)
    {
        long elapsed = nowUtcSec - lastUtcSec;
        return elapsed >= intervalSec;
    }

    public static string GetTimeText(long sec)
    {
        TimeSpan ts = TimeSpan.FromSeconds(sec);

        if (ts.Hours > 0) // 시간이 있으면
        {
            return $"{ts.Hours:D2}h {ts.Minutes:D2}m";
        }
        else if(ts.Minutes > 0)
        {
            return $"{ts.Minutes:D2}m {ts.Seconds:D2}s";
        }
        else
        {
            return $"{ts.Seconds:D2}s";
        }
    }

    public static void SynchronizedTimer(Func<long> getMainUtcSec, long interval, Action applyCallback)
    {
        // while (true)
        // {
        //     long mainUtcSec = getMainUtcSec();
        //     if(IsTimeElapsed (SaveManager.Instance.SaveData.GetTrustedCurrentTime() ,mainUtcSec, interval)) 
        //         applyCallback();
        //     else
        //         break;
        // }
    }
    
    
    // .NET 초 -> DateTime(Utc)
    public static DateTime DotNetSecToUtc(long dotNetSec)
        => new DateTime(dotNetSec * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    // .NET 초 -> DateTime(Local)
    public static DateTime DotNetSecToLocal(long dotNetSec)
        => DotNetSecToUtc(dotNetSec).ToLocalTime();

}