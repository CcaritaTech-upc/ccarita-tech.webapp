using System.IO;
using Xunit;

namespace IoBuild.Contract.Tests;

public sealed class EdgeTests
{
    private static string FindWrappedRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        // Walk up until we find wrapped/nginx or wrapped/frontend marker
        for (int i = 0; i < 10 && dir != null; i++)
        {
            // Check typical structures
            var candidateNginx = Path.Combine(dir.FullName, "nginx", "nginx.conf");
            // If we are inside wrapped/backend/tests/... then wrapped root is 3-4 levels up
            if (File.Exists(candidateNginx))
            {
                // dir is wrapped
                return dir.FullName;
            }
            // Also check parent contains wrapped subfolder
            var wrappedCandidate = Path.Combine(dir.FullName, "wrapped", "nginx", "nginx.conf");
            if (File.Exists(wrappedCandidate))
            {
                return Path.Combine(dir.FullName, "wrapped");
            }
            // Fallback: absolute known path
            if (File.Exists("/home/arroz/dev_projects/iobuild/wrapped/nginx/nginx.conf"))
            {
                return "/home/arroz/dev_projects/iobuild/wrapped";
            }
            dir = dir.Parent;
        }
        // Final fallback to absolute
        if (Directory.Exists("/home/arroz/dev_projects/iobuild/wrapped"))
            return "/home/arroz/dev_projects/iobuild/wrapped";
        return Directory.GetCurrentDirectory();
    }

    private static string ReadFileOrEmpty(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;

    // ── Nginx ──────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Edge")]
    public void Nginx_ContainsProxyPassToMonolith()
    {
        var root = FindWrappedRoot();
        var nginxPath = Path.Combine(root, "nginx", "nginx.conf");
        Assert.True(File.Exists(nginxPath), $"nginx.conf not found at {nginxPath}");
        var content = File.ReadAllText(nginxPath);
        Assert.Contains("proxy_pass http://iobuild-api:8080", content);
        // Must proxy /api (and ideally /api/v1) to monolith
        Assert.Contains("/api", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Nginx_ContainsForwardedHeaders()
    {
        var root = FindWrappedRoot();
        var nginxPath = Path.Combine(root, "nginx", "nginx.conf");
        var content = ReadFileOrEmpty(nginxPath);
        Assert.Contains("X-Forwarded-For", content);
        Assert.Contains("X-Forwarded-Proto", content);
        Assert.Contains("X-Real-IP", content);
        // Host header must be forwarded
        Assert.Contains("proxy_set_header Host", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Nginx_ContainsHealthProxy()
    {
        var root = FindWrappedRoot();
        var nginxPath = Path.Combine(root, "nginx", "nginx.conf");
        var content = ReadFileOrEmpty(nginxPath);
        Assert.Contains("location /health", content);
        // health should proxy to monolith as well
        Assert.Contains("proxy_pass http://iobuild-api:8080", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Nginx_ContainsSpaFallback()
    {
        var root = FindWrappedRoot();
        var nginxPath = Path.Combine(root, "nginx", "nginx.conf");
        var content = ReadFileOrEmpty(nginxPath);
        // Valid SPA strategies: static try_files fallback or proxy to frontend container.
        var hasStaticFallback = content.Contains("try_files") && content.Contains("/index.html");
        var hasFrontendProxy = content.Contains("location /") && content.Contains("proxy_pass http://frontend");
        Assert.True(hasStaticFallback || hasFrontendProxy,
            "nginx must serve SPA via try_files /index.html or proxy / to frontend container");
    }

    // ── Forwarded host / Cloudflare recovery ───────────────────────────────

    [Fact]
    [Trait("Category", "Edge")]
    public void Program_UsesForwardedHeaders()
    {
        var root = FindWrappedRoot();
        var programPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Program.cs");
        Assert.True(File.Exists(programPath), $"Program.cs not found at {programPath}");
        var content = File.ReadAllText(programPath);
        // Must recover forwarded host/proto from Cloudflare/Nginx
        Assert.True(
            content.Contains("UseForwardedHeaders") || content.Contains("ForwardedHeaders"),
            "Program.cs must invoke UseForwardedHeaders or configure ForwardedHeaders for Cloudflare/Nginx");
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Program_ConfiguresCorsForLocalhost5173()
    {
        var root = FindWrappedRoot();
        var programPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Program.cs");
        var content = ReadFileOrEmpty(programPath);
        Assert.Contains("AddCors", content);
        // Must allow Vite dev server
        Assert.Contains("localhost:5173", content);
    }

    // ── Frontend existence & routing ──────────────────────────────────────

    [Fact]
    [Trait("Category", "Edge")]
    public void Frontend_FilesExist()
    {
        var root = FindWrappedRoot();
        var frontendRoot = Path.Combine(root, "frontend");
        Assert.True(Directory.Exists(frontendRoot), $"frontend directory not found at {frontendRoot}");
        Assert.True(File.Exists(Path.Combine(frontendRoot, "src", "App.vue")), "frontend/src/App.vue missing");
        Assert.True(File.Exists(Path.Combine(frontendRoot, "package.json")), "frontend/package.json missing");
        Assert.True(File.Exists(Path.Combine(frontendRoot, "vite.config.js")), "frontend/vite.config.js missing");
        Assert.True(File.Exists(Path.Combine(frontendRoot, "index.html")), "frontend/index.html missing");
        Assert.True(Directory.Exists(Path.Combine(frontendRoot, "public")), "frontend/public missing");
        Assert.True(Directory.Exists(Path.Combine(frontendRoot, "src")), "frontend/src missing");
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Frontend_RouterHasExpectedRoutes()
    {
        var root = FindWrappedRoot();
        var routerPath = Path.Combine(root, "frontend", "src", "router.js");
        Assert.True(File.Exists(routerPath), $"router.js not found at {routerPath}");
        var content = File.ReadAllText(routerPath);
        Assert.Contains("/iam", content);
        Assert.Contains("/analytics", content);
        Assert.Contains("/devices", content);
        Assert.Contains("/projects", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Frontend_ViteApiUrlIsRelative()
    {
        var root = FindWrappedRoot();
        // .env.production should define relative /api/v1
        var envPath = Path.Combine(root, "frontend", ".env.production");
        Assert.True(File.Exists(envPath), $".env.production not found at {envPath}");
        var content = File.ReadAllText(envPath);
        Assert.Contains("VITE_API_URL=/api/v1", content);
        // Must NOT be absolute to microservice gateway
        Assert.DoesNotContain("http://gateway", content);
        Assert.DoesNotContain("http://localhost:8080", content);
    }

    // ── Health / Jaeger optional ──────────────────────────────────────────

    [Fact]
    [Trait("Category", "Edge")]
    public void Health_PassesWithoutJaeger()
    {
        var root = FindWrappedRoot();
        // Observability must be conditional so app starts healthy when jaeger absent
        var obsPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Observability", "ObservabilityExtensions.cs");
        var programPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Program.cs");
        var obsContent = ReadFileOrEmpty(obsPath);
        var progContent = ReadFileOrEmpty(programPath);
        // At least one of them must guard on OTEL_EXPORTER_OTLP_ENDPOINT
        var combined = obsContent + progContent;
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", combined);
        // Guard must allow start when absent (empty check or TryGetValue)
        Assert.True(
            combined.Contains("IsNullOrEmpty") || combined.Contains("TryGetValue") || combined.Contains("string.IsNullOrWhiteSpace"),
            "Observability must guard OTEL endpoint so app starts healthy when jaeger absent");
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Otlp_ExportConfiguredWhenEndpointSet()
    {
        var root = FindWrappedRoot();
        var obsPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Observability", "ObservabilityExtensions.cs");
        Assert.True(File.Exists(obsPath), $"ObservabilityExtensions.cs not found at {obsPath}");
        var content = File.ReadAllText(obsPath);
        Assert.Contains("AddIoBuildObservability", content);
        Assert.Contains("AddAspNetCoreInstrumentation", content);
        Assert.Contains("RecordException", content);
        Assert.Contains("AddHttpClientInstrumentation", content);
        Assert.Contains("AddOtlpExporter", content);
        Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Program_InvokesObservability()
    {
        var root = FindWrappedRoot();
        var programPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "Program.cs");
        var content = ReadFileOrEmpty(programPath);
        Assert.Contains("AddIoBuildObservability", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void CsProj_ContainsOpenTelemetryPackages()
    {
        var root = FindWrappedRoot();
        var csprojPath = Path.Combine(root, "backend", "src", "IoBuild.Api", "IoBuild.Api.csproj");
        var content = ReadFileOrEmpty(csprojPath);
        Assert.Contains("OpenTelemetry.Extensions.Hosting", content);
        Assert.Contains("OpenTelemetry.Instrumentation.AspNetCore", content);
        Assert.Contains("OpenTelemetry.Instrumentation.Http", content);
        Assert.Contains("OpenTelemetry.Exporter.OpenTelemetryProtocol", content);
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void DockerCompose_ContainsJaegerService()
    {
        var root = FindWrappedRoot();
        var composePath = Path.Combine(root, "docker-compose.cutover.yml");
        Assert.True(File.Exists(composePath), $"docker-compose.cutover.yml not found at {composePath}");
        var content = File.ReadAllText(composePath);
        Assert.Contains("jaeger", content);
        // Jaeger should expose OTLP endpoint
        Assert.True(content.Contains("4317") || content.Contains("OTEL") || content.Contains("jaegertracing"), "jaeger service must expose OTLP port or config");
    }

    [Fact]
    [Trait("Category", "Edge")]
    public void Frontend_BuildArtifactsPresent()
    {
        var root = FindWrappedRoot();
        var frontendRoot = Path.Combine(root, "frontend");
        // Dockerfile for building the SPA (optional location check)
        var dockerfileInFrontend = Path.Combine(frontendRoot, "Dockerfile");
        var dockerfileInRoot = Path.Combine(root, "frontend", "Dockerfile");
        var hasDockerfile = File.Exists(dockerfileInFrontend) || File.Exists(dockerfileInRoot) || File.Exists(Path.Combine(root, "Dockerfile.frontend")) || File.Exists(Path.Combine(frontendRoot, "package.json"));
        Assert.True(hasDockerfile, "frontend build artifact (Dockerfile or package.json) missing");
        // vite.config.js must have build config
        var vitePath = Path.Combine(frontendRoot, "vite.config.js");
        if (File.Exists(vitePath))
        {
            var viteContent = File.ReadAllText(vitePath);
            Assert.Contains("build", viteContent);
        }
    }
}
