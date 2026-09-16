using IoBuild.Api.Analytics.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Aggregates;
using IoBuild.Api.Devices.Domain.Model.Entities;
using IoBuild.Api.IAM.Domain.Model.Aggregates;
using IoBuild.Api.IAM.Domain.Model.Commands;
using IoBuild.Api.IAM.Infrastructure.Hashing;
using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Model.Aggregates;
using IoBuild.Api.Workflows;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.IAM.Application.Internal.CommandServices;

/// <summary>
/// IAM Application: registration workflow (transactional outbox via IntegrationDispatch).
/// </summary>
public sealed class RegisterUserWorkflow(
    IoBuildDbContext dbContext,
    PasswordHasher passwordHasher,
    IIntegrationDispatchQueue queue,
    WorkflowExecutor workflowExecutor) : IWorkflow<RegisterUser, int>
{
    public Task<int> ExecuteAsync(RegisterUser request, CancellationToken cancellationToken = default) =>
        workflowExecutor.ExecuteAsync(async cancellationToken =>
        {
            // Fail-closed input guard: backend is authoritative (Tier D discovery:
            // InMemory ignores column limits, so oversized input must be rejected here).
            var rawEmail = request.Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawEmail) || rawEmail.Length > 320)
                throw new InvalidOperationException("Invalid registration data.");
            if (string.IsNullOrWhiteSpace(request.Password))
                throw new InvalidOperationException("Invalid registration data.");
            var email = rawEmail.ToLowerInvariant();
            var existing = await dbContext.IamUsers.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
            if (existing is not null) return 0;

            var newUser = new IamUser { Email = email, PasswordHash = passwordHasher.Hash(request.Password), Role = request.Role };
            dbContext.IamUsers.Add(newUser);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Auto-link Units, UnitOwnerProjections, UnitProjections, Devices, and DeviceProjections
            // if any units or client records were assigned to this email by the builder
            var matchingUnits = await dbContext.Units
                .Where(u => u.OwnerEmail != null && u.OwnerEmail.ToLower() == email)
                .ToListAsync(cancellationToken);

            // Also check if any Client record has this email and an assigned unit not yet in matchingUnits
            var clientUnitIds = await dbContext.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == email && c.UnitId != null)
                .Select(c => c.UnitId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (clientUnitIds.Count > 0)
            {
                var existingUnitIds = matchingUnits.Select(u => u.Id).ToHashSet();
                var additionalUnits = await dbContext.Units
                    .Where(u => clientUnitIds.Contains(u.Id) && !existingUnitIds.Contains(u.Id))
                    .ToListAsync(cancellationToken);
                matchingUnits.AddRange(additionalUnits);
            }

            foreach (var unit in matchingUnits)
            {
                unit.OwnerId = newUser.Id;
                unit.OwnerEmail = email;
                unit.Status = "occupied";

                var ownerProj = await dbContext.UnitOwnerProjections
                    .FirstOrDefaultAsync(p => p.UnitId == unit.Id, cancellationToken);
                if (ownerProj is null)
                {
                    dbContext.UnitOwnerProjections.Add(new UnitOwnerProjection
                    {
                        UnitId = unit.Id,
                        OwnerUserId = newUser.Id,
                        UpdatedAt = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    ownerProj.OwnerUserId = newUser.Id;
                    ownerProj.UpdatedAt = DateTimeOffset.UtcNow;
                }

                // Ensure ProjectProjection exists so owner dashboard displays real project name
                if (!await dbContext.ProjectProjections.AnyAsync(p => p.ProjectId == unit.ProjectId, cancellationToken))
                {
                    var proj = await dbContext.Projects.FindAsync([unit.ProjectId], cancellationToken);
                    if (proj is not null)
                    {
                        dbContext.ProjectProjections.Add(new ProjectProjection
                        {
                            ProjectId = proj.Id,
                            BuilderUserId = proj.BuilderId,
                            Name = proj.Name,
                            Status = "OnGoing",
                            LastEventAt = DateTime.UtcNow
                        });
                    }
                }

                var unitProj = await dbContext.UnitProjections
                    .FirstOrDefaultAsync(p => p.UnitId == unit.Id, cancellationToken);
                if (unitProj is null)
                {
                    var proj = await dbContext.Projects.FindAsync([unit.ProjectId], cancellationToken);
                    dbContext.UnitProjections.Add(new UnitProjection
                    {
                        UnitId = unit.Id,
                        ProjectId = unit.ProjectId,
                        BuilderUserId = proj?.BuilderId ?? 0,
                        OwnerUserId = newUser.Id,
                        OwnerEmail = email,
                        Status = "occupied",
                        Floor = unit.Floor,
                        RoomNumber = unit.RoomNumber,
                        LastEventAt = DateTime.UtcNow
                    });
                }
                else
                {
                    unitProj.OwnerUserId = newUser.Id;
                    unitProj.OwnerEmail = email;
                    unitProj.Status = "occupied";
                    unitProj.LastEventAt = DateTime.UtcNow;
                }

                // Link all IoT devices belonging to this unit to the new owner
                var unitDevices = await dbContext.Devices
                    .Where(d => d.UnitId == unit.Id)
                    .ToListAsync(cancellationToken);

                foreach (var device in unitDevices)
                {
                    device.OwnerId = newUser.Id;

                    var devProj = await dbContext.DeviceProjections
                        .FirstOrDefaultAsync(dp => dp.DeviceId == device.Id, cancellationToken);
                    if (devProj is null)
                    {
                        dbContext.DeviceProjections.Add(new DeviceProjection
                        {
                            DeviceId = device.Id,
                            ProjectId = device.ProjectId,
                            UnitId = device.UnitId,
                            DeviceName = device.Name,
                            DeviceType = device.Type,
                            Status = device.Status,
                            OwnerUserId = newUser.Id,
                            LastEventAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        devProj.OwnerUserId = newUser.Id;
                        devProj.Status = device.Status;
                        devProj.LastEventAt = DateTime.UtcNow;
                    }
                }
            }

            // Synchronize matching client records for this email
            var matchingClients = await dbContext.Clients
                .Where(c => !string.IsNullOrEmpty(c.Email) && c.Email.ToLower() == email)
                .ToListAsync(cancellationToken);

            foreach (var client in matchingClients)
            {
                if (!client.UnitId.HasValue && matchingUnits.Count > 0)
                {
                    var primaryUnit = matchingUnits.First();
                    client.UnitId = primaryUnit.Id;
                    client.UnitNumber = primaryUnit.UnitNumber;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await queue.EnqueueAsync(new DispatchRequest("iam", "domain-event", $"iam-user:{email}", 1, $"{{\"email\":\"{email}\",\"role\":\"{request.Role}\"}}", $"iam.user-registered:{email}"), cancellationToken);
            return newUser.Id;
        }, cancellationToken);
}
