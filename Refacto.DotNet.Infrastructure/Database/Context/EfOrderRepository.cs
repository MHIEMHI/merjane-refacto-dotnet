using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Infrastructure.Persistence
{
    public sealed class EfOrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;

        public EfOrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Order?> GetWithProductsAsync(long orderId, CancellationToken cancellationToken)
        {
            return _context.Orders
                .Include(order => order.Items)
                .SingleOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            _ = await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
