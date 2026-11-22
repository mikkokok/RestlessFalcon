using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RestlessFalcon.Endpoints;

public sealed class StateAuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.RelativePath?.StartsWith("api/state/") != true)
            return;
        operation.Parameters ??= new List<IOpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Auth-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Existing application authentication key. Required for every state operation.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
