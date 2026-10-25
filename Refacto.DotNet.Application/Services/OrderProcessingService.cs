using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Application.Services
{
    public sealed class OrderProcessingService : IOrderProcessingService
    {
        private readonly IOrderRepository _orders;
        private readonly IProductRulesService _productRules;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly INotificationService _notifications;

        public OrderProcessingService(
            IOrderRepository orders,
            IProductRulesService productRules,
            IDateTimeProvider dateTimeProvider,
            INotificationService notifications)
        {
            _orders = orders;
            _productRules = productRules;
            _dateTimeProvider = dateTimeProvider;
            _notifications = notifications;
        }

        public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(order);
            cancellationToken.ThrowIfCancellationRequested();

            DateOnly currentDate = DateOnly.FromDateTime(_dateTimeProvider.Now);
            List<ProductNotification> notifications = new();
            foreach (Product product in order.Items ?? Array.Empty<Product>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ProductNotification? notification = _productRules.ProcessProduct(product, currentDate);
                if (notification is not null)
                {
                    ValidateNotification(notification);
                    notifications.Add(notification);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _orders.SaveChangesAsync(cancellationToken);
            foreach (ProductNotification notification in notifications)
            {
                switch (notification.Type)
                {
                    case ProductNotificationType.Delay:
                        _notifications.SendDelayNotification(
                            notification.LeadTime!.Value,
                            GetProductName(notification));
                        break;
                    case ProductNotificationType.OutOfStock:
                        _notifications.SendOutOfStockNotification(GetProductName(notification));
                        break;
                    case ProductNotificationType.Expired:
                        _notifications.SendExpirationNotification(
                            GetProductName(notification),
                            notification.ExpiryDate!.Value);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unsupported notification type '{notification.Type}'.");
                }
            }
        }

        private static string GetProductName(ProductNotification notification)
        {
            return notification.ProductName
                ?? throw new InvalidOperationException("A product notification must include a product name.");
        }

        private static void ValidateNotification(ProductNotification notification)
        {
            _ = GetProductName(notification);
            if (notification.Type == ProductNotificationType.Delay && !notification.LeadTime.HasValue)
            {
                throw new InvalidOperationException("A delay notification must include a lead time.");
            }

            if (notification.Type == ProductNotificationType.Expired && !notification.ExpiryDate.HasValue)
            {
                throw new InvalidOperationException("An expiration notification must include an expiry date.");
            }

            if (notification.Type is not ProductNotificationType.Delay
                and not ProductNotificationType.OutOfStock
                and not ProductNotificationType.Expired)
            {
                throw new InvalidOperationException(
                    $"Unsupported notification type '{notification.Type}'.");
            }
        }
    }
}
