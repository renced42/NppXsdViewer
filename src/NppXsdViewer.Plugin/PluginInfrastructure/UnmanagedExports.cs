using System;
using System.Runtime.InteropServices;
using NppXsdViewer.Plugin.PluginInfrastructure;
using RGiesecke.DllExport;

namespace NppXsdViewer.Plugin
{
    /// <summary>
    /// A Notepad++ által közvetlenül meghívott unmanaged exportok.
    /// A felépítés az aktuális NppCSharpPluginPack exportmintáját követi.
    /// </summary>
    internal static class UnmanagedExports
    {
        private static IntPtr pluginNamePointer = IntPtr.Zero;

        static UnmanagedExports()
        {
            AssemblyResolver.Install();
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static bool isUnicode()
        {
            return true;
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static void setInfo(NppData notepadPlusData)
        {
            PluginHost.SetInfo(notepadPlusData);
            PluginHost.CommandMenuInit();
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static IntPtr getFuncsArray(ref int count)
        {
            count = PluginHost.CommandCount;
            return PluginHost.CommandPointer;
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static uint messageProc(uint message, IntPtr wParam, IntPtr lParam)
        {
            return 1;
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static IntPtr getName()
        {
            if (pluginNamePointer == IntPtr.Zero)
                pluginNamePointer = Marshal.StringToHGlobalUni(PluginHost.PluginName);
            return pluginNamePointer;
        }

        [DllExport(CallingConvention = CallingConvention.Cdecl)]
        private static void beNotified(IntPtr notificationPointer)
        {
            NotificationHeader notification =
                (NotificationHeader)Marshal.PtrToStructure(notificationPointer, typeof(NotificationHeader));

            if (notification.Code == NppNotifications.Shutdown)
            {
                PluginHost.CleanUp();
                AssemblyResolver.Uninstall();
                if (pluginNamePointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pluginNamePointer);
                    pluginNamePointer = IntPtr.Zero;
                }
                return;
            }

            PluginHost.OnNotification(notification.Code);
        }
    }
}
