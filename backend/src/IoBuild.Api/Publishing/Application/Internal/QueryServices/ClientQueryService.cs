using IoBuild.Api.Persistence;
using IoBuild.Api.Publishing.Domain.Repositories;
using IoBuild.Api.Publishing.Domain.Services;
using IoBuild.Api.Publishing.Domain.Services.Queries;

namespace IoBuild.Api.Publishing.Application.Internal.QueryServices;

public class ClientQueryService : IClientQueryService
{
    private readonly IClientRepository _clientRepository;

    public ClientQueryService(IClientRepository clientRepository)
    {
        _clientRepository = clientRepository;
    }

    public async Task<IEnumerable<Client>> Handle(GetAllClientsQuery query, CancellationToken ct = default)
    {
        return await _clientRepository.ListAllAsync(ct);
    }

    public async Task<Client?> Handle(GetClientByIdQuery query, CancellationToken ct = default)
    {
        return await _clientRepository.FindByIdAsync(query.Id, ct);
    }

    public async Task<IEnumerable<Client>> Handle(GetClientsByBuilderIdQuery query, CancellationToken ct = default)
    {
        return await _clientRepository.FindByBuilderIdAsync(query.BuilderId, query.ProjectId, ct);
    }

    public async Task<IEnumerable<Client>> Handle(GetClientsByProjectIdQuery query, CancellationToken ct = default)
    {
        return await _clientRepository.FindByProjectIdAsync(query.ProjectId, ct);
    }
}
