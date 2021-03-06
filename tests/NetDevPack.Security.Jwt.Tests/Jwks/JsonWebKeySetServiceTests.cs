using System;
using System.Collections.Generic;
using System.Security.Claims;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using NetDevPack.Security.Jwt.DefaultStore.Memory;
using NetDevPack.Security.Jwt.Interfaces;
using NetDevPack.Security.Jwt.Jwk;
using NetDevPack.Security.Jwt.Jwks;
using NetDevPack.Security.Jwt.Model;
using Xunit;

namespace NetDevPack.Security.Jwt.Tests.Jwks
{
    public class JsonWebKeySetServiceTests
    {
        private readonly JwksService _jwksService;
        private readonly IJsonWebKeyStore _store;
        private readonly Mock<IOptions<JwksOptions>> _options;

        public JsonWebKeySetServiceTests()
        {
            _options = new Mock<IOptions<JwksOptions>>();
            _store = new InMemoryStore(_options.Object);
            _jwksService = new JwksService(_store, new JwkService(), _options.Object);
            _options.Setup(s => s.Value).Returns(new JwksOptions());
        }

        [Fact]
        public void ShouldGenerateDefaultSigning()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var sign = _jwksService.GenerateSigningCredentials();
            var current = _jwksService.GetCurrentSigningCredentials();
            current.Kid.Should().Be(sign.Kid);
        }

