![License](https://img.shields.io/badge/License-MIT-green.svg)
![Unity](https://img.shields.io/badge/Unity-6-black.svg?style=flat\&logo=unity)
[![Releases](https://img.shields.io/github/release/tik-choco-lab/mistnet.svg)](https://github.com/tik-choco-lab/mistnet/releases)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/tik-choco-lab/mistnet)

# 特点

这是一个面向 Unity 的、基于 WebRTC 的网络库。
仅在初次建立连接时使用信令服务器，之后基本无需服务器即可实现多人游戏通信。
如有需要，也可以使用 TURN 服务器。

**实现示例**

[https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8](https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8)

# 初始设置

1. 安装 [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)，并搜索 / 安装 **MemoryPack**。

2. 通过 Git URL 安装 [MemoryPack](https://github.com/Cysharp/MemoryPack)：

```
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity
```

3. 通过 Git URL 安装 [UniTask](https://github.com/Cysharp/UniTask)：

```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

4. 通过 Git URL 安装 **MistNet**：

```
https://github.com/tik-choco-lab/mistnet.git?path=/Assets/MistNet
```

# Quickstart

<img width="389" height="272" alt="image" src="https://github.com/user-attachments/assets/8737962c-fe2c-4d7c-901c-356e1074b917" />

<img width="737" height="171" alt="image" src="https://github.com/user-attachments/assets/f749da96-e55f-45c0-a988-92e9a5a87c35" />

<img width="364" height="64" alt="image" src="https://github.com/user-attachments/assets/d5f867e5-bf50-49c6-80ba-6087e30e8362" />

## 信令服务器（Signaling Server）

首次运行时会生成一个配置文件。
打开 `mistnet_config.json`，并设置信令服务器的 URL。

```json
"bootstraps": [
    "wss://rtc.tik-choco.com/signaling"
],
```

如果你想使用自己的信令服务器，请参考以下仓库：
[https://github.com/tik-choco-lab/mistnet-signaling](https://github.com/tik-choco-lab/mistnet-signaling)

# 初始设置

请将「MistNet」Prefab 放置到 Scene 中。

Prefab 位于 `Packages/MistNet/Runtime/Prefabs`。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/e706a9e6-d549-489b-b1cc-1d4a770f6c70)

请参考图片设置连接选择方式。
如果是全网状（Full Mesh）连接，请设置为 Default。

<img width="450" height="282" alt="image" src="https://github.com/user-attachments/assets/10eec4c6-8320-496c-a881-e2f20f877355" />

# 同步 GameObject 的设置方法

## 设置

* 添加「MistSyncObject」组件

  * 用于 RPC 调用以及同步对象的识别。

## 坐标同步方法

* 添加「MistTransform」组件。

## 动画同步方法

* 添加「MistAnimatorState」组件。

## Player 的生成

* 不要在一开始就将需要同步的 GameObject 放置在 Scene 中，
  而必须通过 MistNet 进行 Instantiate。

* 将目标 GameObject 的 Prefab 注册到 Addressable Assets 中，然后按如下方式执行。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/8ee873c1-89ff-4774-b762-a9017df5a825)

```csharp
[SerializeField] 
string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";

MistManager.I.InstantiateAsync(prefabAddress, position, Quaternion.identity).Forget();
```

# RPC

## 注册方法

在方法前添加 `[MistRpc]`。

```csharp
[MistRpc]
void RPC_○○ () {}
```

## 调用方法

```csharp
[SerializeField] MistSyncObject syncObject;

// 发送给包括自己在内的所有人
syncObject.RPCAll(nameof(RPC_○○), args);

// 发送给所有已连接的 Peer
syncObject.RPCOther(nameof(RPC_○○), args);

// 指定接收者 ID 并执行
syncObject.RPC(id, nameof(RPC_○○), args);
```

# 变量同步

通过添加 `[MistSync]`，可以实现变量同步。

```csharp
[MistSync]
int hp { get; set; }
```

当用户首次加入以及值发生变化时，会自动进行同步。

此外，在同步时还可以执行特定方法：

```csharp
[MistSync(OnChanged = nameof(OnChanged))]
int hp { get; set; }

void OnChanged();    
```
以下为**简体中文**翻译（结构与内容保持不变，代码不作修改）。

---

![License](https://img.shields.io/badge/License-MIT-green.svg)
![Unity](https://img.shields.io/badge/Unity-6-black.svg?style=flat\&logo=unity)
[![Releases](https://img.shields.io/github/release/tik-choco-lab/mistnet.svg)](https://github.com/tik-choco-lab/mistnet/releases)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/tik-choco-lab/mistnet)

# 特点

这是一个面向 Unity 的、基于 WebRTC 的网络库。
仅在初次建立连接时使用信令服务器，之后基本无需服务器即可实现多人游戏通信。
如有需要，也可以使用 TURN 服务器。

**实现示例**

[https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8](https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8)

# 初始设置

1. 安装 [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)，并搜索 / 安装 **MemoryPack**。

2. 通过 Git URL 安装 [MemoryPack](https://github.com/Cysharp/MemoryPack)：

```
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/MemoryPack.Unity
```

3. 通过 Git URL 安装 [UniTask](https://github.com/Cysharp/UniTask)：

```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

4. 通过 Git URL 安装 **MistNet**：

```
https://github.com/tik-choco-lab/mistnet.git?path=/Assets/MistNet
```

# Quickstart

<img width="389" height="272" alt="image" src="https://github.com/user-attachments/assets/8737962c-fe2c-4d7c-901c-356e1074b917" />

<img width="737" height="171" alt="image" src="https://github.com/user-attachments/assets/f749da96-e55f-45c0-a988-92e9a5a87c35" />

<img width="364" height="64" alt="image" src="https://github.com/user-attachments/assets/d5f867e5-bf50-49c6-80ba-6087e30e8362" />

## 信令服务器（Signaling Server）

首次运行时会生成一个配置文件。
打开 `mistnet_config.json`，并设置信令服务器的 URL。

```json
"bootstraps": [
    "wss://rtc.tik-choco.com/signaling"
],
```

如果你想使用自己的信令服务器，请参考以下仓库：
[https://github.com/tik-choco-lab/mistnet-signaling](https://github.com/tik-choco-lab/mistnet-signaling)

# 初始设置

请将「MistNet」Prefab 放置到 Scene 中。

Prefab 位于 `Packages/MistNet/Runtime/Prefabs`。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/e706a9e6-d549-489b-b1cc-1d4a770f6c70)

请参考图片设置连接选择方式。
如果是全网状（Full Mesh）连接，请设置为 Default。

<img width="450" height="282" alt="image" src="https://github.com/user-attachments/assets/10eec4c6-8320-496c-a881-e2f20f877355" />

# 同步 GameObject 的设置方法

## 设置

* 添加「MistSyncObject」组件

  * 用于 RPC 调用以及同步对象的识别。

## 坐标同步方法

* 添加「MistTransform」组件。

## 动画同步方法

* 添加「MistAnimatorState」组件。

## Player 的生成

* 不要在一开始就将需要同步的 GameObject 放置在 Scene 中，
  而必须通过 MistNet 进行 Instantiate。

* 将目标 GameObject 的 Prefab 注册到 Addressable Assets 中，然后按如下方式执行。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/8ee873c1-89ff-4774-b762-a9017df5a825)

```csharp
[SerializeField] 
string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";

MistManager.I.InstantiateAsync(prefabAddress, position, Quaternion.identity).Forget();
```

# RPC

## 注册方法

在方法前添加 `[MistRpc]`。

```csharp
[MistRpc]
void RPC_○○ () {}
```

## 调用方法

```csharp
[SerializeField] MistSyncObject syncObject;

// 发送给包括自己在内的所有人
syncObject.RPCAll(nameof(RPC_○○), args);

// 发送给所有已连接的 Peer
syncObject.RPCOther(nameof(RPC_○○), args);

// 指定接收者 ID 并执行
syncObject.RPC(id, nameof(RPC_○○), args);
```

# 变量同步

通过添加 `[MistSync]`，可以实现变量同步。

```csharp
[MistSync]
int hp { get; set; }
```

当用户首次加入以及值发生变化时，会自动进行同步。

此外，在同步时还可以执行特定方法：

```csharp
[MistSync(OnChanged = nameof(OnChanged))]
int hp { get; set; }

void OnChanged();    
```
