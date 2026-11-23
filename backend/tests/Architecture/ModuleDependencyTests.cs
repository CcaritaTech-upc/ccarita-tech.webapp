using System.Text.RegularExpressions;

namespace IoBuild.Architecture.Tests;

// Business modules (bounded contexts) must not reach into each other's code.
// Cross-cutting layers are allowed to know every module on purpose:
// Persistence (the single IoBuildDbContext), CoreBusiness and Workflows
// (orchestration), Shared, Contracts, Observability and Readiness. They are
// not scanned as sources, and they are not forbidden targets.
//
// This is a ratchet. When the rule was introduced the code already had the
// references listed in KnownViolations, so they are accepted as known debt:
//   - a NEW cross-module reference fails the first test;
//   - fixing one of the known references fails the second test until the entry
//     is removed, so the list can only shrink.
public sealed partial class ModuleDependencyTests
{
    private static readonly string[] BusinessModules =
        ["IAM", "Devices", "Profiles", "Publishing", "Subscriptions", "Analytics"];

    // "<file relative to backend/src/IoBuild.Api> -> <module it references>"
    private static readonly string[] KnownViolations =
    [
        "Devices/Interfaces/REST/DevicesEndpoints.cs -> Analytics",
        "IAM/Application/Internal/CommandServices/RegisterUserWorkflow.cs -> Analytics",
        "IAM/Application/Internal/CommandServices/RegisterUserWorkflow.cs -> Devices",
        "IAM/Application/Internal/CommandServices/RegisterUserWorkflow.cs -> Publishing",
        "Publishing/Application/Internal/CommandServices/ProjectCommandService.cs -> Analytics",
        "Publishing/Application/Internal/CommandServices/ProjectCommandService.cs -> Devices",
        "Publishing/Application/Internal/CommandServices/UnitCommandService.cs -> Analytics",
    ];

    [Fact]
    public void Business_modules_gain_no_new_references_to_other_business_modules()
    {
        var added = FindCrossModuleReferences().Except(KnownViolations, StringComparer.Ordinal).ToArray();

        Assert.True(
            added.Length == 0,
            "New cross-module reference(s) found. Business modules must not depend on each other's " +
            "namespaces; go through Shared or an explicit port instead. If the dependency is " +
            "intentional and agreed, add it to KnownViolations.\n" + string.Join("\n", added));
    }

    [Fact]
    public void Known_cross_module_references_still_exist_so_the_list_only_shrinks()
    {
        var stale = KnownViolations.Except(FindCrossModuleReferences(), StringComparer.Ordinal).ToArray();

        Assert.True(
            stale.Length == 0,
            "These references no longer exist. Good: remove them from KnownViolations so " +
            "they cannot come back unnoticed.\n" + string.Join("\n", stale));
    }

    private static SortedSet<string> FindCrossModuleReferences()
    {
        var apiRoot = ApiRoot();
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var module in BusinessModules)
        {
            var moduleDirectory = Path.Combine(apiRoot, module);
            Assert.True(Directory.Exists(moduleDirectory), $"Module folder not found: {moduleDirectory}");

            foreach (var file in Directory.GetFiles(moduleDirectory, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(apiRoot, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;

                foreach (var line in File.ReadLines(file))
                {
                    var code = line.TrimStart();
                    if (code.StartsWith("//", StringComparison.Ordinal)) continue;

                    foreach (Match match in ApiNamespace().Matches(line))
                    {
                        var target = match.Groups["module"].Value;
                        if (target != module) found.Add($"{relative} -> {target}");
                    }
                }
            }
        }

        return found;
    }

    private static string ApiRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "backend", "src", "IoBuild.Api");
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate backend/src/IoBuild.Api from " + AppContext.BaseDirectory);
    }

    [GeneratedRegex(@"\bIoBuild\.Api\.(?<module>IAM|Devices|Profiles|Publishing|Subscriptions|Analytics)\b")]
    private static partial Regex ApiNamespace();
}
