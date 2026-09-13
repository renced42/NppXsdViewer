using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace NppXsdViewer.Plugin.PluginInfrastructure
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct NppData
    {
        public IntPtr NppHandle;
        public IntPtr ScintillaMainHandle;
        public IntPtr ScintillaSecondHandle;
    }

    internal delegate void NppFuncItemDelegate();

    [StructLayout(LayoutKind.Sequential)]
    internal struct ShortcutKey
    {
        public ShortcutKey(bool isCtrl, bool isAlt, bool isShift, Keys key)
        {
            IsCtrl = Convert.ToByte(isCtrl);
            IsAlt = Convert.ToByte(isAlt);
            IsShift = Convert.ToByte(isShift);
            Key = Convert.ToByte(key);
        }

        public byte IsCtrl;
        public byte IsAlt;
        public byte IsShift;
        public byte Key;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct FuncItem
    {
        internal const int MaxNameLength = 63;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxNameLength + 1)]
        public string ItemName;

        public NppFuncItemDelegate Function;
        public int CommandId;
        public bool InitToCheck;
        public ShortcutKey Shortcut;
    }

    /// <summary>
    /// A Notepad++ FuncItem tömb natív memóriaképét kezeli.
    /// A memóriaelrendezés az NppCSharpPluginPack aktuális megoldását követi.
    /// </summary>
    internal sealed class FuncItems : IDisposable
    {
        private readonly List<FuncItem> items = new List<FuncItem>();
        private readonly List<IntPtr> shortcutPointers = new List<IntPtr>();
        private readonly int itemSize = Marshal.SizeOf(typeof(FuncItem));
        private IntPtr nativePointer = IntPtr.Zero;
        private bool disposed;

        public int Count => items.Count;
        public IntPtr NativePointer => nativePointer;

        public void Add(string name, NppFuncItemDelegate function)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (function == null) throw new ArgumentNullException(nameof(function));
            if (name.Length > FuncItem.MaxNameLength)
                throw new ArgumentException("The Notepad++ menu item name can be at most 63 characters.", nameof(name));

            FuncItem item = new FuncItem
            {
                ItemName = name,
                Function = new NppFuncItemDelegate(function),
                CommandId = items.Count,
                InitToCheck = false,
                Shortcut = new ShortcutKey(false, false, false, Keys.None)
            };

            int oldSize = items.Count * itemSize;
            items.Add(item);
            int newSize = items.Count * itemSize;
            IntPtr newPointer = Marshal.AllocHGlobal(newSize);

            if (nativePointer != IntPtr.Zero)
            {
                byte[] oldBytes = new byte[oldSize];
                Marshal.Copy(nativePointer, oldBytes, 0, oldSize);
                Marshal.Copy(oldBytes, 0, newPointer, oldSize);
                Marshal.FreeHGlobal(nativePointer);
            }

            WriteNativeItem(IntPtr.Add(newPointer, oldSize), item);
            nativePointer = newPointer;
        }

        private void WriteNativeItem(IntPtr destination, FuncItem item)
        {
            byte[] nameBytes = Encoding.Unicode.GetBytes(item.ItemName + "\0");
            Marshal.Copy(nameBytes, 0, destination, nameBytes.Length);

            IntPtr cursor = IntPtr.Add(destination, 128);
            IntPtr functionPointer = item.Function == null
                ? IntPtr.Zero
                : Marshal.GetFunctionPointerForDelegate(item.Function);
            Marshal.WriteIntPtr(cursor, functionPointer);

            cursor = IntPtr.Add(cursor, IntPtr.Size);
            Marshal.WriteInt32(cursor, item.CommandId);

            cursor = IntPtr.Add(cursor, 4);
            Marshal.WriteInt32(cursor, item.InitToCheck ? 1 : 0);

            cursor = IntPtr.Add(cursor, 4);
            if (item.Shortcut.Key == 0)
            {
                Marshal.WriteIntPtr(cursor, IntPtr.Zero);
            }
            else
            {
                IntPtr shortcutPointer = Marshal.AllocHGlobal(4);
                Marshal.StructureToPtr(item.Shortcut, shortcutPointer, false);
                shortcutPointers.Add(shortcutPointer);
                Marshal.WriteIntPtr(cursor, shortcutPointer);
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            foreach (IntPtr pointer in shortcutPointers)
                Marshal.FreeHGlobal(pointer);
            shortcutPointers.Clear();

            if (nativePointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nativePointer);
                nativePointer = IntPtr.Zero;
            }

            disposed = true;
            GC.SuppressFinalize(this);
        }

        ~FuncItems()
        {
            Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NotificationHeader
    {
        public IntPtr HwndFrom;
        public IntPtr IdFrom;
        public uint Code;
    }

    [Flags]
    internal enum NppTbMsg : uint
    {
        ContLeft = 0,
        ContRight = 1,
        ContTop = 2,
        ContBottom = 3,
        DockContMax = 4,
        DwsIconTab = 0x00000001,
        DwsIconBar = 0x00000002,
        DwsAddInfo = 0x00000004,
        DwsParamsAll = DwsIconTab | DwsIconBar | DwsAddInfo,
        DwsDefaultLeft = ContLeft << 28,
        DwsDefaultRight = ContRight << 28,
        DwsDefaultTop = ContTop << 28,
        DwsDefaultBottom = ContBottom << 28,
        DwsDefaultFloating = 0x80000000
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NppTbData
    {
        public IntPtr ClientHandle;
        public string Name;
        public int DialogId;
        public NppTbMsg Mask;
        public uint IconTab;
        public string AdditionalInfo;
        public Rect FloatRect;
        public int PreviousContainer;
        public string ModuleName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    internal static class NppMessages
    {
        private const uint WmUser = 0x0400;
        private const uint NppMsg = WmUser + 1000;
        private const uint RunCommandUser = WmUser + 3000;
        private const uint FullCurrentPath = 1;

        public const uint GetCurrentScintilla = NppMsg + 4;
        public const uint DmmShow = NppMsg + 30;
        public const uint DmmHide = NppMsg + 31;
        public const uint DmmRegisterAsDockDialog = NppMsg + 33;
        public const uint DoOpen = NppMsg + 77;
        public const uint GetFullCurrentPath = RunCommandUser + FullCurrentPath;
    }

    internal static class NppNotifications
    {
        private const uint First = 1000;
        public const uint FileSaved = First + 8;
        public const uint Shutdown = First + 9;
        public const uint BufferActivated = First + 10;
    }

    internal static class ScintillaMessages
    {
        public const uint GetLength = 2006;
        public const uint GoToLine = 2024;
        public const uint GoToPos = 2025;
        public const uint LineScroll = 2168;
        public const uint ScrollCaret = 2169;
        public const uint PositionFromLine = 2167;
        public const uint LineFromPosition = 2166;
        public const uint GetFirstVisibleLine = 2152;
        public const uint GetText = 2182;
        public const uint EnsureVisible = 2232;
        public const uint LinesOnScreen = 2370;
        public const uint IndicSetStyle = 2080;
        public const uint IndicSetFore = 2082;
        public const uint IndicSetAlpha = 2523;
        public const uint SetIndicatorCurrent = 2500;
        public const uint IndicatorFillRange = 2504;
        public const uint IndicatorClearRange = 2505;
    }

    internal static class ScintillaIndicatorStyles
    {
        public const int FullBox = 16;
    }

    internal static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);
    }
}
