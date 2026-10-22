using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace NewsApi.Tests;

/// <summary>
/// Groups ASP.NET Core integration tests into a single test collection sharing
/// a single WebApplicationFactory instance to avoid concurrent startup races.
/// </summary>
[CollectionDefinition("NewsApiIntegration")]
public class IntegrationTestCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
}
