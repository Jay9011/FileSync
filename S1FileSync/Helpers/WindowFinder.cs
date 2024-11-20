using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using S1FileSync.Models;

namespace S1FileSync.Helpers
{
    internal static class WindowFinder
    {
        private const string MainWindowClassName = "HwndWrapper[S1FileSync;;]";

        public static bool TryFindAndActivateWindow()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(currentProcess.ProcessName)
                .Where(p => p.Id != currentProcess.Id);

            foreach (var proc in processes)
            {
                try
                {
                    if (TryFindAndActivateWindowForProcess(proc))
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    continue;
                }
            }

            return false;
        }

        private static bool TryFindAndActivateWindowForProcess(Process process)
        {
            IntPtr foundWindow = IntPtr.Zero;
            
            NativeMethod.EnumWindows((hWnd, lParam) =>
            {
                NativeMethod.GetWindowThreadProcessId(hWnd, out uint processId);
                if (processId != process.Id)
                {
                    return true;
                }

                var classNameBuilder = new StringBuilder(256);
                NativeMethod.GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                if (!classNameBuilder.ToString().StartsWith("HwndWrapper[S1FileSync"))
                {
                    return true;
                }

                if (NativeMethod.IsWindowVisible(hWnd))
                {
                    return true;
                }

                var titleBuilder = new StringBuilder(256);
                NativeMethod.GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                if (titleBuilder.ToString() != "File Synchronization")
                {
                    return true;
                }

                foundWindow = hWnd;
                return false;
            }, IntPtr.Zero);

            if (foundWindow != IntPtr.Zero)
            {
                return true;
            }

            return false;
        }
    }
}
