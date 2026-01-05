using System;
using System.Collections;
using System.Collections.Generic;

namespace HAM_DeBugger.Core.Debugging.DebugConsole
{
    public class LogRegistry : IEnumerable<LogEntry>, IEnumerator<LogEntry>
    {
        private readonly List<LogEntry> _logList = new();

        private int _position = -1;

        public int LogAmount => _logList?.Count ?? 0;

        public void AddLog(string message, bool isError, bool hideTimestamp = false)
        {
            Add(new LogEntry
            {
                text = message,
                isError = isError,
                time = hideTimestamp ? "" : DateTime.Now.ToString("HH:mm:ss")
            });
        }

        public void Add(LogEntry entry)
        {
            _logList.Add(entry);
        }

        public void ClearRegistry()
        {
            _logList.Clear();
            Reset();
        }

        public LogEntry Current
        {
            get
            {
                if (_position < 0 || _position >= _logList.Count)
                    throw new System.InvalidOperationException();
                return _logList[_position];
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            _position++;
            return _position < _logList.Count;
        }

        public void Reset()
        {
            _position = -1;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        // IEnumerable<T>
        public IEnumerator<LogEntry> GetEnumerator()
        {
            Reset();
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
