# MistNet
![License](https://img.shields.io/badge/License-MIT-green.svg)
![Unity](https://img.shields.io/badge/Unity-6-black.svg?style=flat&logo=unity)
[![Releases](https://img.shields.io/github/release/tik-choco-lab/mistnet.svg)](https://github.com/tik-choco-lab/mistnet/releases)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/tik-choco-lab/mistnet)
[![Japanese Documents](https://img.shields.io/badge/日本語-blue)](README_JP.md)
[![中文文件](https://img.shields.io/badge/中文-red)](README_CN.md)

# Features
A fully decentralized network library for Unity based on WebRTC.
It uses a signaling server only for the initial connection establishment, and thereafter realizes multiplayer communication basically without a server. A TURN server can also be used if necessary.

**Implementation example**

https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8

# Setup

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) and search/install **MemoryPack**.

2. Install [MemoryPack](https://github.com/Cysharp/MemoryPack) via Git URL:
```
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity
```

3. Install [UniTask](https://github.com/Cysharp/UniTask) via Git URL:
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

4. Install **MistNet** via Git URL:
```
https://github.com/tik-choco-lab/mistnet.git?path=/Assets/MistNet
```
# Quickstart

<img width="389" height="272" alt="image" src="https://github.com/user-attachments/assets/8737962c-fe2c-4d7c-901c-356e1074b917" />

<img width="737" height="171" alt="image" src="https://github.com/user-attachments/assets/f749da96-e55f-45c0-a988-92e9a5a87c35" />

<img width="364" height="64" alt="image" src="https://github.com/user-attachments/assets/d5f867e5-bf50-49c6-80ba-6087e30e8362" />

## Signaling Server

When you run first, a config file will be generated.
Open mistnet_config.json
and set the signaling server URL.

```json
"bootstraps": [
    "wss://rtc.tik-choco.com/signaling"
],
```
    
If you want to use your own signaling server, please refer to the following repository.
https://github.com/tik-choco-lab/mistnet-signaling

# Usage
Place the "MistNet" Prefab in the Scene.

The Prefab is located in "Packages/MistNet/Runtime/Prefabs".

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/e706a9e6-d549-489b-b1cc-1d4a770f6c70)

Please configure the connection selection method as shown in the image.
Set it to Default if you want to connect in a full mesh.

<img width="450" height="282" alt="image" src="https://github.com/user-attachments/assets/10eec4c6-8320-496c-a881-e2f20f877355" />

# Setting up GameObjects to Synchronize

## Configuration
- Add "MistSyncObject" component.
    - Used for RPC calls and identifying the object to sync.

## Position Synchronization Method
- Add "MistTransform" component.

## Animation Synchronization Method
- Add "MistAnimatorState" component.

## Player Instantiation
- Instead of placing the GameObject to synchronize in the Scene from the beginning, you need to Instantiate it via MistNet.

- Register the target GameObject's Prefab in Addressable Assets and execute as follows:

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/8ee873c1-89ff-4774-b762-a9017df5a825)


```csharp
[SerializeField] 
string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";

MistManager.I.InstantiateAsync(prefabAddress, position, Quaternion.identity).Forget();
```

# RPC
## Registration Method
Add `[MistRpc]` before the method.
```csharp
[MistRpc]
void RPC_○○ () {}
```

## Invocation Method
```csharp
[SerializeField] MistSyncObject syncObject;

// Method to send to everyone including self
syncObject.RPCAll(nameof(RPC_○○), args);

// Method to send to all connected Peers
syncObject.RPCOther(nameof(RPC_○○), args);

// Method to execute by specifying the destination ID
syncObject.RPC(id, nameof(RPC_○○), args);
```

# Variable Synchronization

By adding `[MistSync]`, you can synchronize variables.

```csharp
[MistSync]
int hp { get; set; }
```
Synchronization occurs automatically when a user joins for the first time and when the value changes.

Also, you can execute an arbitrary method during synchronization.
```csharp
[MistSync(OnChanged = nameof(OnChanged))]
int hp { get; set; }

void OnChanged();    
```
