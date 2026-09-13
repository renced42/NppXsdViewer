using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using NppXsdViewer.Diagram;
using NppXsdViewer.Plugin.PluginInfrastructure;
using NppXsdViewer.Plugin.UI;

namespace NppXsdViewer.Plugin
{
    /// <summary>
    /// A plugin alkalmazási logikája. Nem tartalmaz unmanaged export attribútumokat.
    /// </summary>
    internal static class PluginHost
    {
        internal const string PluginName = "NppXsdViewer";
        private const int ShowViewerCommandId = 0;
        private const int SourceHighlightIndicator = 31;

        private static readonly FuncItems commands = new FuncItems();
        private static NppData nppData;
        private static bool commandsInitialized;
        private static XsdViewerForm viewer;
        private static IntPtr dockingDataPointer = IntPtr.Zero;

        internal static int CommandCount => commands.Count;

        internal static IntPtr CommandPointer => commands.NativePointer;

        internal static void SetInfo(NppData data)
        {
            nppData = data;
        }

        internal static void CommandMenuInit()
        {
            if (commandsInitialized) return;
            commands.Add("Show XSD diagram", SafeShowViewer);
            commands.Add("Refresh XSD diagram", SafeRefreshViewer);
            commandsInitialized = true;
        }

        internal static void OnNotification(uint notificationCode)
        {
            if (viewer == null || viewer.IsDisposed) return;

            if (notificationCode == NppNotifications.FileSaved ||
                notificationCode == NppNotifications.BufferActivated)
            {
                RefreshViewer(false);
            }
        }

        internal static void CleanUp()
        {
            if (viewer != null && !viewer.IsDisposed)
                viewer.Dispose();
            viewer = null;

            if (dockingDataPointer != IntPtr.Zero)
            {
                Marshal.DestroyStructure(dockingDataPointer, typeof(NppTbData));
                Marshal.FreeHGlobal(dockingDataPointer);
                dockingDataPointer = IntPtr.Zero;
            }

            commands.Dispose();
        }


        private static void SafeShowViewer()
        {
            ExecuteCommandSafely("Show XSD diagram", ShowViewer);
        }

        private static void SafeRefreshViewer()
        {
            ExecuteCommandSafely("Refresh XSD diagram", RefreshViewer);
        }

        private static void ExecuteCommandSafely(string commandName, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                WriteCrashLog(commandName, ex);
                MessageBox.Show(
                    commandName + " failed.\r\n\r\n" + ex,
                    PluginName + " - error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void WriteCrashLog(string commandName, Exception exception)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "NppXsdViewer.log");
                File.AppendAllText(
                    logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + commandName + Environment.NewLine +
                    "Plugin DLL: " + typeof(PluginHost).Assembly.Location + Environment.NewLine +
                    "AppDomain BaseDirectory: " + AppDomain.CurrentDomain.BaseDirectory + Environment.NewLine +
                    exception + Environment.NewLine +
                    new string('-', 80) + Environment.NewLine);
            }
            catch
            {
                // A diagnosztikai napló hibája nem írhatja felül az eredeti hibát.
            }
        }

        private static void ShowViewer()
        {
            EnsureViewer();
            RefreshViewer(false);
            Win32.SendMessage(nppData.NppHandle, NppMessages.DmmShow, IntPtr.Zero, viewer.Handle);
        }

        private static void RefreshViewer()
        {
            RefreshViewer(true);
        }

        private static void RefreshViewer(bool showInfoWhenNotXsd)
        {
            EnsureViewer();
            string path = CurrentFilePath();
            if (!path.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                viewer.ClearSchema("The current document is not a saved .xsd file.");
                if (showInfoWhenNotXsd)
                {
                    MessageBox.Show(
                        "The current document is not a saved .xsd file.",
                        PluginName,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return;
            }

            viewer.LoadSchema(path);
        }

        private static void EnsureViewer()
        {
            if (viewer != null && !viewer.IsDisposed) return;

            viewer = new XsdViewerForm();
            viewer.CreateControl();
            viewer.ReloadRequested += delegate { RefreshViewer(true); };
            viewer.NavigateRequested += OnNavigateRequested;
            viewer.OpenResourceRequested += OnOpenResourceRequested;

            NppTbData dockingData = new NppTbData
            {
                ClientHandle = viewer.Handle,
                Name = "XSD Schema Viewer",
                DialogId = ShowViewerCommandId,
                Mask = NppTbMsg.DwsDefaultRight,
                IconTab = 0,
                AdditionalInfo = string.Empty,
                FloatRect = new Rect(),
                PreviousContainer = 0,
                ModuleName = PluginName
            };

            dockingDataPointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(NppTbData)));
            Marshal.StructureToPtr(dockingData, dockingDataPointer, false);
            Win32.SendMessage(
                nppData.NppHandle,
                NppMessages.DmmRegisterAsDockDialog,
                IntPtr.Zero,
                dockingDataPointer);
        }

        private static string CurrentFilePath()
        {
            StringBuilder buffer = new StringBuilder(32768);
            Win32.SendMessage(
                nppData.NppHandle,
                NppMessages.GetFullCurrentPath,
                new IntPtr(buffer.Capacity),
                buffer);
            return buffer.ToString();
        }

