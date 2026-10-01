using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IoBuild.Api.Publishing.Infrastructure.Persistence.EFC.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly IoBuildDbContext _dbContext;

    public ClientRepository(IoBuildDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Client?> FindByIdAsync(int id, CancellationToken ct = default)
    {
        return await _dbContext.Clients.FindAsync([id], ct);
    }

    public async Task<IEnumerable<Client>> FindByBuilderIdAsync(int builderId, int? projectId = null, CancellationToken ct = default)
    {
        return await _dbContext.Clients
            .Where(c => c.BuilderId == builderId && (!projectId.HasValue || c.ProjectId == projectId.Value))
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Client>> FindByProjectIdAsync(int projectId, CancellationToken ct = default)
    {
        return await _dbContext.Clients
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Client>> ListAllAsync(CancellationToken ct = default)
    {
        return await _dbContext.Clients.OrderBy(c => c.Id).ToListAsync(ct);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await _dbContext.Clients.AddAsync(client, ct);
    }

    public Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        _dbContext.Clients.Update(client);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Client client, CancellationToken ct = default)
    {
        _dbContext.Clients.Remove(client);
        return Task.CompletedTask;
    }
}
