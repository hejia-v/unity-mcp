using System;
using System.Collections.Generic;
using System.IO;
using MCPForUnity.Editor.Clients;
using MCPForUnity.Editor.Constants;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Models;
using MCPForUnity.Editor.Services;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace MCPForUnityTests.Editor.Clients
{
    public class LocalSourcePythonConfiguratorTests
    {
        private sealed class TestJsonConfigurator : JsonFileMcpConfigurator
        {
            public TestJsonConfigurator(string configPath, bool supportsHttpTransport = false) : base(new McpClient
            {
                name = "Test JSON Client",
                windowsConfigPath = configPath,
                macConfigPath = configPath,
                linuxConfigPath = configPath,
                SupportsHttpTransport = supportsHttpTransport
            })
            {
            }

            public override IList<string> GetInstallationSteps() => Array.Empty<string>();
        }

        private sealed class TestCodexConfigurator : CodexMcpConfigurator
        {
            public TestCodexConfigurator(string configPath) : base(new McpClient
            {
                name = "Test Codex Client",
                windowsConfigPath = configPath,
                macConfigPath = configPath,
                linuxConfigPath = configPath,
                SupportsHttpTransport = true
            })
            {
            }
        }

        private string _tempRoot;
        private string _serverRoot;
        private bool _hadGitOverride;
        private string _originalGitOverride;
        private bool _hadHttpTransport;
        private bool _originalHttpTransport;

        [SetUp]
        public void SetUp()
        {
            _hadGitOverride = EditorPrefs.HasKey(EditorPrefKeys.GitUrlOverride);
            _originalGitOverride = EditorPrefs.GetString(EditorPrefKeys.GitUrlOverride, string.Empty);
            _hadHttpTransport = EditorPrefs.HasKey(EditorPrefKeys.UseHttpTransport);
            _originalHttpTransport = EditorPrefs.GetBool(EditorPrefKeys.UseHttpTransport, true);

            _tempRoot = Path.Combine(Path.GetTempPath(), "UnityMCPTests", Guid.NewGuid().ToString("N"));
            _serverRoot = Path.Combine(_tempRoot, "Server");
            Directory.CreateDirectory(Path.Combine(_serverRoot, "src"));
            File.WriteAllText(Path.Combine(_serverRoot, "src", "main.py"), "print('test')");

            EditorPrefs.SetString(EditorPrefKeys.GitUrlOverride, _serverRoot);
            EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, false);
            EditorConfigurationCache.Instance.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadGitOverride)
                EditorPrefs.SetString(EditorPrefKeys.GitUrlOverride, _originalGitOverride);
            else
                EditorPrefs.DeleteKey(EditorPrefKeys.GitUrlOverride);

            if (_hadHttpTransport)
                EditorPrefs.SetBool(EditorPrefKeys.UseHttpTransport, _originalHttpTransport);
            else
                EditorPrefs.DeleteKey(EditorPrefKeys.UseHttpTransport);

            EditorConfigurationCache.Instance.Refresh();

            try
            {
                if (Directory.Exists(_tempRoot))
                    Directory.Delete(_tempRoot, true);
            }
            catch
            {
            }
        }

        [Test]
        public void JsonConfigurator_CheckStatus_LocalSourcePythonConfig_IsConfigured()
        {
            Assert.IsTrue(
                AssetPathUtility.TryGetPreferredStdioCommand(MCPServiceLocator.Paths.GetUvxPath(), out var command, out var args, out var error),
                error);

            string configPath = Path.Combine(_tempRoot, "client.json");
            var root = new JObject
            {
                ["mcpServers"] = new JObject
                {
                    ["unityMCP"] = new JObject
                    {
                        ["command"] = command,
                        ["args"] = JArray.FromObject(args),
                        ["type"] = "stdio"
                    }
                }
            };
            File.WriteAllText(configPath, root.ToString());

            var configurator = new TestJsonConfigurator(configPath);
            McpStatus status = configurator.CheckStatus(attemptAutoRewrite: false);

            Assert.AreEqual(McpStatus.Configured, status);
            Assert.AreEqual(ConfiguredTransport.Stdio, configurator.ConfiguredTransport);
        }

        [Test]
        public void JsonConfigurator_GetManualSnippet_LocalSourcePythonConfig_UsesPythonCommand()
        {
            Assert.IsTrue(
                AssetPathUtility.TryGetPreferredStdioCommand(MCPServiceLocator.Paths.GetUvxPath(), out var command, out _, out var error),
                error);

            var configurator = new TestJsonConfigurator(Path.Combine(_tempRoot, "manual.json"));
            string snippet = configurator.GetManualSnippet();
            var root = JObject.Parse(snippet);
            var unity = (JObject)root["mcpServers"]?["unityMCP"];

            Assert.NotNull(unity);
            Assert.AreEqual(command, (string)unity["command"]);
            Assert.AreEqual("stdio", (string)unity["type"]);
        }

        [Test]
        public void CodexConfigurator_CheckStatus_LocalSourcePythonConfig_IsConfigured()
        {
            string configPath = Path.Combine(_tempRoot, "config.toml");
            string toml = CodexConfigHelper.BuildCodexServerBlock(MCPServiceLocator.Paths.GetUvxPath());
            File.WriteAllText(configPath, toml);

            var configurator = new TestCodexConfigurator(configPath);
            McpStatus status = configurator.CheckStatus(attemptAutoRewrite: false);

            Assert.AreEqual(McpStatus.Configured, status);
            Assert.AreEqual(ConfiguredTransport.Stdio, configurator.ConfiguredTransport);
        }

        [Test]
        public void CodexConfigurator_GetManualSnippet_LocalSourcePythonConfig_UsesPythonCommand()
        {
            Assert.IsTrue(
                AssetPathUtility.TryGetPreferredStdioCommand(MCPServiceLocator.Paths.GetUvxPath(), out var command, out var args, out var error),
                error);

            var configurator = new TestCodexConfigurator(Path.Combine(_tempRoot, "manual.toml"));
            string snippet = configurator.GetManualSnippet();

            Assert.IsTrue(CodexConfigHelper.TryParseCodexServer(snippet, out var parsedCommand, out var parsedArgs));
            Assert.AreEqual(command, parsedCommand);
            CollectionAssert.AreEqual(args, parsedArgs);
        }
    }
}
