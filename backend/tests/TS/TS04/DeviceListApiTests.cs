using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IoBuild.Api.Devices.Domain.Model.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IoBuild.Api.Persistence;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.TechnicalStories.TS04;

[Trait("TechnicalStory", "TS04")]
public sealed class DeviceListApiTests
{
    [Fact]
    public async Task List_filters_by_project_and_unit_and_returns_current_device_resource_fields()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7401;
        var token = TechnicalStoryApiTestSupport.Token(builderId);
        var projectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, token, builderId, "Device Garden");

        await DefineProjectStructureAsync(client, token, projectId, unitsPerFloor: 1);

        int unitId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            unitId = await db.Units.Where(unit => unit.ProjectId == projectId).Select(unit => unit.Id).SingleAsync();
        }

        using var projectResponse = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices?projectId={projectId}", token);
        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        var projectDevices = await projectResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(projectDevices.EnumerateArray());
        var unitDevices = projectDevices.EnumerateArray()
            .Where(device => device.GetProperty("unitId").ValueKind == JsonValueKind.Number
                && device.GetProperty("unitId").GetInt32() == unitId)
            .ToList();
        Assert.Equal(2, unitDevices.Count);
        var device = unitDevices[0];
        foreach (var property in new[] { "id", "name", "type", "location", "macAddress", "projectId", "status", "unitId" })
        {
            Assert.True(device.TryGetProperty(property, out _), $"Device response is missing '{property}'.");
        }
        Assert.False(device.TryGetProperty("realTimeStatus", out _));

        using var unitResponse = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices?unitId={unitId}", token);
        Assert.Equal(HttpStatusCode.OK, unitResponse.StatusCode);
        var filteredDevices = await unitResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, filteredDevices.GetArrayLength());
        Assert.All(filteredDevices.EnumerateArray(), item => Assert.Equal(unitId, item.GetProperty("unitId").GetInt32()));
    }

    [Fact]
    public async Task List_requires_authentication()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/devices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Builder_list_and_read_routes_are_scoped_to_owned_projects()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7403;
        const int otherBuilderId = 7404;
        var builderToken = TechnicalStoryApiTestSupport.Token(builderId);
        var otherBuilderToken = TechnicalStoryApiTestSupport.Token(otherBuilderId);
        var ownProjectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, builderToken, builderId, "Builder Owned Garden");
        var otherProjectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, otherBuilderToken, otherBuilderId, "Other Builder Garden");
        await DefineProjectStructureAsync(client, builderToken, ownProjectId, unitsPerFloor: 1);
        await DefineProjectStructureAsync(client, otherBuilderToken, otherProjectId, unitsPerFloor: 1);

        using var ownListResponse = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/devices", builderToken);
        Assert.Equal(HttpStatusCode.OK, ownListResponse.StatusCode);
        var ownDevices = await ownListResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(ownDevices.EnumerateArray());
        Assert.All(ownDevices.EnumerateArray(), item => Assert.Equal(ownProjectId, item.GetProperty("projectId").GetInt32()));
        var ownDeviceId = ownDevices[0].GetProperty("id").GetInt32();

        using var ownDevice = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownDeviceId}", builderToken);
        using var ownStatus = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownDeviceId}/status", builderToken);
        using var ownEnergy = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownDeviceId}/energy", builderToken);
        Assert.Equal(HttpStatusCode.OK, ownDevice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownEnergy.StatusCode);

        using var otherListResponse = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices?projectId={otherProjectId}", otherBuilderToken);
        var otherDevices = await otherListResponse.Content.ReadFromJsonAsync<JsonElement>();
        var foreignDeviceId = otherDevices[0].GetProperty("id").GetInt32();

        using var foreignDevice = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{foreignDeviceId}", builderToken);
        using var foreignStatus = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{foreignDeviceId}/status", builderToken);
        using var foreignEnergy = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{foreignDeviceId}/energy", builderToken);
        Assert.Equal(HttpStatusCode.NotFound, foreignDevice.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignEnergy.StatusCode);
    }

    [Fact]
    public async Task Owner_list_and_read_routes_are_scoped_to_assigned_units()
    {
        await using var factory = TechnicalStoryApiTestSupport.CreateFactory();
        using var client = factory.CreateClient();
        const int builderId = 7405;
        const int ownerId = 7406;
        var builderToken = TechnicalStoryApiTestSupport.Token(builderId);
        var ownerToken = TechnicalStoryApiTestSupport.Token(ownerId, "Owner");
        var projectId = await TechnicalStoryApiTestSupport.CreateProjectAsync(client, builderToken, builderId, "Owner Device Garden");
        await DefineProjectStructureAsync(client, builderToken, projectId, unitsPerFloor: 2);

        int ownedUnitId;
        int unownedUnitId;
        int ownedDeviceId;
        int unownedDeviceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IoBuildDbContext>();
            var unitIds = await db.Units.Where(unit => unit.ProjectId == projectId).OrderBy(unit => unit.Id).Select(unit => unit.Id).ToListAsync();
            ownedUnitId = unitIds[0];
            unownedUnitId = unitIds[1];
            db.UnitOwnerProjections.Add(new UnitOwnerProjection { UnitId = ownedUnitId, OwnerUserId = ownerId });
            await db.SaveChangesAsync();
            ownedDeviceId = await db.Devices.Where(device => device.UnitId == ownedUnitId).Select(device => device.Id).OrderBy(id => id).FirstAsync();
            unownedDeviceId = await db.Devices.Where(device => device.UnitId == unownedUnitId).Select(device => device.Id).OrderBy(id => id).FirstAsync();
        }

        using var ownerListResponse = await SendAuthorizedAsync(client, HttpMethod.Get, "/api/v1/devices", ownerToken);
        Assert.Equal(HttpStatusCode.OK, ownerListResponse.StatusCode);
        var ownerDevices = await ownerListResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, ownerDevices.GetArrayLength());
        Assert.All(ownerDevices.EnumerateArray(), item => Assert.Equal(ownedUnitId, item.GetProperty("unitId").GetInt32()));

        using var ownedDevice = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownedDeviceId}", ownerToken);
        using var ownedStatus = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownedDeviceId}/status", ownerToken);
        using var ownedEnergy = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{ownedDeviceId}/energy", ownerToken);
        using var unownedDevice = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{unownedDeviceId}", ownerToken);
        using var unownedStatus = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{unownedDeviceId}/status", ownerToken);
        using var unownedEnergy = await SendAuthorizedAsync(client, HttpMethod.Get, $"/api/v1/devices/{unownedDeviceId}/energy", ownerToken);
        Assert.Equal(HttpStatusCode.OK, ownedDevice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownedStatus.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownedEnergy.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unownedDevice.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unownedStatus.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unownedEnergy.StatusCode);
    }

    private static async Task DefineProjectStructureAsync(HttpClient client, string token, int projectId, int unitsPerFloor)
    {
        using var response = await SendAuthorizedAsync(client, HttpMethod.Post, $"/api/v1/projects/{projectId}/structure", token,
            $"{{\"floors\":1,\"unitsPerFloor\":{unitsPerFloor},\"floorNumbers\":null}}");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
