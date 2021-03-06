using System.Collections.Generic;
using NetDevPack.Security.Jwt.Model;

namespace NetDevPack.Security.Jwt.Interfaces
{
    public interface IJsonWebKeyStore
    {
        void Save(SecurityKeyWithPrivate securityParamteres);
        SecurityKeyWithPrivate GetCurrentKey(JsonWebKeyType jwkType);
        IReadOnlyCollection<SecurityKeyWithPrivate> Get(JsonWebKeyType jwkType, int quantity = 5);
        void Clear();
        bool NeedsUpdate(JsonWebKeyType jsonWebKeyType);
        void Revoke(SecurityKeyWithPrivate securityKeyWithPrivate);
    }
}