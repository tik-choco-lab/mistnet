using Chaos.NaCl;
using UnityEngine;

namespace MistNet
{
    public class MistAuth : MonoBehaviour
    {
        private const string AuthMessage = "d2b9c48c07e54f969324a3c75b83b275";
        public static MistAuth I { get; private set; }

        public ConfigLoader ConfigLoader = new();
        public FriendsConfigLoader FriendsConfig = new();

        [ContextMenu("Test")]
        private void Start()
        {
            I = this;
            ConfigLoader.LoadConfig();
            FriendsConfig.LoadConfig();
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

        public static byte[] SignData(byte[] data, byte[] privateKey)
        {
            return Ed25519.Sign(data, privateKey);
        }

        public static bool VerifySignature(byte[] data, byte[] signature, byte[] publicKey)
        {
            return Ed25519.Verify(signature, data, publicKey);
        }
    }
}
