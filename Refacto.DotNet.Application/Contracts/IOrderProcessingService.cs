namespace Refacto.DotNet.Application.Contracts
{
    public interface IOrderProcessingService
    {
        Task ProcessOrderAsync(Domain.Entities.Order order, CancellationToken cancellationToken);
    }
}
