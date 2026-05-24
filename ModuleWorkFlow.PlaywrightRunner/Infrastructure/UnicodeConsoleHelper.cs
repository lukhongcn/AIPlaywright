using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ModuleWorkFlow.PlaywrightRunner.Infrastructure
{
    internal static class UnicodeConsoleHelper
    {
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private const int StdInputHandle = -10;

        public static string ReadLine()
        {
            if (Console.IsInputRedirected)
            {
                return Console.ReadLine();
            }

            var handle = GetStdHandle(StdInputHandle);
            if (handle == IntPtr.Zero || handle == InvalidHandleValue)
            {
                return Console.ReadLine();
            }

            uint mode;
            if (!GetConsoleMode(handle, out mode))
            {
                return Console.ReadLine();
            }

            var buffer = new StringBuilder(4096);
            uint charsRead;
            if (!ReadConsoleW(handle, buffer, (uint)buffer.Capacity, out charsRead, IntPtr.Zero) || charsRead == 0)
            {
                return string.Empty;
            }

            return buffer
                .ToString(0, (int)charsRead)
                .TrimEnd('\r', '\n');
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ReadConsoleW(
            IntPtr hConsoleInput,
            [Out] StringBuilder lpBuffer,
            uint nNumberOfCharsToRead,
            out uint lpNumberOfCharsRead,
            IntPtr pInputControl);
    }
}
