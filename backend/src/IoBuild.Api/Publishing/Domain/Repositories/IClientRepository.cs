using IoBuild.Api.Persistence;

namespace IoBuild.Api.Publishing.Domain.Repositories;

public interface IClientRepository
{
    Task<Client?> FindByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<Client>> FindByBuilderIdAsync(int builderId, int? projectId = null, CancellationToken ct = default);
    Task<IEnumerable<Client>> FindByProjectIdAsync(int projectId, CancellationToken ct = default);
    Task<IEnumerable<Client>> ListAllAsync(CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(Client client, CancellationToken ct = default);
}
