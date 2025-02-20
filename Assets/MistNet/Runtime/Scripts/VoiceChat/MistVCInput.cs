using UnityEngine;

namespace MistNet.VC
{
    [RequireComponent(typeof(AudioSource))]
    public class MistVCInput : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        // 現在選択されているマイクデバイスの名前
        [SerializeField] public string selectedMicDevice = "";

        private void Start()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            Debug.Log($"[Debug][MistVC] add input audio source");
            MistPeerData.I.AddInputAudioSource(audioSource);

            if (string.IsNullOrEmpty(selectedMicDevice))
            {
                // デフォルトのマイクを選択する
                selectedMicDevice = Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
            }

            Debug.Log($"[MistMicSettings] Selected microphone: {selectedMicDevice}");

            if (!string.IsNullOrEmpty(selectedMicDevice))
            {
                StartMicrophone(selectedMicDevice);
            }
            else
            {
                Debug.LogError("No microphone devices found.");
            }
        }

        // マイクの録音を開始し、AudioSourceに設定するメソッド
        private void StartMicrophone(string deviceName)
        {
            // マイクが既に設定されていれば停止
            Microphone.End(null);

            // 新しいマイクで録音を開始し、AudioSourceに設定
            audioSource.loop = true;
            audioSource.clip = Microphone.Start(deviceName, true, 10, 44100);

            // マイクが準備できるまで待つ
            while (!(Microphone.GetPosition(deviceName) > 0)) { }

            // 自身のマイク音声は再生しないのでコメントアウト
            audioSource.Play();

            // MistPeerData にオーディオ入力を追加
            MistPeerData.I.AddInputAudioSource(audioSource);
        }

        // マイクデバイスのリストを取得するためのヘルパーメソッド
        public string[] GetMicrophoneDevices()
        {
            return Microphone.devices;
        }

        // マイクデバイスを選択するためのメソッド
        public void SetMicrophoneDevice(string deviceName)
        {
            if (Microphone.devices.Length > 0 && System.Array.Exists(Microphone.devices, d => d == deviceName))
            {
                selectedMicDevice = deviceName;
                StartMicrophone(deviceName);
            }
            else
            {
                Debug.LogError("Selected microphone device not found.");
            }
        }
    }
}
