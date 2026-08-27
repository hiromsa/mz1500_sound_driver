using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Mz1500SoundPlayer.Sound.Emulator
{
    public class EmulatorLogger
    {
        public static EmulatorLogger Instance { get; } = new EmulatorLogger();

        private readonly ConcurrentQueue<string> _logs = new ConcurrentQueue<string>();
        private const int MaxLogs = 1000;

        public event Action<string>? OnLogAdded;

        public void Log(string category, string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss.fff}] [{category}] {message}";
            _logs.Enqueue(entry);

            while (_logs.Count > MaxLogs)
            {
                _logs.TryDequeue(out _);
            }

            OnLogAdded?.Invoke(entry);
        }

        public IEnumerable<string> GetRecentLogs()
        {
            return _logs.ToArray();
        }

        public void Clear()
        {
            while (_logs.TryDequeue(out _)) { }
        }
    }
}
