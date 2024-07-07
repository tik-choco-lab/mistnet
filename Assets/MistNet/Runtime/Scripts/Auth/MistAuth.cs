using UnityEngine;

namespace MistNet
{
    public class MistAuth : MonoBehaviour
    {
        private AuthConfig _authConfig;
        private ConfigLoader _configLoader = new();

        [ContextMenu("Test")]
        private void Start()
        {
            _authConfig = _configLoader.LoadConfig();
        }
    }
}
