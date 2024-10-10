import asyncio
import json
import random
import websockets
import logging
import datetime
import os
from websockets.exceptions import ConnectionClosed
from ordered_set import OrderedSet

class WebSocketServer:
    def __init__(self):
        self.clients = {}
        self.clients_by_id = {}
        self.broadcast_queue = asyncio.Queue()
        self.signaling_requests = OrderedSet()
        self.setup_logging()

    def setup_logging(self):
        current_datetime = datetime.datetime.now().strftime('%Y-%m-%d-%H-%M-%S')
        log_dir = "logs"
        os.makedirs(log_dir, exist_ok=True)
        logging.basicConfig(
            filename=f"{log_dir}/{current_datetime}.log",
            level=logging.INFO,
            format='%(asctime)s %(message)s'
        )

    async def start_server(self):
        async with websockets.serve(self.handle_client, "localhost", 8080):
            await asyncio.Future()  # Run indefinitely

    async def handle_client(self, websocket, path):
        try:
            async for message in websocket:
                await self.process_message(websocket, message)
        except ConnectionClosed:
            pass  # Handle client disconnection
        finally:
            self.remove_client(websocket)

    async def process_message(self, websocket, message):
        logging.info(f"[RECV] {message}")
        data = json.loads(message)
        client_id = data.get("id")

        if websocket not in self.clients:
            self.add_client(websocket, client_id)

        if data["type"] == "evaluation":
            await self.handle_evaluation(data)
        elif data["type"] == "signaling_request":
            await self.handle_signaling_request(data)
        else:
            await self.forward_message(data)

    def add_client(self, websocket, client_id):
        self.clients[websocket] = client_id
        self.clients_by_id[client_id] = websocket

    def remove_client(self, websocket):
        client_id = self.clients.pop(websocket, None)
        if client_id:
            del self.clients_by_id[client_id]
            self.signaling_requests.discard(client_id)

    async def handle_signaling_request(self, data):
        client_id = data["id"]
        if self.signaling_requests:
            target_id = random.choice(list(self.signaling_requests))
            client = self.clients_by_id[client_id]
            await self.send_message(client, {
                "type": "signaling_response",
                "target_id": target_id,
                "request": "offer"
            })
        self.signaling_requests.add(client_id)

    async def handle_evaluation(self, data):
        pass

    async def forward_message(self, data):
        target_id = data["target_id"]
        target_client = self.clients_by_id.get(target_id)
        if target_client:
            await self.send_message(target_client, data)

    async def send_message(self, client, data):
        logging.info(f"[SEND] {data}")
        try:
            await client.send(json.dumps(data))
        except ConnectionClosed:
            self.remove_client(client)

if __name__ == "__main__":
    server = WebSocketServer()
    asyncio.run(server.start_server())