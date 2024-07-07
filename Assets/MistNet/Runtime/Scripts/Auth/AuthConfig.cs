using System;
using System.IO;
using System.Security.Cryptography;
using Chaos.NaCl;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MistNet
{
    public class AuthConfig
    {
        public bool VerificationEnabled { get; set; }
        public Keys Keys { get; set; }
    }

    public class Keys
    {
        public string PrivateKey { get; set; }
        public string PublicKey { get; set; }
    }

    public class ConfigLoader
    {
        private static readonly string ConfigFilePath = $"{Application.dataPath}/../auth.yaml";

        public AuthConfig LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                CreateConfig();
            }

            var yaml = File.ReadAllText(ConfigFilePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<AuthConfig>(yaml);
            return config;
        }

        private void CreateConfig()
        {
            var (privateKey, publicKey) = CreateKey();
            var config = new AuthConfig
            {
                VerificationEnabled = true,
                Keys = new Keys
                {
                    PrivateKey = privateKey,
                    PublicKey = publicKey
                }
            };

            var serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(config);
            File.WriteAllText(ConfigFilePath, yaml);
        }

        private (string, string) CreateKey()
        {
            var seed = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(seed);
            }

            var privateKey = Ed25519.ExpandedPrivateKeyFromSeed(seed);
            var publicKey = Ed25519.PublicKeyFromSeed(seed);

            return (Convert.ToBase64String(privateKey), Convert.ToBase64String(publicKey));
        }
    }
}