        [Fact]
        public void ShouldGenerateDefaultEncryption()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var sign = _jwksService.GenerateEncryptingCredentials();
            var current = _jwksService.GetCurrentEncryptingCredentials();
            current.Key.KeyId.Should().Be(sign.Key.KeyId);
        }

        [Fact]
        public void ShouldGenerateFiveDefaultSigning()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            _store.Clear();
            var keysGenerated = new List<SigningCredentials>();
            for (int i = 0; i < 5; i++)
            {
                var sign = _jwksService.GenerateSigningCredentials();
                keysGenerated.Add(sign);
            }

            var current = _jwksService.GetLastKeysCredentials(JsonWebKeyType.Jws, 5);
            foreach (var securityKey in current)
            {
                keysGenerated.Should().Contain(s => s.Kid == securityKey.KeyId);
            }
        }

        [Fact]
        public void ShouldGenerateFiveDefaultEncrypting()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            _store.Clear();
            var keysGenerated = new List<EncryptingCredentials>();
            for (int i = 0; i < 5; i++)
            {
                var sign = _jwksService.GenerateEncryptingCredentials();
                keysGenerated.Add(sign);
            }

            var current = _jwksService.GetLastKeysCredentials(JsonWebKeyType.Jwe, 5);
            foreach (var securityKey in current)
            {
                keysGenerated.Should().Contain(s => s.Key.KeyId == securityKey.KeyId);
            }
        }

        [Fact]
        public void ShouldGenerateRsa()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_", Jws = JwsAlgorithm.RS512 });
            _store.Clear();
            var sign = _jwksService.GenerateSigningCredentials();
            sign.Algorithm.Should().Be(JwsAlgorithm.RS512);
        }


        [Fact]
        public void ShouldGenerateECDsa()
        {
            _options.Setup(s => s.Value).Returns(new JwksOptions() { Jws = JwsAlgorithm.ES256, KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" });
            var sign = _jwksService.GenerateSigningCredentials();
            var current = _store.GetCurrentKey(JsonWebKeyType.Jws);
            current.KeyId.Should().Be(sign.Kid);
            current.JwsAlgorithm.Should().Be(SecurityAlgorithms.EcdsaSha256);
        }


        [Theory]
        [InlineData(SecurityAlgorithms.RsaOAEP, KeyType.RSA, SecurityAlgorithms.Aes128CbcHmacSha256)]
        [InlineData(SecurityAlgorithms.RsaPKCS1, KeyType.RSA, SecurityAlgorithms.Aes128CbcHmacSha256)]
        [InlineData(SecurityAlgorithms.Aes128KW, KeyType.AES, SecurityAlgorithms.Aes128CbcHmacSha256)]
        [InlineData(SecurityAlgorithms.Aes256KW, KeyType.AES, SecurityAlgorithms.Aes128CbcHmacSha256)]
        [InlineData(SecurityAlgorithms.RsaOAEP, KeyType.RSA, SecurityAlgorithms.Aes256CbcHmacSha512)]
        [InlineData(SecurityAlgorithms.RsaPKCS1, KeyType.RSA, SecurityAlgorithms.Aes256CbcHmacSha512)]
        [InlineData(SecurityAlgorithms.Aes128KW, KeyType.AES, SecurityAlgorithms.Aes256CbcHmacSha512)]
        [InlineData(SecurityAlgorithms.Aes256KW, KeyType.AES, SecurityAlgorithms.Aes256CbcHmacSha512)]
        [InlineData(SecurityAlgorithms.RsaOAEP, KeyType.RSA, SecurityAlgorithms.Aes192CbcHmacSha384)]
        [InlineData(SecurityAlgorithms.RsaPKCS1, KeyType.RSA, SecurityAlgorithms.Aes192CbcHmacSha384)]
        [InlineData(SecurityAlgorithms.Aes128KW, KeyType.AES, SecurityAlgorithms.Aes192CbcHmacSha384)]
        [InlineData(SecurityAlgorithms.Aes256KW, KeyType.AES, SecurityAlgorithms.Aes192CbcHmacSha384)]
        public void ShouldValidateJwe(string algorithm, KeyType keyType, string encryption)
        {
            var options = new JwksOptions()
            {
                KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_",
                Jwe = JweAlgorithm.Create(algorithm, keyType).WithEncryption(encryption)
            };

            var encryptingCredentials = _jwksService.GenerateEncryptingCredentials(options);

            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var jwt = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                EncryptingCredentials = encryptingCredentials
            };

            var jwe = handler.CreateToken(jwt);
            var result = handler.ValidateToken(jwe,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void ShouldValidateJweAndJws()
        {
            var options = new JwksOptions()
            {
                KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_",
            };

            var encryptingCredentials = _jwksService.GenerateEncryptingCredentials(options);
            var signingCredentials = _jwksService.GenerateSigningCredentials(options);

            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var jwtE = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                EncryptingCredentials = encryptingCredentials
            };
            var jwtS = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                SigningCredentials = signingCredentials
            };


            var jwe = handler.CreateToken(jwtE);
            var jws = handler.CreateToken(jwtS);

            var jweResult = handler.ValidateToken(jwe,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });
            var jwsResult = handler.ValidateToken(jws,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });

            jweResult.IsValid.Should().BeTrue();
        }



        [Theory]
        [InlineData(SecurityAlgorithms.HmacSha256, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.HmacSha384, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.HmacSha512, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.RsaSha256, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSha384, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSha512, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha256, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha384, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha512, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.EcdsaSha256, KeyType.ECDsa)]
        [InlineData(SecurityAlgorithms.EcdsaSha384, KeyType.ECDsa)]
        [InlineData(SecurityAlgorithms.EcdsaSha512, KeyType.ECDsa)]
        public void ShouldValidateJws(string algorithm, KeyType keyType)
        {
            var options = new JwksOptions()
            {
                Jws = JwsAlgorithm.Create(algorithm, keyType),
                KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_"
            };
            var signingCredentials = _jwksService.GenerateSigningCredentials(options);
            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var jwt = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                SigningCredentials = signingCredentials
            };

            var jws = handler.CreateToken(jwt);
            var result = handler.ValidateToken(jws,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    IssuerSigningKey = signingCredentials.Key
                });

            result.IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData(SecurityAlgorithms.HmacSha256, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.HmacSha384, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.HmacSha512, KeyType.HMAC)]
        [InlineData(SecurityAlgorithms.RsaSha256, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSha384, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSha512, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha256, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha384, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.RsaSsaPssSha512, KeyType.RSA)]
        [InlineData(SecurityAlgorithms.EcdsaSha256, KeyType.ECDsa)]
        [InlineData(SecurityAlgorithms.EcdsaSha384, KeyType.ECDsa)]
        [InlineData(SecurityAlgorithms.EcdsaSha512, KeyType.ECDsa)]
        public void ShouldGetCurrentToSignAndValidateJws(string algorithm, KeyType keyType)
        {
            var options = new JwksOptions() { Jws = JwsAlgorithm.Create(algorithm, keyType), KeyPrefix = $"{nameof(JsonWebKeySetServiceTests)}_" };
            _jwksService.GenerateSigningCredentials(options);
            var signingCredentials = _jwksService.GetCurrentSigningCredentials();
            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                SigningCredentials = signingCredentials
            };

            var jwt = handler.CreateToken(descriptor);
            var result = handler.ValidateToken(jwt,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    IssuerSigningKey = signingCredentials.Key
                });

            result.IsValid.Should().BeTrue();
        }


        public Faker<Claim> GenerateClaim()
        {
            return new Faker<Claim>().CustomInstantiator(f => new Claim(f.Internet.DomainName(), f.Lorem.Text()));
        }
    }
}
