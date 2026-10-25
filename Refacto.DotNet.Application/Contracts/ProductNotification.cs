namespace Refacto.DotNet.Application.Contracts
{
    public sealed record ProductNotification(
        ProductNotificationType Type,
        string? ProductName,
        int? LeadTime = null,
        DateTime? ExpiryDate = null);
}
