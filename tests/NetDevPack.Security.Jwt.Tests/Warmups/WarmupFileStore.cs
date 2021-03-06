using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NetDevPack.Security.Jwt.Interfaces;

namespace NetDevPack.Security.Jwt.Tests.Warmups
{
    /// <summary>
    /// 
    /// </summary>
    public class WarmupFileStore : IWarmupTest
    {
        private readonly IJsonWebKeyStore _jsonWebKeyStore;
        public ServiceProvider Services { get; set; }
        public WarmupFileStore()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddMemoryCache();
            serviceCollection.AddJwksManager().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory())));

            Services = serviceCollection.BuildServiceProvider();
            _jsonWebKeyStore = Services.GetRequiredService<IJsonWebKeyStore>();
        }

        public void Clear()
        {
            _jsonWebKeyStore.Clear();
        }
    }
}