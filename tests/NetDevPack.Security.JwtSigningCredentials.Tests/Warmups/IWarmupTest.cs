using Microsoft.Extensions.DependencyInjection;

namespace NetDevPack.Security.JwtSigningCredentials.Tests.Warmups
{
    public interface IWarmupTest
    {
        ServiceProvider Services { get; set; }
    }
}