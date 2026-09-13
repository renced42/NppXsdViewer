using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace NppXsdViewer.Plugin.PluginInfrastructure
{
    /// <summary>
    /// A Notepad++ folyamatba betöltött managed plugin saját függőségeit
    /// a plugin DLL könyvtárából oldja fel.
    /// </summary>
    internal static class AssemblyResolver
    {
        private static readonly HashSet<string> AllowedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NppXsdViewer.Schema",
            "NppXsdViewer.Diagram"
        };

        private static bool installed;

        internal static void Install()
        {
            if (installed)
                return;

            AppDomain.CurrentDomain.AssemblyResolve += ResolvePluginAssembly;
            installed = true;
        }

        internal static void Uninstall()
        {
            if (!installed)
                return;

            AppDomain.CurrentDomain.AssemblyResolve -= ResolvePluginAssembly;
            installed = false;
        }

        private static Assembly? ResolvePluginAssembly(object? sender, ResolveEventArgs args)
        {
            AssemblyName requestedName;
            try
            {
                requestedName = new AssemblyName(args.Name);
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(requestedName.Name) || !AllowedAssemblies.Contains(requestedName.Name))
                return null;

            string pluginAssemblyPath = typeof(AssemblyResolver).Assembly.Location;
            string? pluginDirectory = Path.GetDirectoryName(pluginAssemblyPath);
            if (string.IsNullOrWhiteSpace(pluginDirectory))
                return null;

            string dependencyPath = Path.Combine(pluginDirectory, requestedName.Name + ".dll");
            if (!File.Exists(dependencyPath))
                return null;

            return Assembly.LoadFrom(dependencyPath);
        }
    }
}
