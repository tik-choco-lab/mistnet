## MistNet
- [English Documents](README_EN.md)
- [中文文件](README_CN.md)


## 特徴
Unity向けのWebRTCベースのネットワークライブラリです。
初回の接続確立時のみシグナリングサーバーを利用し、それ以降は基本的にサーバー不要でマルチプレイ通信を実現します。必要に応じてTURNサーバーも利用可能です。

**実装例**

https://github.com/DecentralizedMetaverse/mistnet/assets/38463346/cd4a1d95-3422-4b07-b9b6-21f8c63cd1f8



## 導入方法
UPM Package
本ソフトウェアは、MemoryPackとUniTaskが使用されています。

事前にImportする必要があります。
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

## Signaling Server
こちらを使用することができます。

https://github.com/tik-choco-lab/mistnet-signaling

## 初期設定
Scene上に「MistNet」Prefabを置いてください。

Prefabは「Packages/MistNet/Runtime/Prefabs」の中にあります。

![image](https://github.com/DecentralizedMetaverse/mistnet/assets/38463346/e706a9e6-d549-489b-b1cc-1d4a770f6c70)

画像のように接続先選択方法を設定してください。
フルメッシュ型で接続する場合はDefaultを設定してください。

<img width="450" height="282" alt="image" src="https://github.com/user-attachments/assets/10eec4c6-8320-496c-a881-e2f20f877355" />

## 同期するGameObjectの設定方法

### 設定
- 「MistSyncObject」を Add Componentします。
    - RPC呼び出しや、同期するObjectの識別に使用されます。

### 座標同期方法
- 「MistTransform」を Add Componentします。

### Animation同期方法
- 「MistAnimatorState」を Add Componentします。

### Playerの生成
- 最初からSceneに同期するGameObjectを配置するのではなく、
MistNetを経由してInstantiateする必要があります。

- Addressable Assets に対象となるGameObjectのPrefabを登録し、下記のように実行してください。

![image](https://github.com/DecentralizedMetaverse/mistnet/assets/38463346/8ee873c1-89ff-4774-b762-a9017df5a825)


```csharp
[SerializeField] 
string prefabAddress = "Assets/Prefab/MistNet/MistPlayerTest.prefab";

MistManager.I.InstantiateAsync(prefabAddress, position, Quaternion.identity).Forget();
```

## RPC
### 登録方法
`[MistRpc]`をメソッドの前につけます。
```csharp
[MistRpc]
void RPC_○○ () {}
```

### 呼び出し方法
```csharp
[SerializeField] MistSyncObject syncObject;

// 自身を含めた全員に送信する方法
syncObject.RPCAll(nameof(RPC_○○), args);

// 接続しているPeer全員に送信する方法
syncObject.RPCOther(nameof(RPC_○○), args);

// 送信先のIDを指定して実行する方法
syncObject.RPC(id, nameof(RPC_○○), args);

```

## 変数の同期

`[MistSync]`をつけることで、変数の同期が可能です。

```csharp
[MistSync]
int hp { get; set; }
```
同期のタイミングは、ユーザーが新しく参加した時と、値が変更時に自動的に行われます。

また、同期時に、任意のメソッドを実行することも可能です。
```csharp
[MistSync(OnChanged = nameof(OnChanged))]
int hp { get; set; }

void OnChanged();    
```
