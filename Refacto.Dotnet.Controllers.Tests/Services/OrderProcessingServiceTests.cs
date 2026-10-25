using Moq;
using Refacto.DotNet.Application.Contracts;
using Refacto.DotNet.Application.Services;
using Refacto.DotNet.Domain.Entities;

namespace Refacto.DotNet.Controllers.Tests.Services
{
    public class OrderProcessingServiceTests
    {
        private static readonly DateTime CurrentTime = new(2025, 5, 10, 14, 30, 0);

        [Fact]
        public async Task ProcessesAllProductsSavesOnceThenDispatchesNotifications()
        {
            List<string> events = new();
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product firstProduct = new() { Name = "First" };
            Product secondProduct = new() { Name = "Second" };
            Order order = new() { Id = 12, Items = new[] { firstProduct, secondProduct } };

            Mock<IOrderRepository> orders = new();
            _ = orders.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Callback(() => events.Add("save"))
                .Returns(Task.CompletedTask);
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(firstProduct, currentDate))
                .Callback(() => events.Add("process:First"))
                .Returns(new ProductNotification(ProductNotificationType.Delay, "First", 4));
            _ = rules.Setup(service => service.ProcessProduct(secondProduct, currentDate))
                .Callback(() => events.Add("process:Second"))
                .Returns(new ProductNotification(ProductNotificationType.OutOfStock, "Second"));
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            _ = notifications.Setup(service => service.SendDelayNotification(4, "First"))
                .Callback(() => events.Add("notify:First"));
            _ = notifications.Setup(service => service.SendOutOfStockNotification("Second"))
                .Callback(() => events.Add("notify:Second"));

            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);
            await processor.ProcessOrderAsync(order, CancellationToken.None);

            Assert.Equal(
                new[] { "process:First", "process:Second", "save", "notify:First", "notify:Second" },
                events);
            orders.Verify(repository => repository.SaveChangesAsync(CancellationToken.None), Times.Once);
            clock.VerifyGet(provider => provider.Now, Times.Once);
        }

        [Fact]
        public async Task DoesNotSaveOrNotifyWhenProductProcessingFails()
        {
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product firstProduct = new() { Name = "First" };
            Product failingProduct = new() { Name = "Invalid" };
            Order order = new() { Id = 12, Items = new[] { firstProduct, failingProduct } };

            Mock<IOrderRepository> orders = new();
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(firstProduct, currentDate))
                .Returns(new ProductNotification(ProductNotificationType.Delay, "First", 4));
            _ = rules.Setup(service => service.ProcessProduct(failingProduct, currentDate))
                .Throws<InvalidOperationException>();
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessOrderAsync(order, CancellationToken.None));

            orders.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
            notifications.Verify(
                service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
            notifications.Verify(service => service.SendOutOfStockNotification(It.IsAny<string>()), Times.Never);
            notifications.Verify(
                service => service.SendExpirationNotification(It.IsAny<string>(), It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task DoesNotSaveWhenCancellationIsRequestedDuringProductProcessing()
        {
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product product = new() { Name = "First" };
            Order order = new() { Id = 12, Items = new[] { product } };
            using CancellationTokenSource cancellationSource = new();
            Mock<IOrderRepository> orders = new();
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(product, currentDate))
                .Callback(cancellationSource.Cancel)
                .Returns((ProductNotification?)null);
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => processor.ProcessOrderAsync(order, cancellationSource.Token));

            orders.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
            notifications.Verify(
                service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task DoesNotSendNotificationsWhenSavingFails()
        {
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product product = new() { Name = "First" };
            Order order = new() { Id = 12, Items = new[] { product } };
            Mock<IOrderRepository> orders = new();
            _ = orders.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException());
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(product, currentDate))
                .Returns(new ProductNotification(ProductNotificationType.Delay, "First", 4));
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessOrderAsync(order, CancellationToken.None));

            orders.Verify(repository => repository.SaveChangesAsync(CancellationToken.None), Times.Once);
            notifications.Verify(
                service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SurfacesNotificationFailureAfterSavingOrder()
        {
            List<string> events = new();
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product product = new() { Name = "First" };
            Order order = new() { Id = 12, Items = new[] { product } };
            Mock<IOrderRepository> orders = new();
            _ = orders.Setup(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask)
                .Callback(() => events.Add("save"));
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(product, currentDate))
                .Returns(new ProductNotification(ProductNotificationType.Delay, "First", 4));
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            _ = notifications.Setup(service => service.SendDelayNotification(4, "First"))
                .Callback(() =>
                {
                    events.Add("notify");
                    throw new InvalidOperationException("Notification failed.");
                });
            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);

            InvalidOperationException exception =
                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => processor.ProcessOrderAsync(order, CancellationToken.None));

            Assert.Equal("Notification failed.", exception.Message);
            Assert.Equal(new[] { "save", "notify" }, events);
            orders.Verify(repository => repository.SaveChangesAsync(CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task DoesNotSaveWhenNotificationPayloadIsInvalid()
        {
            DateOnly currentDate = DateOnly.FromDateTime(CurrentTime);
            Product product = new() { Name = null };
            Order order = new() { Id = 12, Items = new[] { product } };
            Mock<IOrderRepository> orders = new();
            Mock<IProductRulesService> rules = new();
            _ = rules.Setup(service => service.ProcessProduct(product, currentDate))
                .Returns(new ProductNotification(ProductNotificationType.Delay, null, 4));
            Mock<IDateTimeProvider> clock = new();
            _ = clock.SetupGet(provider => provider.Now).Returns(CurrentTime);
            Mock<INotificationService> notifications = new();
            OrderProcessingService processor = new(orders.Object, rules.Object, clock.Object, notifications.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => processor.ProcessOrderAsync(order, CancellationToken.None));

            orders.Verify(
                repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()),
                Times.Never);
            notifications.Verify(
                service => service.SendDelayNotification(It.IsAny<int>(), It.IsAny<string>()),
                Times.Never);
        }
    }
}
