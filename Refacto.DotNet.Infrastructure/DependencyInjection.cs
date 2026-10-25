using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Infrastructure.Persistence;
using Refacto.DotNet.Infrastructure.Services;

namespace Refacto.DotNet.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            _ = services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("InMemoryDb"));
            _ = services.AddScoped<IOrderRepository, EfOrderRepository>();
            _ = services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

            return services;
        }
    }
}
