using System.Collections.Generic;
using UnityEngine;

namespace HAM_DeBugger.Core.UtilSystem
{

    public static class TimeManager
    {
        private static readonly float _defaultTimeScale = 1f;
        private static readonly Stack<float> _timeScaleRecord;

        static TimeManager()
        {
            _timeScaleRecord = new Stack<float>();
        }

        public static void ResetTimeScale()
        {
            Time.timeScale = _defaultTimeScale;
            _timeScaleRecord.Clear();
        }

        public static void AddTimeScaleRecord(float newTimeScale)
        {
            _timeScaleRecord.Push(newTimeScale);
            Time.timeScale = newTimeScale;
        }

        public static void RemoveTimeScaleRecord()
        {
            if (_timeScaleRecord.Count > 0)
                _timeScaleRecord.Pop();

            Time.timeScale = _timeScaleRecord.Count > 0 ? _timeScaleRecord.Peek() : _defaultTimeScale;
        }
    }
}