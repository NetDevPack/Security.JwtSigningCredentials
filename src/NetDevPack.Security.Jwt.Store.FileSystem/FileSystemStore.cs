using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace NetDevPack.Security.Jwt.Store.FileSystem
{
    public class FileSystemStore : IJsonWebKeyStore
    {
        private readonly IOptions<JwksOptions> _options;
        private readonly IMemoryCache _memoryCache;
        public DirectoryInfo KeysPath { get; }

        public FileSystemStore(DirectoryInfo keysPath, IOptions<JwksOptions> options, IMemoryCache memoryCache)
        {
            _options = options;
            _memoryCache = memoryCache;
            KeysPath = keysPath;
        }

        private string GetCurrentFile(JsonWebKeyType jsonWebKeyType)
        {
            return Path.Combine(KeysPath.FullName, $"{_options.Value.KeyPrefix}current-{jsonWebKeyType}.key");
        }

        public void Save(SecurityKeyWithPrivate securityParamteres)
        {
            if (!KeysPath.Exists)
                KeysPath.Create();

            // Datetime it's just to be easy searchable.
            if (File.Exists(GetCurrentFile(securityParamteres.JwkType)))
                File.Copy(GetCurrentFile(securityParamteres.JwkType), Path.Combine(Path.GetDirectoryName(GetCurrentFile(securityParamteres.JwkType)), $"{_options.Value.KeyPrefix}old-{DateTime.Now:yyyy-MM-dd}-{Guid.NewGuid()}.key"));

            File.WriteAllText(GetCurrentFile(securityParamteres.JwkType), JsonSerializer.Serialize(securityParamteres, new JsonSerializerOptions() { IgnoreNullValues = true }));
            ClearCache();
        }

        public bool NeedsUpdate(JsonWebKeyType jsonWebKeyType)
        {
            return !File.Exists(GetCurrentFile(jsonWebKeyType)) || File.GetCreationTimeUtc(GetCurrentFile(jsonWebKeyType)).AddDays(_options.Value.DaysUntilExpire) < DateTime.UtcNow.Date;
        }

        public void Revoke(SecurityKeyWithPrivate securityKeyWithPrivate)
        {
            securityKeyWithPrivate.Revoke();
            foreach (var fileInfo in KeysPath.GetFiles("*.key"))
            {
                var key = GetKey(fileInfo.FullName);
                if (key.Id != securityKeyWithPrivate.Id) continue;
                File.WriteAllText(fileInfo.FullName, JsonSerializer.Serialize(securityKeyWithPrivate, new JsonSerializerOptions() { IgnoreNullValues = true }));
                break;
            }
            ClearCache();
        }


        public SecurityKeyWithPrivate GetCurrentKey(JsonWebKeyType jwkType)
        {
            if (!_memoryCache.TryGetValue(JwkContants.CurrentJwkCache(jwkType), out SecurityKeyWithPrivate credentials))
            {
                credentials = GetKey(GetCurrentFile(jwkType));
                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(_options.Value.CacheTime);
                if (credentials != null)
                    _memoryCache.Set(JwkContants.CurrentJwkCache(jwkType), credentials, cacheEntryOptions);
            }

            return credentials;
        }

        private SecurityKeyWithPrivate GetKey(string file)
        {
            if (!File.Exists(file)) throw new FileNotFoundException("Check configuration - cannot find auth key file: " + file);
            var keyParams = JsonSerializer.Deserialize<SecurityKeyWithPrivate>(File.ReadAllText(file));
            return keyParams;

        }

        public IReadOnlyCollection<SecurityKeyWithPrivate> Get(JsonWebKeyType jsonWebKeyType, int quantity = 5)
        {
            if (!_memoryCache.TryGetValue(JwkContants.JwksCache, out IReadOnlyCollection<SecurityKeyWithPrivate> keys))
            {
                keys = KeysPath.GetFiles("*.key")
                    .Take(quantity)
                    .Select(s => s.FullName)
                    .Select(GetKey).ToList().AsReadOnly();

                // Set cache options.
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    // Keep in cache for this time, reset time if accessed.
                    .SetSlidingExpiration(_options.Value.CacheTime);

                if (keys.Any())
                    _memoryCache.Set(JwkContants.JwksCache, keys, cacheEntryOptions);
            }

            return keys.Where(w => w.JwkType == jsonWebKeyType).ToList().AsReadOnly();
        }

        public void Clear()
        {
            if (KeysPath.Exists)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                foreach (var fileInfo in KeysPath.GetFiles($"*.key"))
                {
                    fileInfo.Delete();
                }
            }
        }


        private void ClearCache()
        {
            _memoryCache.Remove(JwkContants.JwksCache);
            _memoryCache.Remove(JwkContants.CurrentJwkCache(JsonWebKeyType.Jwe));
            _memoryCache.Remove(JwkContants.CurrentJwkCache(JsonWebKeyType.Jws));
        }
    }
}
