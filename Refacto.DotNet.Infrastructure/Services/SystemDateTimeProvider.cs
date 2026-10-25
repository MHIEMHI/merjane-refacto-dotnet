using Refacto.DotNet.Application.Contracts;

namespace Refacto.DotNet.Infrastructure.Services
{
    public sealed class SystemDateTimeProvider : IDateTimeProvider
    {
        public DateTime Now => DateTime.Now;
    }
}
