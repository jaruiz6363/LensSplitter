using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AberrationCalculator.Mcp;

/// <summary>
/// An MCP server over the aberration calculator, speaking JSON-RPC 2.0 on stdin and stdout.
///
/// <para>The protocol is implemented directly rather than through a package. It is three
/// methods - <c>initialize</c>, <c>tools/list</c>, <c>tools/call</c> - and writing them out
/// costs less than a preview dependency would, in a program whose whole point is that its
/// numbers can be traced to something.</para>
///
/// <para><b>stdout carries the protocol and nothing else.</b> A stray line of output there
/// corrupts the stream and the client sees a parse error rather than whatever went wrong, so
/// everything diagnostic goes to stderr.</para>
/// </summary>
internal static class Program
{
    private const string ProtocolVersion = "2024-11-05";

    private static int Main()
    {
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
        var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));

        string? line;
        while ((line = stdin.ReadLine()) != null)
        {
            if (line.Length == 0) continue;

            JsonNode? request;
            try
            {
                request = JsonNode.Parse(line);
            }
            catch (JsonException e)
            {
                Console.Error.WriteLine($"abcalc-mcp: unparseable message: {e.Message}");
                continue;
            }
            if (request == null) continue;

            string method = request["method"]?.GetValue<string>() ?? "";
            JsonNode? id = request["id"];

            // A notification has no id and takes no reply - "initialized" is the usual one.
            if (id == null) continue;

            try
            {
                JsonNode result = Dispatch(method, request["params"]);
                Respond(stdout, id, result);
            }
            catch (MethodNotFound)
            {
                RespondError(stdout, id, -32601, $"unknown method: {method}");
            }
            catch (Exception e)
            {
                // A failure to read a lens is the caller's problem to see and fix, so it comes
                // back as the tool's result rather than as a protocol error.
                RespondError(stdout, id, -32000, e.Message);
            }
        }
        return 0;
    }

    private sealed class MethodNotFound : Exception { }

    private static JsonNode Dispatch(string method, JsonNode? args) => method switch
    {
        "initialize" => new JsonObject
        {
            ["protocolVersion"] = ProtocolVersion,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject() },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "abcalc",
                ["version"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            },
        },
        "ping" => new JsonObject(),
        "tools/list" => ToolList(),
        "tools/call" => CallTool(args),
        _ => throw new MethodNotFound(),
    };

    private static JsonNode ToolList()
    {
        var tools = new JsonArray();
        foreach (var t in Tools.All)
        {
            var properties = new JsonObject
            {
                ["lens_file"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] =
                        "Path to the lens file. ZEMAX .zmx, CODE V .seq, OPTALIX "
                      + ".otx/.opt, OSLO .len/.osl, Optiland .json or LensHH-LT .lhlt "
                      + "- the format is taken from the extension.",
                },
                ["glass_dir"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] =
                        "Optional folder of .agf catalogs to use instead of the "
                      + "bundled ones. Note that a glass NAME alone does not say "
                      + "whose glass it is: some formats carry no catalog, and "
                      + "picking a different vendor's glass of the same name can "
                      + "move the focal length by a per cent or two in silence.",
                },
            };

            // The few reporting tools that take something of their own. Advertised here rather
            // than described in prose, so that a caller's schema validation knows about them and
            // an argument cannot be documented without being accepted.
            if (t.Extra != null)
                foreach (var spec in t.Extra)
                    properties[spec.Name] = new JsonObject
                    {
                        ["type"] = spec.Type,
                        ["description"] = spec.Description,
                    };

            tools.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = new JsonArray { "lens_file" },
                },
            });
        }

        // The tools that change a design rather than report on one carry their own arguments,
        // so they bring their own schema too.
        foreach (var t in ActionTools.All)
        {
            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var arg in t.Arguments)
            {
                properties[arg.Name] = new JsonObject
                {
                    ["type"] = arg.Type,
                    ["description"] = arg.Description,
                };
                if (arg.Required) required.Add(arg.Name);
            }

            tools.Add(new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                },
            });
        }
        return new JsonObject { ["tools"] = tools };
    }

    private static JsonNode CallTool(JsonNode? args)
    {
        string name = args?["name"]?.GetValue<string>()
                      ?? throw new ArgumentException("tools/call needs a tool name");
        JsonNode? a = args?["arguments"];

        foreach (var action in ActionTools.All)
            if (string.Equals(action.Name, name, StringComparison.Ordinal))
                return new JsonObject
                {
                    ["content"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "text", ["text"] = action.Run(a) },
                    },
                };

        Tool? tool = null;
        foreach (var t in Tools.All)
            if (string.Equals(t.Name, name, StringComparison.Ordinal)) { tool = t; break; }
        if (tool == null) throw new ArgumentException($"unknown tool: {name}");

        string lens = a?["lens_file"]?.GetValue<string>()
                      ?? throw new ArgumentException("lens_file is required");
        string? glass = a?["glass_dir"]?.GetValue<string>();

        var writer = Tools.Open(lens, glass);
        string text = tool.Run(writer, a);

        // Unresolved materials are reported rather than thrown: the analysis is still
        // meaningful, but every number that depends on the missing glass is not, and the
        // caller has no other way to find that out.
        if (writer.Unresolved.Count > 0)
            text = "WARNING: unresolved materials: " + string.Join(", ", writer.Unresolved)
                 + "\nEvery quantity that depends on them is unreliable.\n\n" + text;

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject { ["type"] = "text", ["text"] = text },
            },
        };
    }

    private static void Respond(TextWriter output, JsonNode id, JsonNode result)
        => output.WriteLine(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["result"] = result,
        }.ToJsonString());

    private static void RespondError(TextWriter output, JsonNode id, int code, string message)
        => output.WriteLine(new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id.DeepClone(),
            ["error"] = new JsonObject { ["code"] = code, ["message"] = message },
        }.ToJsonString());
}
