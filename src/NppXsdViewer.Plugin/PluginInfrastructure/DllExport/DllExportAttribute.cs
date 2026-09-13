using System;
using System.Runtime.InteropServices;

namespace RGiesecke.DllExport
{
    /// <summary>
    /// Az UnmanagedExports.Repack.Upgrade MSBuild target által felismert export attribútum.
    /// A teljes típusnévnek RGiesecke.DllExport.DllExportAttribute értékűnek kell maradnia.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal partial class DllExportAttribute : Attribute
    {
        public DllExportAttribute()
        {
        }

        public DllExportAttribute(string exportName)
            : this(exportName, CallingConvention.StdCall)
        {
        }

        public DllExportAttribute(string exportName, CallingConvention callingConvention)
        {
            ExportName = exportName;
            CallingConvention = callingConvention;
        }

        public CallingConvention CallingConvention { get; set; }

        public string ExportName { get; set; }
    }
}
