import asyncio
import websockets
from datetime import datetime
from enum import Enum

class ConnectionStatus(Enum):
    Connected = 1
    Disconnected = 2
    Connecting = 3
    Disconnecting = 4  
    
class Vector3:
    def __init__(self, x, y, z) -> None:
        self.x = x
        self.y = y
        self.z = z

class Node:
    def __init__(self) -> None:
        self.id = None  # ノードの識別子
        self.connected = []  # 接続されているクライアントやノードのリスト
        self.position = Vector3(0, 0, 0)  # 3次元座標の初期値
        self.display_count = 0  # 表示するクライアント数
        self.max_connections = 10  # 最大接続数
        self.connection_status = ConnectionStatus.Disconnected  # 接続の状態
        self.timestamp = datetime.now()  # 最終更新時刻
    

class ServerBridge:
    def __init__(self):
        self.nodes = []
        self.connections = set()

    async def handler(self, websocket, path):
        # 新しい接続を追加
        self.connections.add(websocket)
        print(f"New connection: {websocket.remote_address}")

        # 接続状態を更新
        node = Node()
        node.id = websocket.remote_address
        node.connection_status = ConnectionStatus.Connected
        node.timestamp = datetime.now()
        self.nodes.append(node)

        try:
            # メッセージの受信ループ
            async for message in websocket:
                print(f"Message from {websocket.remote_address}: {message}")
                await websocket.send(f"Server received: {message}")
                node.timestamp = datetime.now()  # 最終更新時刻を更新
        except websockets.ConnectionClosed:
            print(f"Connection closed: {websocket.remote_address}")
        finally:
            # 接続を削除
            self.connections.remove(websocket)
            node.connection_status = ConnectionStatus.Disconnected
            node.timestamp = datetime.now()
            print(f"Connection removed: {websocket.remote_address}")

    async def start_server(self, host="localhost", port=8765):
        async with websockets.serve(self.handler, host, port):
            print(f"Server started on {host}:{port}")
            await asyncio.Future()  # Run forever


if __name__ == "__main__":
    server_bridge = ServerBridge()
    asyncio.run(server_bridge.start_server())