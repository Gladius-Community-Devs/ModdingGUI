namespace ModdingGUI.Tests.Mcp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

/// <summary>
/// End-to-end tests against the real <c>ModdingGUI.Mcp</c> process over stdio: the handshake, the tool
/// catalogue, a read, a write that lands on disk with a <c>.mod-history</c> backup, strict mode in both
/// directions, the log tools, and the job-backed pack/unpack.
/// </summary>
/// <remarks>
/// Every test starts its own server against its own staged project, so the tests are independent and a
/// failure names the exact tool call that broke. <see cref="CleanBecFactAttribute"/> skips them when the
/// read-only reference corpus is not present, the same way the round-trip gate does.
/// </remarks>
public sealed class McpServerTests
{
    [CleanBecFact]
    public async Task InitializeListReadAndWriteWorkEndToEnd()
    {
        await using McpSession session = await McpSession.StartAsync();

        // tools/list: the catalogue a client would see. McpClient has already completed initialize and
        // notifications/initialized, so the handshake is proven by this point.
        Assert.True(session.Tools.Count > 100, $"only {session.Tools.Count} tools were advertised");
        Assert.All(session.Tools, tool => Assert.False(string.IsNullOrWhiteSpace(tool.Description)));
        Assert.All(session.Tools, tool => Assert.NotEqual(System.Text.Json.JsonValueKind.Undefined, tool.JsonSchema.ValueKind));

        // A read: every item id, in source order.
        CallToolResult listResult = await session.CallAsync("items_list");
        JsonNode? ids = McpSession.Json(listResult);
        Assert.NotNull(ids);
        Assert.True(ids!.AsArray().Count > 100, "items_list returned implausibly few ids");

        string itemId = ids.AsArray()[0]!.GetValue<string>();
        CallToolResult getResult = await session.CallAsync("items_get", new Dictionary<string, object?> { ["name"] = itemId });
        JsonNode? item = McpSession.Json(getResult);
        Assert.NotNull(item);
        Assert.Equal(itemId, item!["name"]!.GetValue<string>());

        // A write: one field of that item.
        byte[] before = session.ReadProjectBytes("data/config/items.tok");
        Assert.Empty(session.BackupsOf("data/config/items.tok"));

        CallToolResult patchResult = await session.CallAsync("items_patch", new Dictionary<string, object?>
        {
            ["name"] = itemId,
            ["patch"] = new Dictionary<string, object?> { ["cost"] = 4242 },
            ["strict"] = false,
        });

        JsonNode? written = McpSession.Json(patchResult);
        Assert.NotNull(written);
        Assert.Equal(4242, written!["result"]!["cost"]!.GetValue<long>());

        // It landed on disk, changed the file, and is recoverable.
        Assert.Contains("4242", session.ReadProjectFile("data/config/items.tok"), StringComparison.Ordinal);
        Assert.NotEqual(before, session.ReadProjectBytes("data/config/items.tok"));

        string[] backups = session.BackupsOf("data/config/items.tok");
        Assert.Single(backups);
        Assert.Equal(before, File.ReadAllBytes(backups[0]));

        // And the log the GUI would render is readable over MCP, with the finding/record of the write.
        CallToolResult logResult = await session.CallAsync("logs_tail", new Dictionary<string, object?>
        {
            ["count"] = 200,
        });

        string logText = McpSession.Text(logResult);
        Assert.Contains("items_patch", logText, StringComparison.OrdinalIgnoreCase);
    }

