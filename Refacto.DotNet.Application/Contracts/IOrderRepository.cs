using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Application.Contracts
{
    public interface IOrderRepository
    {
        Task<Order?> GetWithProductsAsync(long orderId, CancellationToken cancellationToken);

        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
