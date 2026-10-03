using IoBuild.Api.Publishing.Application.Internal.CommandServices;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using Microsoft.EntityFrameworkCore;
using static IoBuild.Modules.Tests.TestSupport.CoreBusinessTestSupport;

namespace IoBuild.Modules.Tests.Publishing.Structure.Application;

[Trait("Context", "Publishing")]
[Trait("Capability", "Structure")]
[Trait("Layer", "Application")]
[Trait("Dependency", "InMemory")]
public sealed class ProjectStructureWorkflowTests
{
    [Fact]
    [Trait("Category", "DDD")]
    public async Task DefineProjectStructureAsync_provisions_units_and_devices_atomically()
    {
        await using var db = CreateDb();
        var project = new Project { Id = 1, Name = "Grand Horizon", Description = "Residential Tower", Location = "Downtown", TotalUnits = 10, BuilderId = 5 };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var service = new ProjectCommandService(db);
        await service.DefineProjectStructureAsync(1, floors: 2, unitsPerFloor: 3, floorNumbers: null);

        var updatedProject = await db.Projects.FindAsync(1);
        Assert.NotNull(updatedProject);
        Assert.True(updatedProject!.StructureDefined);

        // 2 floors * 3 units = 6 units
        var units = await db.Units.Where(u => u.ProjectId == 1).OrderBy(u => u.UnitNumber).ToListAsync();
        Assert.Equal(6, units.Count);

        // 2 floors * 3 floor devices (SmartMeter, WaterSensor, SmokeDetector) = 6 floor devices
        var floorDevices = await db.Devices.Where(d => d.ProjectId == 1 && d.Source == "FloorProvisioned").ToListAsync();
        Assert.Equal(6, floorDevices.Count);

        // 6 units * 2 unit devices (AirConditioner, SmartLight) = 12 unit devices
        var unitDevices = await db.Devices.Where(d => d.ProjectId == 1 && d.Source == "UnitProvisioned").ToListAsync();
        Assert.Equal(12, unitDevices.Count);
    }
}
