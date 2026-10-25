namespace Refacto.DotNet.Application.Contracts
{
    public interface INotificationService
    {
        void SendDelayNotification(int leadTime, string productName);

        void SendOutOfStockNotification(string productName);

        void SendExpirationNotification(string productName, DateTime expiryDate);
    }
}
