using UnityEngine;
namespace HAM_DeBugger.Core.Debugging
{
    public static class DebugTool
    {
        public enum DebugLogType
        {
            Normal,
            Warning,
            Error

        }

        public static void PrintLog(DebugLogType logType, string content)
        {
            switch (logType)
            {
                case DebugLogType.Normal:
                    Log(content);
                    break;
                case DebugLogType.Warning:
                    LogWarning(content);
                    break;
                case DebugLogType.Error:
                    LogError(content);
                    break;
            }
        }

        public static void Assert(bool assertCondition)
        {
            //if (!assertCondition)

        }

        public static void Assert(bool assertCondition, string content)
        {

            if (!assertCondition)
                LogError(content);
        }

        public static void Log(string content)
        {

            Debug.Log(content);
        }

        public static void LogWarning(string content)
        {
            Debug.LogWarning(content);
        }

        public static void LogError(string content)
        {
            Debug.LogError(content);
        }


    }
}