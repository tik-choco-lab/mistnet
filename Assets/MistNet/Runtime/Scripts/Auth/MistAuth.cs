using System;
using Chaos.NaCl;
using UnityEngine;

namespace MistNet
{
    public class MistAuth : MonoBehaviour
    {
        private const string AuthMessage = "94a7873a2ca4473d97d030d67ab666b8";
        public static MistAuth I { get; private set; }

        public ConfigLoader ConfigLoader = new();
        public FriendsConfigLoader FriendsConfig = new();

        [ContextMenu("Test")]
        private void Start()
        {
            I = this;
            ConfigLoader.LoadConfig();
            FriendsConfig.LoadConfig();
            var signature = Sign(ConfigLoader.Config.Keys.PrivateKey);
            var result = Verify(ConfigLoader.Config.Keys.PublicKey, signature);
            MistLogger.Debug($"Signature: {result}");
        }

        public void AddFriend(string name, string id)
        {
            FriendsConfig.Config.Friends.Add(new Friend
            {
                Name = name,
                PublicKey = id
            });
            FriendsConfig.Save();
        }

        public string Sign(string privateKey)
        {
            MistLogger.Debug($"[Sign] {privateKey}");
            var messageBytes = Convert.FromBase64String(AuthMessage);
            var privateKeyBytes = Convert.FromBase64String(privateKey);
            var signatureBytes = Ed25519.Sign(messageBytes,privateKeyBytes);
            return Convert.ToBase64String(signatureBytes);
        }

        public bool Verify(string publicKey, string signature)
        {
            MistLogger.Debug($"[Verify] {publicKey} {signature}");
            var messageBytes = Convert.FromBase64String(AuthMessage);
            var publicKeyBytes = Convert.FromBase64String(publicKey);
            var signatureBytes = System.Convert.FromBase64String(signature);
            return Ed25519.Verify(signatureBytes, messageBytes, publicKeyBytes);
        }
    }
}
