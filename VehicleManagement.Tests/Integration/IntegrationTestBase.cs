using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VehicleManagement.Data;

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

    public static async Task InitializeAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await dbContext.Database.EnsureDeletedAsync();
    await dbContext.Database.EnsureCreatedAsync();
}
}