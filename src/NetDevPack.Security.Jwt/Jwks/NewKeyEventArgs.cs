using NetDevPack.Security.Jwt.Model;

namespace NetDevPack.Security.Jwt.Jwks
{
    public class JwkContants
    {
        public static string CurrentJwkCache(JsonWebKeyType jwkType) => $"NETDEVPACK-CURRENT-{jwkType}-SECURITY-KEY";
        public const string JwksCache = "NETDEVPACK-JWKS";
    }
}