        private static void OnNavigateRequested(object sender, SchemaLocationEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.SourceUri))
            {
                string sourcePath = SourceUriToLocalPath(args.SourceUri);
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    string currentPath = CurrentFilePath();
                    bool isCurrent = !string.IsNullOrWhiteSpace(currentPath)
                                     && string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(currentPath), StringComparison.OrdinalIgnoreCase);
                    if (!isCurrent)
                        OpenInNotepad(sourcePath);
                }
            }

            IntPtr currentScintillaPointer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(currentScintillaPointer, 0);
                Win32.SendMessage(
                    nppData.NppHandle,
                    NppMessages.GetCurrentScintilla,
                    IntPtr.Zero,
                    currentScintillaPointer);

                int currentScintilla = Marshal.ReadInt32(currentScintillaPointer);
                IntPtr scintilla = currentScintilla == 0
                    ? nppData.ScintillaMainHandle
                    : nppData.ScintillaSecondHandle;

                HighlightAndCenterSourceElement(scintilla, args.LineNumber);
            }
            finally
            {
                Marshal.FreeHGlobal(currentScintillaPointer);
            }
        }


        private static void OnOpenResourceRequested(object sender, SchemaResourceEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.Location)) return;
            string localPath = SourceUriToLocalPath(args.Location);
            if (!string.IsNullOrWhiteSpace(localPath) && File.Exists(localPath))
            {
                OpenInNotepad(localPath);
                return;
            }

            if (Uri.TryCreate(args.Location, UriKind.Absolute, out Uri uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, PluginName + " - open resource", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void OpenInNotepad(string path)
        {
            IntPtr pathPointer = Marshal.StringToHGlobalUni(path);
            try
            {
                Win32.SendMessage(nppData.NppHandle, NppMessages.DoOpen, IntPtr.Zero, pathPointer);
            }
            finally
            {
                Marshal.FreeHGlobal(pathPointer);
            }
        }

        private static string SourceUriToLocalPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
                return uri.IsFile ? uri.LocalPath : string.Empty;
            return value;
        }

        private static void HighlightAndCenterSourceElement(IntPtr scintilla, int lineNumber)
        {
            var zeroBasedLine = Math.Max(0, lineNumber - 1);
            var lineStart = ToInt32(Win32.SendMessage(
                scintilla,
                ScintillaMessages.PositionFromLine,
                new IntPtr(zeroBasedLine),
                IntPtr.Zero));

            var documentLength = ToInt32(Win32.SendMessage(
                scintilla,
                ScintillaMessages.GetLength,
                IntPtr.Zero,
                IntPtr.Zero));

            if (documentLength <= 0 || lineStart < 0)
            {
                Win32.SendMessage(
                    scintilla,
                    ScintillaMessages.GoToLine,
                    new IntPtr(zeroBasedLine),
                    IntPtr.Zero);
                return;
            }

            var document = ReadScintillaUtf8(scintilla, documentLength);
            if (!XmlSourceSpanFinder.TryFindElement(document, lineStart, out var span) || span.Length <= 0)
            {
                Win32.SendMessage(
                    scintilla,
                    ScintillaMessages.GoToLine,
                    new IntPtr(zeroBasedLine),
                    IntPtr.Zero);
                CenterLine(scintilla, zeroBasedLine);
                return;
            }

            ConfigureSourceHighlight(scintilla);
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.SetIndicatorCurrent,
                new IntPtr(SourceHighlightIndicator),
                IntPtr.Zero);
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.IndicatorClearRange,
                IntPtr.Zero,
                new IntPtr(documentLength));
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.IndicatorFillRange,
                new IntPtr(span.Start),
                new IntPtr(span.Length));

            Win32.SendMessage(
                scintilla,
                ScintillaMessages.GoToPos,
                new IntPtr(span.Start),
                IntPtr.Zero);
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.ScrollCaret,
                IntPtr.Zero,
                IntPtr.Zero);

            var targetLine = ToInt32(Win32.SendMessage(
                scintilla,
                ScintillaMessages.LineFromPosition,
                new IntPtr(span.Start),
                IntPtr.Zero));
            CenterLine(scintilla, targetLine);
        }

        private static void ConfigureSourceHighlight(IntPtr scintilla)
        {
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.IndicSetStyle,
                new IntPtr(SourceHighlightIndicator),
                new IntPtr(ScintillaIndicatorStyles.FullBox));
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.IndicSetFore,
                new IntPtr(SourceHighlightIndicator),
                new IntPtr(0x00D7FF)); // RGB(255, 215, 0) - sárga/arany
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.IndicSetAlpha,
                new IntPtr(SourceHighlightIndicator),
                new IntPtr(90));
        }

        private static void CenterLine(IntPtr scintilla, int targetLine)
        {
            targetLine = Math.Max(0, targetLine);
            Win32.SendMessage(
                scintilla,
                ScintillaMessages.EnsureVisible,
                new IntPtr(targetLine),
                IntPtr.Zero);

            var firstVisible = ToInt32(Win32.SendMessage(
                scintilla,
                ScintillaMessages.GetFirstVisibleLine,
                IntPtr.Zero,
                IntPtr.Zero));
            var linesOnScreen = Math.Max(1, ToInt32(Win32.SendMessage(
                scintilla,
                ScintillaMessages.LinesOnScreen,
                IntPtr.Zero,
                IntPtr.Zero)));

            var desiredFirst = Math.Max(0, targetLine - linesOnScreen / 2);
            var delta = desiredFirst - firstVisible;
            if (delta != 0)
            {
                Win32.SendMessage(
                    scintilla,
                    ScintillaMessages.LineScroll,
                    IntPtr.Zero,
                    new IntPtr(delta));
            }
        }

        private static byte[] ReadScintillaUtf8(IntPtr scintilla, int documentLength)
        {
            IntPtr buffer = Marshal.AllocHGlobal(documentLength + 1);
            try
            {
                Win32.SendMessage(
                    scintilla,
                    ScintillaMessages.GetText,
                    new IntPtr(documentLength + 1),
                    buffer);
                var bytes = new byte[documentLength];
                Marshal.Copy(buffer, bytes, 0, documentLength);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static int ToInt32(IntPtr value)
            => unchecked((int)value.ToInt64());
    }
}
