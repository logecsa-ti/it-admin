namespace TIAdmin.Tests.Functional;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

/// <summary>
/// Swagger solo se publica en Development, asi que un endpoint que Swashbuckle no sabe describir
/// (p. ej. [FromForm] con IFormFile) rompia /swagger sin que ninguna prueba lo detectara.
/// El frontend genera sus modelos desde este documento.
/// </summary>
public sealed class OpenApiDocumentTests(TIAdminApiFactory factory) : IClassFixture<TIAdminApiFactory>
{
    [Fact]
    public void OpenApiDocument_ShouldGenerateForEveryEndpoint()
    {
        var provider = factory.Services.GetRequiredService<ISwaggerProvider>();

        var document = provider.GetSwagger("v1");

        document.Paths.Should().ContainKey("/api/v1/assets/import");
        document.Paths.Should().ContainKey("/api/v1/documents");
        document.Components!.Schemas.Should().ContainKey("AssetListItemDto");
    }
}
