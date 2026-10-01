namespace IoBuild.Api.Publishing.Domain.Services.Queries;

public record GetAllClientsQuery;
public record GetClientByIdQuery(int Id);
public record GetClientsByBuilderIdQuery(int BuilderId, int? ProjectId = null);
public record GetClientsByProjectIdQuery(int ProjectId);
