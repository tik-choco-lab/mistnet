using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MistNet
{
    public class MistSyncObject : MonoBehaviour
    {
        public ObjectId Id { get; private set; } = new ObjectId("");
        public string PrefabAddress { get; private set; }
        public NodeId OwnerId { get; private set; }
        [SerializeField] private bool isOwner = true; // 表示用

        public bool IsOwner
        {
            get => isOwner;
            private set => isOwner = value;
        }

        public bool IsPlayerObject { get; private set; }
        [HideInInspector] public MistTransform MistTransform;
        [SerializeField] private float syncIntervalSeconds = 0.5f;

        private readonly List<string> _rpcList = new();
        private readonly List<(Component, PropertyInfo)> _propertyList = new();
        private readonly Dictionary<string, object> _propertyValueDict = new();
        private readonly List<WatchedProperty> _watchedProperties = new();
        private class WatchedProperty
        {
            public string KeyName;
            public Func<object> Getter;
        }
        private static int _instanceIdCount;

        public void Init(ObjectId id, bool isPlayer, string prefabAddress, NodeId ownerId)
        {
            Id = id;
            IsOwner = PeerRepository.I.SelfId == ownerId;
            PrefabAddress = prefabAddress;
            OwnerId = ownerId;
            IsPlayerObject = isPlayer;
            gameObject.TryGetComponent(out MistTransform);
            InitSyncParameters();
            MistSyncManager.I.RegisterSyncObject(this);
        }

        public void SetOwner(NodeId newOwnerId)
        {
            OwnerId = newOwnerId;
            IsOwner = PeerRepository.I.SelfId == newOwnerId;
            MistSyncManager.I.UnregisterSyncObject(this);

            InitSyncParameters();
            MistSyncManager.I.RegisterSyncObject(this);
        }

        private void InitSyncParameters()
        {
            RegisterPropertyAndRPC();
            if (IsOwner)
            {
                WatchPropertiesAsync(this.GetCancellationTokenOnDestroy()).Forget();
            }

            gameObject.TryGetComponent(out MistTransform);
            if (MistTransform != null) MistTransform.Init();
        }

        private void OnDestroy()
        {
            foreach (var rpc in _rpcList)
            {
                MistManager.I.RemoveRPC(rpc);
            }

            MistSyncManager.I.UnregisterSyncObject(this);
        }

        // -------------------
        public void RPC(NodeId targetId, string key, params object[] args)
        {
            MistManager.I.RPC(targetId, GetRPCName(key), args);
        }

        public void RPCOther(string key, params object[] args)
        {
            MistManager.I.RPCOther(GetRPCName(key), args);
        }

        public void RPCAll(string key, params object[] args)
        {
            MistManager.I.RPCAll(GetRPCName(key), args);
        }

        private string GetRPCName(string methodName)
        {
            return $"{Id}_{methodName}";
        }

        // -------------------

        public void SendAllProperties(NodeId id)
        {
            foreach (var (component, property) in _propertyList)
            {
                var value = property.GetValue(component);
                MistManager.I.RPC(id, GetRPCName(property.Name), value);
            }
        }

        private void RegisterPropertyAndRPC()
        {
            // 子階層を含むすべてのComponentsを取得
            var components = gameObject.GetComponentsInChildren<Component>();

            foreach (var component in components)
            {
                // 各Componentで定義されているMethodを取得し、Attributeが付与されたメソッドを検索
                var methodsWithAttribute = component.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => m.GetCustomAttributes(typeof(MistRpcAttribute), false).Length > 0);

                RegisterRPCMethods(methodsWithAttribute, component);

                // 各Componentで定義されているPropertyを取得し、Attributeが付与されたプロパティを検索
                var propertyInfos = component.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(prop => Attribute.IsDefined(prop, typeof(MistSyncAttribute))).ToList();

                RegisterSyncProperties(propertyInfos, component);

                // 各Componentで定義されているInterfaceを取得
                var interfaces = component.GetType().GetInterfaces();
                RegisterCallback(interfaces, component);
            }
        }

        private static void RegisterCallback(Type[] interfaces, Component component)
        {
            if (interfaces.Contains(typeof(IMistJoined)))
            {
                var method = component.GetType().GetMethod("OnJoined",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var delegateInstance = Delegate.CreateDelegate(Expression.GetActionType(new[] { typeof(string) }),
                        component, method);
                    MistManager.I.AddJoinedCallback((Action<string>)delegateInstance);
                }
            }

            if (interfaces.Contains(typeof(IMistLeft)))
            {
                var method = component.GetType().GetMethod("OnLeft",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (method != null)
                {
                    var delegateInstance = Delegate.CreateDelegate(Expression.GetActionType(new[] { typeof(string) }),
                        component, method);
                    MistManager.I.AddLeftCallback((Action<string>)delegateInstance);
                }
            }
        }

        /// <summary>
        /// 他のPeerからのRPCを受け取るための処理
        /// </summary>
        /// <param name="methodsWithAttribute"></param>
        /// <param name="component"></param>
        private void RegisterRPCMethods(IEnumerable<MethodInfo> methodsWithAttribute, Component component)
        {
            foreach (var methodInfo in methodsWithAttribute)
            {
                MistLogger.Log($"Found method: {methodInfo.Name} in component: {component.GetType().Name}");
                // 引数の種類に応じたDelegateを作成
                var argTypes = methodInfo.GetParameters().Select(p => p.ParameterType).ToArray();

                // 返り値がvoidかどうか
                var delegateType = methodInfo.ReturnType == typeof(void)
                    ? Expression.GetActionType(argTypes)
                    : Expression.GetFuncType(argTypes.Concat(new[] { methodInfo.ReturnType }).ToArray());

                var delegateInstance = Delegate.CreateDelegate(delegateType, component, methodInfo);
                var keyName = GetRPCName(delegateInstance.Method.Name);
                _rpcList.Add(keyName);

                var argTypesWithoutMessageInfo = argTypes.Where(t => t != typeof(MessageInfo)).ToArray();
                MistManager.I.AddObjectRPC(keyName, delegateInstance, argTypesWithoutMessageInfo);
            }
        }

        /// <summary>
        /// 他のPeerからのpropertyの変更を受け取るための処理
        /// </summary>
        /// <param name="propertyInfos"></param>
        /// <param name="component"></param>
        private void RegisterSyncProperties(List<PropertyInfo> propertyInfos, Component component)
        {
            foreach (var property in propertyInfos)
            {
                _propertyList.Add((component, property));

                var keyName = $"{Id}_{property.Name}";
                var getter = CreateGetter(property, component);
                _watchedProperties.Add(new WatchedProperty
                {
                    KeyName = keyName,
                    Getter = getter
                });
                _propertyValueDict[keyName] = getter();

                // MistSyncAttributeからOnChangedメソッド名を取得
                var mistSyncAttr = (MistSyncAttribute)Attribute.GetCustomAttribute(property, typeof(MistSyncAttribute));
                var onChangedMethodName = mistSyncAttr?.OnChanged;

                MethodInfo onChangedMethodInfo = null;
                if (!string.IsNullOrEmpty(onChangedMethodName))
                {
                    onChangedMethodInfo = component.GetType().GetMethod(onChangedMethodName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                var originalSetMethod = property.SetMethod;

                void Wrapper(object value)
                {
                    originalSetMethod.Invoke(component, new[] { value });
                    onChangedMethodInfo?.Invoke(component, null);
                }

                _rpcList.Add(keyName);
                MistManager.I.AddObjectRPC(keyName, (Action<object>)Wrapper, new[] { property.PropertyType });
            }
        }

        private static Func<object> CreateGetter(PropertyInfo property, Component component)
        {
            var instanceParam = Expression.Constant(component);
            var propertyAccess = Expression.Property(instanceParam, property);
            var castToObject = Expression.Convert(propertyAccess, typeof(object));
            var lambda = Expression.Lambda<Func<object>>(castToObject);
            return lambda.Compile();
        }

        private async UniTask WatchPropertiesAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                foreach (var watched in _watchedProperties)
                {
                    // 保存されたプロパティ情報を使用して値を取得し、ログに出力
                    var value = watched.Getter();
                    if (!_propertyValueDict.TryGetValue(watched.KeyName, out var oldValue)) continue;
                    if (Equals(oldValue, value)) continue;

                    _propertyValueDict[watched.KeyName] = value;

                    MistLogger.Log($"Property: {watched.KeyName}, Value: {value}");
                    MistManager.I.RPCOther(watched.KeyName, value);
                }

                await UniTask.Delay(TimeSpan.FromSeconds(syncIntervalSeconds), cancellationToken: token);
            }
        }
    }
}
