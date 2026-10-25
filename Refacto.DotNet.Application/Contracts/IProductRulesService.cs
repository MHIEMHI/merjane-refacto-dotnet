using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Application.Contracts
{
    public interface IProductRulesService
    {
        ProductNotification? ProcessProduct(Product product, DateOnly currentDate);
    }
}
