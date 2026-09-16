namespace IoBuild.TestKit;

// Convergent Testing: stable taxonomy for journey reports across runners.
// Use with [Trait(Traits.Flow, "IAM.LOGIN")] etc. so reports aggregate one
// scenario across xUnit, Vitest and Playwright implementations.
public static class Traits
{
    public const string Flow = "Flow";
    public const string Layer = "Layer";
    public const string Risk = "Risk";
    public const string Dependency = "Dependency";
}

public static class Layers
{
    public const string Domain = "Domain";
    public const string Application = "Application";
    public const string Component = "Component";
    public const string FrontendIntegration = "FrontendIntegration";
    public const string Api = "Api";
    public const string Contract = "Contract";
    public const string Persistence = "Persistence";
    public const string System = "System";
}

public static class Risks
{
    public const string A = "A";
    public const string B = "B";
    public const string C = "C";
    public const string D = "D";
}

public static class Dependencies
{
    public const string None = "None";
    public const string InMemory = "InMemory";
    public const string MySql = "MySql";
}