    [CleanBecFact]
    public async Task StrictModeWritesAdvisoryThenRefusesAndRestores()
    {
        await using McpSession session = await McpSession.StartAsync();

        const string relativePath = "data/units/statsets.txt";

        // A row with an implausible stat value: StatSetsApi rejects it with a `stat-out-of-range` warning.
        var invalidRow = new Dictionary<string, object?>
        {
            ["level"] = 1,
            ["con"] = 9999,
            ["pow"] = 1,
            ["acc"] = 1,
            ["def"] = 1,
            ["iniMov"] = 1,
        };

        byte[] before = session.ReadProjectBytes(relativePath);

        // strict: false — the GUI's advisory behaviour: it writes, and reports what it thinks.
        CallToolResult advisory = await session.CallAsync("statsets_set_row", new Dictionary<string, object?>
        {
            ["index"] = 0,
            ["row"] = invalidRow,
            ["strict"] = false,
        });

        JsonNode? advisoryPayload = McpSession.Json(advisory);
        Assert.NotNull(advisoryPayload);
        JsonArray warnings = advisoryPayload!["warnings"]!.AsArray();
        Assert.Contains(warnings, warning => warning!["code"]!.GetValue<string>() == "stat-out-of-range");

        byte[] afterAdvisory = session.ReadProjectBytes(relativePath);
        Assert.NotEqual(before, afterAdvisory);
        Assert.Single(session.BackupsOf(relativePath));

        // strict: true — the same kind of payload on a different level, which genuinely changes the file and
        // genuinely introduces a warning. It must be refused, and the file must come back byte-identical.
        var secondInvalidRow = new Dictionary<string, object?>(invalidRow) { ["level"] = 2 };

        CallToolResult refused = await session.Tool("statsets_set_row").CallAsync(new Dictionary<string, object?>
        {
            ["index"] = 0,
            ["row"] = secondInvalidRow,
            ["strict"] = true,
        });

        Assert.True(McpSession.IsError(refused), "strict mode accepted a row it should have refused");
        Assert.Contains("refused by strict mode", McpSession.Text(refused), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stat-out-of-range", McpSession.Text(refused), StringComparison.OrdinalIgnoreCase);

        Assert.Equal(afterAdvisory, session.ReadProjectBytes(relativePath));
    }

    [CleanBecFact]
    public async Task EveryCoreApiMethodHasATool()
    {
        await using McpSession session = await McpSession.StartAsync();

        var families = new (string Prefix, Type Interface)[]
        {
            ("items", typeof(ModdingGUI.Core.Api.IItemsApi)),
            ("skills", typeof(ModdingGUI.Core.Api.ISkillsApi)),
            ("lookup", typeof(ModdingGUI.Core.Api.ILookupTextApi)),
            ("classes", typeof(ModdingGUI.Core.Api.IClassesApi)),
            ("encounters", typeof(ModdingGUI.Core.Api.IEncountersApi)),
            ("leagues", typeof(ModdingGUI.Core.Api.ILeaguesApi)),
            ("schools", typeof(ModdingGUI.Core.Api.ISchoolsApi)),
            ("schoolnames", typeof(ModdingGUI.Core.Api.ISchoolNamesApi)),
            ("grids", typeof(ModdingGUI.Core.Api.IGridsApi)),
            ("gladiators", typeof(ModdingGUI.Core.Api.IGladiatorsApi)),
            ("statsets", typeof(ModdingGUI.Core.Api.IStatSetsApi)),
            ("skillsets", typeof(ModdingGUI.Core.Api.ISkillSetsApi)),
            ("itemsets", typeof(ModdingGUI.Core.Api.IItemSetsApi)),
            ("tintsets", typeof(ModdingGUI.Core.Units.ITintSetsApi)),
            ("config", typeof(ModdingGUI.Core.Api.IConfigTableApi)),
            ("history", typeof(ModdingGUI.Core.Api.IHistoryApi)),
        };

        // The 16 API interfaces are the contract. A new one has to be added here and given tools.
        Type[] discovered = typeof(ModdingGUI.Core.Api.IItemsApi).Assembly.GetTypes()
            .Where(type => type.IsInterface && type.IsPublic
                           && type.Name.StartsWith('I') && type.Name.EndsWith("Api", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(families.Length, discovered.Length);
        Assert.Equal(
            discovered.OrderBy(type => type.Name, StringComparer.Ordinal).Select(type => type.Name),
            families.Select(family => family.Interface.Name).OrderBy(name => name, StringComparer.Ordinal));

        var toolNames = session.Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();

        foreach ((string prefix, Type contract) in families)
        {
            foreach (System.Reflection.MethodInfo method in contract.GetMethods())
            {
                string expected = prefix + "_" + Snake(method.Name);

                // Overloads share the stem and are told apart by a suffix (skills_add / skills_add_validated,
                // lookup_load / lookup_load_language, history_restore / history_restore_entry).
                bool covered = toolNames.Contains(expected)
                               || toolNames.Any(name => name.StartsWith(expected + "_", StringComparison.Ordinal));

                if (!covered)
                {
                    string parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    missing.Add($"{contract.Name}.{method.Name}({parameters}) -> expected a tool named '{expected}'");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "Every Core API method must be reachable over MCP. Missing:" +
            Environment.NewLine + string.Join(Environment.NewLine, missing));
    }

    [CleanBecFact]
    public async Task ProjectUnpackRunsAsAJobAndReportsItsResult()
    {
        await using McpSession session = await McpSession.StartAsync();

        // A missing ISO fails the job immediately, which exercises the whole job path — Started/Finished,
        // progress notifications, a BuildResult with steps — without unpacking a 1.4 GB image.
        var progress = new CollectingProgress();

        CallToolResult result = await session.Tool("project_unpack").CallAsync(
            new Dictionary<string, object?>
            {
                ["iso"] = System.IO.Path.Combine(session.ProjectPath, "no-such-iso.iso"),
                ["projectName"] = "mcp-test-unpack",
            },
            progress: progress);

        JsonNode? report = McpSession.Json(result);
        Assert.NotNull(report);
        Assert.Equal("unpack", report!["kind"]!.GetValue<string>());
        Assert.False(report["succeeded"]!.GetValue<bool>());
        Assert.NotEqual(0, report["exitCode"]!.GetValue<int>());
        Assert.NotEmpty(report["steps"]!.AsArray());
        Assert.Equal("Failed", report["state"]!.GetValue<string>());

        // forward Started/Finished as progress: at least the two the tool sends around the job.
        IReadOnlyList<ProgressNotificationValue> notifications = await progress.WaitForAsync(2, TimeSpan.FromSeconds(10));
        Assert.True(notifications.Count >= 2, $"expected at least two progress notifications, got {notifications.Count}");
        Assert.Contains(notifications, item => item.Message?.Contains("unpack started", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(notifications, item => item.Message?.Contains("unpack finished", StringComparison.OrdinalIgnoreCase) == true);
    }

    /// <summary>
    /// Collects progress notifications synchronously. <see cref="Progress{T}"/> posts callbacks through the
    /// thread pool, which would make the assertion race the dispatcher.
    /// </summary>
    private sealed class CollectingProgress : IProgress<ProgressNotificationValue>
    {
        private readonly List<ProgressNotificationValue> _items = new();

        public void Report(ProgressNotificationValue value)
        {
            lock (_items) _items.Add(value);
        }

        public async Task<IReadOnlyList<ProgressNotificationValue>> WaitForAsync(int count, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (_items)
                {
                    if (_items.Count >= count) return _items.ToArray();
                }

                await Task.Delay(25);
            }

            lock (_items) return _items.ToArray();
        }
    }

    /// <summary><c>SetTeamUnits</c> → <c>set_team_units</c>: the naming rule the coverage test relies on.</summary>
    private static string Snake(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c)) builder.Append('_');
            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
