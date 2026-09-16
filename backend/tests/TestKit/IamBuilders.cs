using IoBuild.Api.IAM.Domain.Model.Commands;

namespace IoBuild.TestKit;

// Convergent Testing: builders keep journey tests readable and isolated.
// No shared state, no dependency on preexisting rows.
public static class IamBuilders
{
    public static string UniqueEmail(string prefix = "ct") =>
        $"{prefix}.{Guid.NewGuid():N}@example.test";

    public static RegisterUser RegisterRequest(
        string? email = null,
        string password = "secret123",
        string role = "Owner") =>
        new(email ?? UniqueEmail(), password, role);
}
