using System;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace MCPForUnity.Editor.Services.Server
{
    /// <summary>
    /// Builds uvx/server command strings for starting the MCP HTTP server.
    /// Handles platform-specific command construction.
    /// </summary>
    public class ServerCommandBuilder : IServerCommandBuilder
    {
        /// <inheritdoc/>
        public bool TryBuildCommand(out string fileName, out string arguments, out string displayCommand, out string error)
        {
            fileName = null;
            arguments = null;
            displayCommand = null;
            error = null;

            bool useHttpTransport = EditorConfigurationCache.Instance.UseHttpTransport;
            if (!useHttpTransport)
            {
                error = "HTTP transport is disabled. Enable it in the MCP For Unity window first.";
                return false;
            }

            string httpUrl = HttpEndpointUtility.GetLocalBaseUrl();
            if (!HttpEndpointUtility.IsHttpLocalUrlAllowedForLaunch(httpUrl, out string localUrlError))
            {
                error = string.IsNullOrEmpty(localUrlError)
                    ? $"The configured URL ({httpUrl}) is not allowed for HTTP Local launch."
                    : $"{localUrlError} (configured URL: {httpUrl})";
                return false;
            }

            bool projectScopedTools = EditorPrefs.GetBool(
                EditorPrefKeys.ProjectScopedToolsLocalHttp,
                true
            );
            string uvxPath = MCPServiceLocator.Paths.GetUvxPath();
            if (!AssetPathUtility.TryGetPreferredHttpCommand(
                    uvxPath,
                    httpUrl,
                    projectScopedTools,
                    out var command,
                    out var commandArgs,
                    out error))
            {
                return false;
            }

            string args = string.Join(" ", commandArgs.Select(QuoteIfNeeded));
            fileName = command;
            arguments = args;
            displayCommand = $"{QuoteIfNeeded(command)} {args}";
            return true;
        }

        /// <inheritdoc/>
        public string BuildUvPathFromUvx(string uvxPath)
        {
            if (string.IsNullOrWhiteSpace(uvxPath))
            {
                return uvxPath;
            }

            string directory = Path.GetDirectoryName(uvxPath);
            string extension = Path.GetExtension(uvxPath);
            string uvFileName = "uv" + extension;

            return string.IsNullOrEmpty(directory)
                ? uvFileName
                : Path.Combine(directory, uvFileName);
        }

        /// <inheritdoc/>
        public string GetPlatformSpecificPathPrepend()
        {
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                return string.Join(Path.PathSeparator.ToString(), new[]
                {
                    "/opt/homebrew/bin",
                    "/usr/local/bin",
                    "/usr/bin",
                    "/bin"
                });
            }

            if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                return string.Join(Path.PathSeparator.ToString(), new[]
                {
                    "/usr/local/bin",
                    "/usr/bin",
                    "/bin"
                });
            }

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

                return string.Join(Path.PathSeparator.ToString(), new[]
                {
                    !string.IsNullOrEmpty(localAppData) ? Path.Combine(localAppData, "Programs", "uv") : null,
                    !string.IsNullOrEmpty(programFiles) ? Path.Combine(programFiles, "uv") : null
                }.Where(p => !string.IsNullOrEmpty(p)).ToArray());
            }

            return null;
        }

        /// <inheritdoc/>
        public string QuoteIfNeeded(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.IndexOf(' ') >= 0 ? $"\"{input}\"" : input;
        }

    }
}
