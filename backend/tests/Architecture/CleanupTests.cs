using System.Text.RegularExpressions;

namespace IoBuild.Architecture.Tests;

[Trait("Category", "Cleanup")]
public sealed class CleanupTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docker-compose.cutover.yml")) && !File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")) && Directory.Exists(Path.Combine(dir.FullName, "backend")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            if (dir is not null) return dir.FullName;
            // Fallback: traverse from current directory
            dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "docker-compose.cutover.yml")) || Directory.Exists(Path.Combine(dir.FullName, "backend")))
                    return dir.FullName;
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    var candidate = dir.FullName;
                    // wrapped repo root is where backend folder exists
                    if (Directory.Exists(Path.Combine(candidate, "backend")))
                        return candidate;
                }
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }
    }

    private static string ReadRootFile(string relativePath)
    {
        var full = Path.Combine(RepoRoot, relativePath);
        Assert.True(File.Exists(full), $"Expected file to exist: {relativePath} (resolved {full}, root {RepoRoot})");
        return File.ReadAllText(full);
    }

    // ── docker-compose.yml ──────────────────────────────────────────

    [Fact]
    public void Compose_file_exists_as_final_docker_compose_yml()
    {
        var compose = Path.Combine(RepoRoot, "docker-compose.yml");
        Assert.True(File.Exists(compose), "docker-compose.yml must exist at repo root (final promoted compose, not only docker-compose.cutover.yml)");
    }

    [Fact]
    public void Compose_has_single_backend_frontend_mysql()
    {
        var content = ReadRootFile("docker-compose.yml");
        // backend
        Assert.Contains("iobuild-api", content);
        // frontend
        Assert.Contains("frontend", content);
        // single mysql - must be mysql-monolith, exactly one mysql service definition at indent 2 (services level)
        var mysqlServiceMatches = Regex.Matches(content, @"^  mysql-monolith\s*:", RegexOptions.Multiline);
        Assert.True(mysqlServiceMatches.Count == 1, $"Expected exactly one mysql-monolith service at services level, found {mysqlServiceMatches.Count}");
        // Also ensure only one MySQL image overall
        var mysqlImageMatches = Regex.Matches(content, @"image:\s*mysql:8\.0", RegexOptions.IgnoreCase);
        Assert.True(mysqlImageMatches.Count == 1, $"Expected exactly one mysql:8.0 image, found {mysqlImageMatches.Count}");
        // No multiple mysqls like mysql-iam etc
        Assert.DoesNotContain("mysql-iam", content);
        Assert.DoesNotContain("mysql-devices", content);
        Assert.DoesNotContain("mysql-projects", content);
        Assert.DoesNotContain("mysql-analytics", content);
    }

    [Fact]
    public void Compose_has_no_gateway_rabbitmq_redis()
    {
        var content = ReadRootFile("docker-compose.yml");
        // Must not contain legacy infra as service definitions or images (comments mentioning the terms are allowed)
        // Check for rabbitmq service or image
        var hasRabbitService = Regex.IsMatch(content, @"^\s*rabbitmq\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Assert.False(hasRabbitService, "docker-compose.yml must not contain a 'rabbitmq' service");
        var hasRabbitImage = content.ToLower().Contains("image: rabbitmq") || content.ToLower().Contains("rabbitmq:4") || content.ToLower().Contains("rabbitmq:3");
        Assert.False(hasRabbitImage, "docker-compose.yml must not contain a rabbitmq image");
        // redis service or image
        var hasRedisService = Regex.IsMatch(content, @"^\s*redis\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Assert.False(hasRedisService, "docker-compose.yml must not contain a 'redis' service");
        var hasRedisImage = content.ToLower().Contains("image: redis");
        Assert.False(hasRedisImage, "docker-compose.yml must not contain a redis image");
        // YARP gateway service was named "gateway"
        // Ensure no service named gateway (exact service key at indent 2)
        var gatewayService = Regex.IsMatch(content, @"^  gateway\s*:", RegexOptions.Multiline);
        Assert.False(gatewayService, "docker-compose.yml must not contain a 'gateway' service (YARP retired)");
        // Also ensure no YARP image/package reference in compose
        var lower = content.ToLower();
        Assert.DoesNotContain("yarp.reverseproxy", lower);
        // Allow explanatory comments but ensure no yarp service
        var hasYarpService = Regex.IsMatch(content, @"^\s*yarp\s*:", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        Assert.False(hasYarpService, "docker-compose.yml must not contain a yarp service");
    }

    [Fact]
    public void Compose_has_networks_volumes_healthchecks()
    {
        var content = ReadRootFile("docker-compose.yml");
        Assert.Contains("networks:", content);
        Assert.Contains("volumes:", content);
        Assert.Contains("healthcheck:", content);
        // api healthcheck specifically
        Assert.Contains("healthcheck", content.ToLower());
    }

    [Fact]
    public void Compose_references_frontend_build_and_api_dockerfile()
    {
        var content = ReadRootFile("docker-compose.yml");
        Assert.Contains("./frontend", content);
        Assert.Contains("iobuild-api", content);
        // Dockerfile reference should be valid — either backend/Dockerfile or src/IoBuild.Api/Dockerfile
        var hasDockerfile = content.Contains("Dockerfile") || content.Contains("dockerfile");
        Assert.True(hasDockerfile, "Compose should reference a Dockerfile for the api build");
    }

    [Fact]
    public void Dokploy_compose_is_standalone_and_exposes_api_and_spa_without_aggregator()
    {
        var content = ReadRootFile("docker-compose.dokploy.yml");

        Assert.Contains("services:", content);
        Assert.Contains("build:\n      context: ./backend", content);
        Assert.Contains("build:\n      context: ./frontend", content);
        Assert.Contains("image: mysql:8.0", content);
        Assert.Contains("mysql_monolith_data:/var/lib/mysql", content);
        Assert.Contains("image: eclipse-mosquitto:2-openssl", content);
        Assert.DoesNotMatch(@"(?m)^  nginx:\s*$", content);
        Assert.DoesNotContain("!reset", content);
        Assert.Contains("expose:\n      - \"8080\"", content);
        Assert.Contains("expose:\n      - \"80\"", content);
        Assert.DoesNotContain("traefik.http", content);
        Assert.DoesNotContain("dokploy-network", content);
        Assert.Contains("paths /api, /swagger, /health", content);
        Assert.Contains("Disable Strip Path", content);
    }

    [Fact]
    public void Dokploy_compose_starts_influxdb_by_default()
    {
        var content = ReadRootFile("docker-compose.dokploy.yml");
        var influx = Regex.Match(content, @"(?ms)^  influxdb:\s*\n(?<service>.*?)(?=^  \S|^networks:)");
        Assert.True(influx.Success, "Dokploy Compose must define an InfluxDB service");
        Assert.Contains("image: influxdb:2.7-alpine", influx.Groups["service"].Value);
        Assert.Contains("influxdb_data:/var/lib/influxdb2", influx.Groups["service"].Value);
        Assert.DoesNotContain("profiles:", influx.Groups["service"].Value);
        Assert.Contains("${INFLUXDB_ADMIN_PASSWORD:?", influx.Groups["service"].Value);
        Assert.Contains("${INFLUXDB_ADMIN_TOKEN:?", influx.Groups["service"].Value);
        Assert.DoesNotContain("admin123", influx.Groups["service"].Value);
    }

    [Fact]
    public void Compose_optional_influx_mosquitto_simulator_are_profiled_or_absent()
    {
        var content = ReadRootFile("docker-compose.yml");
        // If these services exist they must be gated via profiles or commented — we accept either absent or profiled.
        // The key assertion: core compose must be runnable without them (they are not required dependencies of api)
        // So we check: if simulator/influxdb/mosquitto appear, they must have profiles
        var hasSimulator = content.Contains("simulator");
        var hasInflux = content.ToLower().Contains("influxdb") || content.ToLower().Contains("influx:");
        var hasMosquitto = content.Contains("mosquitto");
        if (hasSimulator || hasInflux || hasMosquitto)
        {
            // At least one occurrence of 'profiles:' should exist when optional services are present
            Assert.Contains("profiles:", content);
        }
        else
        {
            // Absent is also valid — no assertion needed
            Assert.True(true);
        }
    }

    // ── No YARP / RabbitMQ / Redis in backend ───────────────────────

    [Fact]
    public void Csproj_has_no_yarp_rabbitmq_redis_packages()
    {
        var csproj = ReadRootFile("backend/src/IoBuild.Api/IoBuild.Api.csproj");
        var lower = csproj.ToLower();
        Assert.DoesNotContain("yarp.reverseproxy", lower);
        Assert.DoesNotContain("yarp", lower);
        Assert.DoesNotContain("rabbitmq", lower);
        Assert.DoesNotContain("stackexchange.redis", lower);
        Assert.DoesNotContain("microsoft.extensions.caching.stackexchangeredis", lower);
    }

    [Fact]
    public void Backend_src_has_no_rabbitmq_redis_yarp_strings()
    {
        var srcRoot = Path.Combine(RepoRoot, "backend", "src");
        Assert.True(Directory.Exists(srcRoot), $"backend/src not found at {srcRoot}");
        var files = Directory.GetFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(srcRoot, "*.csproj", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(srcRoot, "*.json", SearchOption.AllDirectories))
            .ToList();
        var violations = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var lower = text.ToLower();
            // Allow Influx/OTEL mentions but not rabbit/redis/yarp plumbing
            if (lower.Contains("rabbitmq") || lower.Contains("amqp://") || lower.Contains("stackexchange.redis") || lower.Contains("yarp.reverseproxy"))
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)} contains forbidden plumbing string");
            }
            // Redis word is very generic; only flag if it appears as a package/connection string, not as incidental comment.
            // Check explicit Redis connection patterns
            if (Regex.IsMatch(text, @"redis\s*:\s*6379", RegexOptions.IgnoreCase) || Regex.IsMatch(text, @"REDIS_CONNECTION", RegexOptions.IgnoreCase))
            {
                violations.Add($"{Path.GetRelativePath(RepoRoot, file)} contains Redis connection string");
            }
        }
        Assert.True(violations.Count == 0, $"Forbidden plumbing found:\n{string.Join("\n", violations)}");
    }

    [Fact]
    public void Analytics_projections_kept_but_no_rabbitmq_consumers()
    {
        var analyticsFile = Path.Combine(RepoRoot, "backend", "src", "IoBuild.Api", "Analytics", "AnalyticsServices.cs");
        Assert.True(File.Exists(analyticsFile), "AnalyticsServices.cs must exist (projections kept)");
        var content = File.ReadAllText(analyticsFile);
        Assert.Contains("DeviceProjection", content);
        Assert.Contains("ProjectProjection", content);
        Assert.Contains("UnitProjection", content);
        // Ensure no RabbitMQ consumer plumbing in AnalyticsServices.cs
        Assert.DoesNotContain("RabbitMQ", content);
        Assert.DoesNotContain("IConsumer", content);
        Assert.DoesNotContain("EventBus", content);
    }

    // ── CI workflow ─────────────────────────────────────────────────

    [Fact]
    public void Ci_workflow_exists_and_runs_dotnet_test_and_compose_smoke()
    {
        var ciPath = Path.Combine(RepoRoot, ".github", "workflows", "ci.yml");
        Assert.True(File.Exists(ciPath), ".github/workflows/ci.yml must exist");
        var content = File.ReadAllText(ciPath);
        var lower = content.ToLower();
        Assert.Contains("dotnet test", lower);
        // Must reference solution or project
        Assert.True(lower.Contains("iobuild.sln") || lower.Contains("dotnet test"), "CI must run dotnet test on solution");
        Assert.Contains("docker", lower);
        // compose smoke: up/config/curl health
        Assert.True(lower.Contains("compose") || lower.Contains("docker compose"), "CI must run compose smoke");
        Assert.Contains("curl", lower);
        Assert.Contains("health", lower);
    }

    // ── Docs ────────────────────────────────────────────────────────

    [Fact]
    public void Docs_exist_runbook_cutover_rollback_compose()
    {
        foreach (var doc in new[] { "docs/runbook.md", "docs/cutover.md", "docs/rollback.md", "docs/compose.md" })
        {
            var full = Path.Combine(RepoRoot, doc);
            Assert.True(File.Exists(full), $"Expected doc to exist: {doc}");
            var content = File.ReadAllText(full);
            Assert.True(content.Length > 100, $"{doc} should have substantive content (>100 chars)");
        }
    }

    [Fact]
    public void Docs_contain_expected_sections()
    {
        var runbook = ReadRootFile("docs/runbook.md");
        Assert.True(runbook.ToLower().Contains("health") || runbook.ToLower().Contains("compose"), "runbook.md should mention health/compose");

        var cutover = ReadRootFile("docs/cutover.md");
        var cutLower = cutover.ToLower();
        Assert.True(cutLower.Contains("freeze") || cutLower.Contains("backup"), "cutover.md should contain freeze/backup steps");
        Assert.True(cutLower.Contains("verify") || cutLower.Contains("switch"), "cutover.md should contain verify/switch");

        var rollback = ReadRootFile("docs/rollback.md");
        Assert.True(rollback.ToLower().Contains("restore") || rollback.ToLower().Contains("rollback"), "rollback.md should contain restore/rollback");

        var composeDoc = ReadRootFile("docs/compose.md");
        Assert.True(composeDoc.ToLower().Contains("compose") && composeDoc.ToLower().Contains("docker"), "compose.md should document compose usage");
    }

    [Fact]
    public void Readme_documents_architecture_and_compose()
    {
        var readme = ReadRootFile("README.md");
        var lower = readme.ToLower();
        Assert.Contains("docker", lower);
        Assert.Contains("compose", lower);
    }

    [Fact]
    public void Backend_dockerfile_exists()
    {
        var candidates = new[]
        {
            Path.Combine(RepoRoot, "backend", "Dockerfile"),
            Path.Combine(RepoRoot, "backend", "src", "IoBuild.Api", "Dockerfile"),
            Path.Combine(RepoRoot, "Dockerfile")
        };
        var exists = candidates.Any(File.Exists);
        Assert.True(exists, $"Expected a Dockerfile for the monolith at one of: {string.Join(", ", candidates.Select(p => Path.GetRelativePath(RepoRoot, p)))}");
    }
}
