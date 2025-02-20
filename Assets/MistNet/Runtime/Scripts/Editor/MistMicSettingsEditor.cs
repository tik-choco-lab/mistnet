using UnityEditor;

namespace MistNet.VC
{
    [CustomEditor(typeof(MistVCInput))]
    public class MistMicSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            // 基本のインスペクターの表示
            DrawDefaultInspector();

            // 現在のターゲットを取得
            MistVCInput vcInput = (MistVCInput)target;

            // マイクデバイスのリストを取得
            var devices = vcInput.GetMicrophoneDevices();

            // 利用可能なマイクデバイスを表示するドロップダウンメニュー
            var selectedIndex = System.Array.IndexOf(devices, vcInput.selectedMicDevice);
            if (selectedIndex == -1) selectedIndex = 0;

            selectedIndex = EditorGUILayout.Popup("Microphone Device", selectedIndex, devices);

            // 選択が変更された場合、設定を更新
            if (devices.Length > 0 && devices[selectedIndex] != vcInput.selectedMicDevice)
            {
                vcInput.SetMicrophoneDevice(devices[selectedIndex]);
                vcInput.selectedMicDevice = devices[selectedIndex];
            }

            // 更新を保存
            EditorUtility.SetDirty(target);
        }
    }
}
