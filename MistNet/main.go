package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"sync"

	"github.com/gorilla/websocket"
)

// NodeId は接続ノードの識別子
type NodeId string

// SignalingType は通信タイプ
type SignalingType string

const (
	SignalingTypeRequest SignalingType = "Request"
)

type MistConfig struct {
	RoomId     string
	GlobalNode struct {
		Enable bool
		Port   int
	}
}

const configFile = "config.json"

type SignalingData struct {
	Type       SignalingType `json:"Type"`
	Data       string        `json:"Data,omitempty"`
	SenderId   NodeId        `json:"SenderId"`
	ReceiverId NodeId        `json:"ReceiverId"`
	RoomId     string        `json:"RoomId"`
}

type MistServer struct {
	config         MistConfig
	upgrader       websocket.Upgrader
	clients        map[*websocket.Conn]NodeId
	nodeToConn     map[NodeId]*websocket.Conn
	sessionToNode  map[string]NodeId
	nodeToSession  map[NodeId]string
	requestQueue   []NodeIdWithData
	requestQueueMu sync.Mutex
}

type NodeIdWithData struct {
	NodeId NodeId
	Data   SignalingData
}

func loadConfig() (*MistConfig, error) {
	if _, err := os.Stat(configFile); os.IsNotExist(err) {
		return createDefaultConfig()
	}

	file, err := os.Open(configFile)
	if err != nil {
		return nil, err
	}
	defer file.Close()

	var config MistConfig
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		return nil, err
	}

	fmt.Println(config)
	return &config, nil
}

func createDefaultConfig() (*MistConfig, error) {
	config := MistConfig{
		RoomId: "MistNet",
		GlobalNode: struct {
			Enable bool
			Port   int
		}{
			Enable: true,
			Port:   8080,
		},
	}

	json, err := json.MarshalIndent(config, "", "  ")
	if err != nil {
		return nil, err
	}

	err = os.WriteFile(configFile, json, 0644)
	if err != nil {
		return nil, err
	}

	fmt.Println(config)

	return &config, nil
}

func MistDebug(format string, v ...interface{}) {
	log.Printf(format, v...)
}

func NewMistServer(config MistConfig) *MistServer {
	return &MistServer{
		config: config,
		upgrader: websocket.Upgrader{
			CheckOrigin: func(r *http.Request) bool {
				return true // 全オリジンからの接続を許可
			},
		},
		clients:       make(map[*websocket.Conn]NodeId),
		nodeToConn:    make(map[NodeId]*websocket.Conn),
		sessionToNode: make(map[string]NodeId),
		nodeToSession: make(map[NodeId]string),
		requestQueue:  []NodeIdWithData{},
	}
}

func (s *MistServer) Start() {
	if !s.config.GlobalNode.Enable {
		return
	}

	http.HandleFunc("/signaling", s.handleWebSocket)

	port := s.config.GlobalNode.Port
	addr := fmt.Sprintf(":%d", port)
	MistDebug("[MistSignalingServer] Start %d", port)
	log.Fatal(http.ListenAndServe(addr, nil))
}

func (s *MistServer) handleWebSocket(w http.ResponseWriter, r *http.Request) {
	conn, err := s.upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Println("Upgrade error:", err)
		return
	}
	defer conn.Close()

	sessionID := conn.RemoteAddr().String()
	MistDebug("[SERVER][OPEN] %s", sessionID)

	for {
		_, message, err := conn.ReadMessage()
		if err != nil {
			MistDebug("[SERVER][CLOSE] %s", sessionID)
			s.handleClose(conn, sessionID)
			break
		}

		s.handleMessage(conn, sessionID, message)
	}
}

func (s *MistServer) handleMessage(conn *websocket.Conn, sessionID string, message []byte) {
	MistDebug("[SERVER][RECV] %s", string(message))

	var data SignalingData
	if err := json.Unmarshal(message, &data); err != nil {
		MistDebug("[SERVER][ERROR] Invalid JSON: %v", err)
		return
	}

	if data.RoomId != s.config.RoomId {
		return
	}

	s.nodeToSession[data.SenderId] = sessionID
	s.sessionToNode[sessionID] = data.SenderId
	s.clients[conn] = data.SenderId
	s.nodeToConn[data.SenderId] = conn

	if data.Type == SignalingTypeRequest {
		s.requestQueueMu.Lock()
		s.requestQueue = append(s.requestQueue, NodeIdWithData{
			NodeId: data.SenderId,
			Data:   data,
		})
		queueLen := len(s.requestQueue)
		s.requestQueueMu.Unlock()

		if queueLen >= 2 {
			s.sendRequest()
		}
		return
	}

	s.send(data.ReceiverId, message)
}

func (s *MistServer) sendRequest() {
	s.requestQueueMu.Lock()
	defer s.requestQueueMu.Unlock()

	if len(s.requestQueue) < 2 {
		return
	}

	nodeA := s.requestQueue[0]
	nodeB := s.requestQueue[1]
	s.requestQueue = s.requestQueue[2:]

	nodeAData := SignalingData{
		Type:       SignalingTypeRequest,
		ReceiverId: nodeA.NodeId,
		SenderId:   nodeB.NodeId,
		RoomId:     nodeB.Data.RoomId,
	}

	nodeBData := SignalingData{
		Type:       SignalingTypeRequest,
		Data:       "Disconnect",
		ReceiverId: nodeB.NodeId,
		SenderId:   nodeA.NodeId,
		RoomId:     nodeA.Data.RoomId,
	}

	nodeAJSON, _ := json.Marshal(nodeAData)
	nodeBJSON, _ := json.Marshal(nodeBData)

	s.send(nodeA.NodeId, nodeAJSON)
	s.send(nodeB.NodeId, nodeBJSON)

	s.requestQueue = append(s.requestQueue, nodeA)
}

func (s *MistServer) send(receiverId NodeId, data []byte) {
	MistDebug("[SERVER][SEND] %s %s", receiverId, string(data))
	conn, exists := s.nodeToConn[receiverId]
	if !exists {
		MistDebug("[SERVER][ERROR] %s is not found.", receiverId)
		return
	}

	if err := conn.WriteMessage(websocket.TextMessage, data); err != nil {
		MistDebug("[SERVER][ERROR] Failed to send: %v", err)
	}
}

func (s *MistServer) handleClose(conn *websocket.Conn, sessionID string) {
	nodeID, exists := s.sessionToNode[sessionID]
	if exists {
		delete(s.nodeToSession, nodeID)
		delete(s.nodeToConn, nodeID)
	}
	delete(s.sessionToNode, sessionID)
	delete(s.clients, conn)
}

func main() {
	config, err := loadConfig()
	if err != nil {
		fmt.Println(err)
		return
	}
	server := NewMistServer(*config)
	server.Start()
}
