public sealed partial class IamTierCTests
{
    [Fact]
    [Trait("Flow", "IAM.REGISTRATION")]
    [Trait("Layer", "Persistence")]
    [Trait("Risk", "C")]
    public async Task IAM_REGISTRATION_EMAIL_has_unique_index_intent_in_model()
    {
        await using var db = CreateDb();
        var entity = db.Model.FindEntityType(typeof(IoBuild.Api.IAM.Domain.Model.Aggregates.IamUser));
        Assert.NotNull(entity);
        var uniqueEmailIndex = entity!.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(["Email"]) && i.IsUnique);
        Assert.NotNull(uniqueEmailIndex);
    }
}
