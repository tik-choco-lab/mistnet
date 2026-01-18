![License](https://img.shields.io/badge/License-MIT-green.svg)
![Unity](https://img.shields.io/badge/Unity-6-black.svg?style=flat&logo=unity)
[![Releases](https://img.shields.io/github/release/tik-choco-lab/mistnet.svg)](https://github.com/tik-choco-lab/mistnet/releases)
[![Documents](https://img.shields.io/badge/Docs-blue)](https://deepwiki.com/tik-choco-lab/mistnet)


# 特點
這是一個針對Unity的基於WebRTC的網絡庫。
僅在初次建立連接時使用信令服務器，之後基本上無需服務器即可實現多人遊戲通信。如有需要，也可以使用TURN服務器。

**實作範例**

https://github.com/tik-choco-lab/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8


# 導入方法
UPM Package
本軟件使用了MemoryPack和UniTask。

需要事先導入。
- MemoryPack
```
https://github.com/Cysharp/MemoryPack.git?path=src/MemoryPack.Unity/Assets/Plugins/MemoryPack
```
- UniTask
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```
- MistNet
```
https://github.com/DecentralizedMetaverse/mistnet.git?path=/Assets/MistNet
```

# Signaling Server
可以使用這裡提供的服務器：

https://github.com/tik-choco-lab/mistnet-signaling

# 初始設置
請將「MistNet」Prefab放置於Scene中。

Prefab位於「Packages/MistNet/Runtime/Prefabs」中。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/e706a9e6-d549-489b-b1cc-1d4a770f6c70)

請參考圖片設置連接選擇方法。
如果是全網狀（Full Mesh）連接，請設置為 Default。

<img width="450" height="282" alt="image" src="https://github.com/user-attachments/assets/10eec4c6-8320-496c-a881-e2f20f877355" />

# 同步GameObject的設定方法

## 設定
- 添加「MistSyncObject」組件。
    - 用於RPC呼叫和同步對象的識別。

## 座標同步方法
- 添加「MistTransform」組件。

## 動畫同步方法
- 添加「MistAnimatorState」組件。

## Player的生成
- 不要從一開始就在Scene中配置要同步的GameObject，而是必須經由MistNet進行Instantiate。

- 將目標GameObject的Prefab註冊到Addressable Assets中，然後按照以下方式執行。

![image](https://github.com/tik-choco-lab/mistnet/assets/38463346/8ee873c1-89ff-4774-b762-a9017df5a825)


```csharp
[SerializeField] 
string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";

MistManager.I.InstantiateAsync(prefabAddress, position, Quaternion.identity).Forget();
```

# RPC
## 註冊方法
在方法前加上`[MistRpc]`。
```csharp
[MistRpc]
void RPC_○○ () {}
```

## 調用方法
```csharp
[SerializeField] MistSyncObject syncObject;

// 發送給包括自己在內的所有人的方法
syncObject.RPCAll(nameof(RPC_○○), args);

// 向所有連接的Peer發送的方法
syncObject.RPCOther(nameof(RPC_○○), args);

// 指定接收者ID並執行的方法
syncObject.RPC(id, nameof(RPC_○○), args);
```

# 變數同步

通過添加 `[MistSync]`，可以實現變量同步。

```csharp
[MistSync]
int hp { get; set; }
```
當用戶首次加入以及值發生變化時，將自動進行同步。

此外，在同步時可以執行特定的方法。
```csharp
[MistSync(OnChanged = nameof(OnChanged))]
int hp { get; set; }

void OnChanged();    
```
