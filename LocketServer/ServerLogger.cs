using System;

namespace LocketServer
{
    public static class ServerLogger
    {
        public static void LogInfo(string message)
        {
            Print($"[INFO]  {message}", ConsoleColor.Green);
        }

        public static void LogWarning(string message)
        {
            Print($"[WARN]  {message}", ConsoleColor.Yellow);
        }

        public static void LogError(string message)
        {
            Print($"[ERROR] {message}", ConsoleColor.Red);
        }

        public static void LogChat(string from, string to, string content)
        {
            Print($"[CHAT]  {from} -> {to}: {content}", ConsoleColor.Cyan);
        }

        public static void LogRequest(string method, string path)
        {
            Print($"[HTTP]  {method} {path}", ConsoleColor.Magenta);
        }

        private static void Print(string msg, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");
            Console.ResetColor();
        }
    }
}