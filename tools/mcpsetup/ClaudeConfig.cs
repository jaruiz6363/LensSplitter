using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AberrationCalculator.McpSetup
{
    /// <summary>
    /// Reading and editing a Claude client's JSON configuration.
    ///
    /// <para>Separated from the window, and with no reference to WPF, so that the part which
    /// can do damage is the part that is tested. This edits a file the user did not write and
    /// probably cares about: it usually holds other MCP servers, and losing them to a tool
    /// meant to help would be a poor trade. Every method here is written to leave alone
    /// anything it does not own.</para>
    /// </summary>
    public static class ClaudeConfig
    {
        /// <summary>
        /// The file as a JSON object. A missing or empty file is an empty object rather than an
        /// error - that is a client which has simply never registered anything.
        /// </summary>
        public static JsonObject Load(string path)
        {
            if (!File.Exists(path)) return new JsonObject();
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
            return JsonNode.Parse(text) as JsonObject
                   ?? throw new InvalidDataException("the configuration is not a JSON object");
        }

        /// <summary>The MCP servers already registered, in file order.</summary>
        public static IReadOnlyList<string> ServerNames(JsonObject root) =>
            root["mcpServers"] is JsonObject servers
                ? servers.Select(kv => kv.Key).ToList()
                : new List<string>();

        /// <summary>
        /// Adds or replaces one server, leaving every other server and every other top-level
        /// key exactly as it was.
        /// </summary>
        public static void Register(JsonObject root, string name, string command)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("a name is required", nameof(name));
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("a command is required", nameof(command));

            if (root["mcpServers"] is not JsonObject servers)
            {
                servers = new JsonObject();
                root["mcpServers"] = servers;
            }
            servers.Remove(name);
            servers[name] = new JsonObject { ["command"] = command, ["args"] = new JsonArray() };
        }

        /// <summary>Takes one server out. False if it was not there, which is not an error.</summary>
        public static bool Remove(JsonObject root, string name) =>
            root["mcpServers"] is JsonObject servers && servers.Remove(name);

        /// <summary>
        /// Writes the file back, having first copied it aside. The backup is the point: this is
        /// somebody's working setup, and a bad write should be a nuisance rather than a loss.
        /// Returns the backup path, or empty when there was nothing yet to back up.
        /// </summary>
        public static string Save(string path, JsonObject root)
        {
            string backup = string.Empty;
            if (File.Exists(path))
            {
                backup = path + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                for (int n = 1; File.Exists(backup); n++) backup = path + ".backup-"
                    + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + n;
                File.Copy(path, backup);
            }
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            return backup;
        }
    }
}
