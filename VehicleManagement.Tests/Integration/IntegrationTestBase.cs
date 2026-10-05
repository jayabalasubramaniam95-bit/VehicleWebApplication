using Microsoft.Extensions.DependencyInjection;
namespace VehicleManagement.Tests.Integration;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected VehicleApiFactory Factory { get; private set; } = null!;
    protected IServiceScope Scope { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        Factory = new VehicleApiFactory();
        await TestDatabase.InitializeAsync(Factory.Services);
        Scope = Factory.Services.CreateScope();
    }
    public async Task DisposeAsync()
    {
        Scope.Dispose();
        await Factory.DisposeAsync();
    }
}