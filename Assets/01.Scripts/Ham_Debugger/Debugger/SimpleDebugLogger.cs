using UnityEngine;
namespace HAM_DeBugger.Core.Debugging
{

    public class SimpleDebugLogger : MonoBehaviour
    {
        [SerializeField] private DebugTool.DebugLogType _logType;


        [SerializeField] private string _logContent;
        private string _result;
        
        public void PrintLog()
        {
            DebugTool.PrintLog(_logType, $"[Type:{_logType}] {_logContent}");
        }
        
    }
}