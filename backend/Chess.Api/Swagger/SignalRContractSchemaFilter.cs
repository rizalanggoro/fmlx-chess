using Api.DTOs.Events;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Swagger;

// SignalR payloads are not HTTP operations, so register them as schema-only
// components. This keeps the generated frontend event types in sync with the DTOs.
public abstract class SignalRContractSchemaFilter : IDocumentFilter
{
    private static readonly Type[] Contracts =
    [
        typeof(TimerTickDto),
        typeof(GameEndedDto),
        typeof(DrawOfferedDto),
        typeof(DrawDeclinedDto),
        typeof(SnapshotChangedDto),
        typeof(PieceCapturedDto),
        typeof(CheckDeclaredDto),
        typeof(LobbyChangedDto)
    ];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var contract in Contracts)
            context.SchemaGenerator.GenerateSchema(contract, context.SchemaRepository);
    }
}