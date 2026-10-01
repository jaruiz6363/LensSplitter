using System;
using System.IO;
using System.Text.Json.Nodes;
using AberrationCalculator.McpSetup;
using Xunit;

namespace AberrationCalculator.Tests;

/// <summary>
/// The part of the MCP setup program that edits a Claude configuration.
///
/// <para>These exist because that file is not ours. It usually holds other MCP servers a
/// person depends on, and a setup tool that quietly dropped them would be worse than no
/// setup tool. Everything here is a test that something was <i>left alone</i>.</para>
/// </summary>
public class ClaudeConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(),
        "abcalc-cfg-" + Guid.NewGuid().ToString("N"));

    public ClaudeConfigTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private string Write(string json)
    {
        string path = Path.Combine(_dir, "claude_desktop_config.json");
        File.WriteAllText(path, json);
        return path;
    }

    /// <summary>The shape of a real config: other servers, and other top-level keys.</summary>
    private const string Existing = @"{
  ""mcpServers"": {
    ""server-one"": { ""command"": ""C:\\one.exe"", ""args"": [] },
    ""server-two"": { ""command"": ""C:\\two.exe"", ""args"": [""--flag""] }
  },
  ""coworkUserFilesPath"": ""C:\\files"",
  ""preferences"": { ""theme"": ""dark"" }
}";

    [Fact]
    public void RegisteringLeavesTheOtherServersAlone()
    {
        var root = ClaudeConfig.Load(Write(Existing));
        ClaudeConfig.Register(root, "abcalc", @"C:\abcalc-mcp.exe");

        var names = ClaudeConfig.ServerNames(root);
        Assert.Contains("server-one", names);
        Assert.Contains("server-two", names);
        Assert.Contains("abcalc", names);
        Assert.Equal(3, names.Count);

        // And their settings, not merely their names.
        var others = (JsonObject)root["mcpServers"]!;
        Assert.Equal(@"C:\two.exe", others["server-two"]!["command"]!.GetValue<string>());
        Assert.Single(others["server-two"]!["args"]!.AsArray());
    }

    [Fact]
    public void RegisteringLeavesTheOtherTopLevelKeysAlone()
    {
        var root = ClaudeConfig.Load(Write(Existing));
        ClaudeConfig.Register(root, "abcalc", @"C:\abcalc-mcp.exe");

        Assert.Equal(@"C:\files", root["coworkUserFilesPath"]!.GetValue<string>());
        Assert.Equal("dark", root["preferences"]!["theme"]!.GetValue<string>());
    }

    /// <summary>Registering twice is a correction, not a duplicate.</summary>
    [Fact]
    public void RegisteringTwiceReplacesRatherThanDuplicates()
    {
        var root = ClaudeConfig.Load(Write(Existing));
        ClaudeConfig.Register(root, "abcalc", @"C:\old.exe");
        ClaudeConfig.Register(root, "abcalc", @"C:\new.exe");

        Assert.Equal(3, ClaudeConfig.ServerNames(root).Count);
        Assert.Equal(@"C:\new.exe",
            root["mcpServers"]!["abcalc"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void RemovingTakesOnlyTheOneNamed()
    {
        var root = ClaudeConfig.Load(Write(Existing));
        Assert.True(ClaudeConfig.Remove(root, "server-one"));

        var names = ClaudeConfig.ServerNames(root);
        Assert.DoesNotContain("server-one", names);
        Assert.Contains("server-two", names);
        Assert.Equal("dark", root["preferences"]!["theme"]!.GetValue<string>());
    }

    /// <summary>Removing something that was never there is not an error.</summary>
    [Fact]
    public void RemovingSomethingAbsentSaysSoRatherThanThrowing()
    {
        var root = ClaudeConfig.Load(Write(Existing));
        Assert.False(ClaudeConfig.Remove(root, "never-registered"));
        Assert.Equal(2, ClaudeConfig.ServerNames(root).Count);
    }

    /// <summary>
    /// The backup is the safety net the whole design leans on, so it has to exist, and it has
    /// to hold what was there BEFORE the write rather than after.
    /// </summary>
    [Fact]
    public void SavingKeepsTheOriginalAside()
    {
        string path = Write(Existing);
        var root = ClaudeConfig.Load(path);
        ClaudeConfig.Register(root, "abcalc", @"C:\abcalc-mcp.exe");

        string backup = ClaudeConfig.Save(path, root);

        Assert.True(File.Exists(backup), "no backup was written");
        var restored = ClaudeConfig.Load(backup);
        Assert.Equal(2, ClaudeConfig.ServerNames(restored).Count);
        Assert.DoesNotContain("abcalc", ClaudeConfig.ServerNames(restored));

        // And the file that was written is the new one, and is valid JSON.
        Assert.Equal(3, ClaudeConfig.ServerNames(ClaudeConfig.Load(path)).Count);
    }

    /// <summary>A client that has never registered anything, and a first run.</summary>
    [Fact]
    public void AMissingOrEmptyConfigIsAnEmptyOneRatherThanAFailure()
    {
        string missing = Path.Combine(_dir, "not-there.json");
        Assert.Empty(ClaudeConfig.ServerNames(ClaudeConfig.Load(missing)));
        Assert.Empty(ClaudeConfig.ServerNames(ClaudeConfig.Load(Write("   "))));

        var root = ClaudeConfig.Load(missing);
        ClaudeConfig.Register(root, "abcalc", @"C:\abcalc-mcp.exe");
        string backup = ClaudeConfig.Save(missing, root);

        Assert.Equal(string.Empty, backup);   // nothing existed to back up
        Assert.Single(ClaudeConfig.ServerNames(ClaudeConfig.Load(missing)));
    }

    /// <summary>
    /// A config that is not JSON at all must stop the program rather than be overwritten with
    /// a fresh one - that file may be the only copy of something the user hand-edited.
    /// </summary>
    [Fact]
    public void AConfigThatIsNotJsonIsRefusedRatherThanReplaced()
    {
        string path = Write("this is not json");
        Assert.ThrowsAny<Exception>(() => ClaudeConfig.Load(path));
        Assert.Equal("this is not json", File.ReadAllText(path));
    }
}
