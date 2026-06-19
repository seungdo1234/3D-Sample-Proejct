using System.Collections.Generic;
using UnityEngine;

public static class DebugUtil
{
    public enum LogType
    {
        Log,
        Warning,
        Error
    }

    public enum LogCategory
    {
        General,
        Skill,
    }

    private static readonly HashSet<LogCategory> _enabledCategories = new()
    {
        LogCategory.General,
        LogCategory.Skill,
    };

    public static void SetCategoryEnabled(LogCategory category, bool enabled)
    {
        if (enabled) _enabledCategories.Add(category);
        else         _enabledCategories.Remove(category);
    }

    public static bool IsCategoryEnabled(LogCategory category)
        => _enabledCategories.Contains(category);

    public static bool IsSkillLoggingEnabled => IsCategoryEnabled(LogCategory.Skill);

    public static void SetSkillLoggingEnabled(bool enabled)
        => SetCategoryEnabled(LogCategory.Skill, enabled);

    public static void Log(string log) => Debug.Log(log);

    public static void DevelopmentLog(string log)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log(log);
#endif
    }

    public static void DevelopmentLog(string log, LogType logType)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        switch (logType)
        {
            case LogType.Log:     Debug.Log(log);      break;
            case LogType.Warning: Debug.LogWarning(log); break;
            case LogType.Error:   Debug.LogError(log);   break;
        }
#endif
    }

    public static void DevelopmentLog(string log, LogCategory category, LogType logType = LogType.Log)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        if (!_enabledCategories.Contains(category)) return;
        switch (logType)
        {
            case LogType.Log:     Debug.Log(log);        break;
            case LogType.Warning: Debug.LogWarning(log); break;
            case LogType.Error:   Debug.LogError(log);   break;
        }
#endif
    }

    public static void SkillLog(UnityEngine.Object context, string message)
        => DevelopmentLog(BuildSkillMessage(context != null ? context.name : null, message), LogCategory.Skill);

    public static void SkillLog(string subject, string message)
        => DevelopmentLog(BuildSkillMessage(subject, message), LogCategory.Skill);

    public static void SkillWarn(UnityEngine.Object context, string message)
        => DevelopmentLog(BuildSkillMessage(context != null ? context.name : null, message), LogCategory.Skill, LogType.Warning);

    public static void SkillWarn(string subject, string message)
        => DevelopmentLog(BuildSkillMessage(subject, message), LogCategory.Skill, LogType.Warning);

    public static void SkillError(UnityEngine.Object context, string message)
        => DevelopmentLog(BuildSkillMessage(context != null ? context.name : null, message), LogCategory.Skill, LogType.Error);

    public static void SkillError(string subject, string message)
        => DevelopmentLog(BuildSkillMessage(subject, message), LogCategory.Skill, LogType.Error);

    public static void DevelopmentLogWarning(string log)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.LogWarning(log);
#endif
    }

    public static void DevelopmentLogError(string log)
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.LogError(log);
#endif
    }

    private static string BuildSkillMessage(string subject, string message)
        => string.IsNullOrEmpty(subject) ? $"[Skill] {message}" : $"[Skill] {subject} | {message}";
}
