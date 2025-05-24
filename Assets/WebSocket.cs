using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static UnityEngine.UIElements.UxmlAttributeDescription;

public class Connection : MonoBehaviour
{
    WebSocket websocket;

    [SerializeField] private BinPLYMeshLoader meshLoader;
    [SerializeField] private RayInteractorSphereSpawner rayClicker;
    [SerializeField] private MaskBasedSubmeshDetacher meshDetacher;
    

    // Start is called before the first frame update
    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8765");

        websocket.OnOpen += () =>
        {
            Debug.Log("Connection open!");
        };

        websocket.OnError += (e) =>
        {
            Debug.Log("Error! " + e);
        };

        websocket.OnClose += (e) =>
        {
            Debug.Log("Connection closed!");
        };

        websocket.OnMessage += (bytes) =>
        {

            // Convert message bytes to string
            var jsonMessage = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("Received: " + jsonMessage);

            // Process the JSON message
            ProcessJsonMessage(jsonMessage);
        };

        ControlMessages.OnThumbstickPressed += SendNextScene;


        // waiting for messages
        await websocket.Connect();
    }

    void ProcessJsonMessage(string jsonMessage)
    {
        try
        {
            // Parse the JSON message using Newtonsoft.Json
            JObject messageObj = JObject.Parse(jsonMessage);

            // Check if the message has a type field
            if (messageObj["type"] == null)
            {
                Debug.LogError("Message missing 'type' field");
                return;
            }

            string messageType = messageObj["type"].ToString();

            // Handle different message types
            switch (messageType)
            {
                case "load_scene":
                    // Reset everything
                    rayClicker.Reset();
                    meshDetacher.ClearDetachedSubmeshes();
                    HandleLoadSceneMessage(messageObj);
                    break;

                case "mask":
                    // TODO
                    break;


                default:
                    Debug.LogWarning($"Unknown message type: {messageType}");
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    private async void SendNextScene(bool isPressed)
    {
        var responseMessage = new
        {
            type = "next_scene",
        };

        // Convert to JSON
        string jsonResponse = JsonConvert.SerializeObject(responseMessage);
        // Send the response if the websocket is open
        if (websocket.State == WebSocketState.Open)
        {
            Debug.Log($"Sending next_scene message");
            await websocket.SendText(jsonResponse);
        }
        else
        {
            Debug.LogWarning("WebSocket is not open, cannot send scene_loaded response");
        }
    }

    private async Task HandleLoadSceneMessage(JObject message)
    {
        try
        {
            string sceneName = message["scene_name"].ToString();
            int pointCount = message["point_count"].ToObject<int>();
            string currentObject = message["current_object"].ToString();
            bool semanticsMode = message["semantics_mode"].ToObject<bool>();

            Debug.Log($"Loading scene: {sceneName}");
            Debug.Log($"Point count: {pointCount}");
            Debug.Log($"Current object: {currentObject}");
            Debug.Log($"Semantics mode: {semanticsMode}");

            // Handle the objects array
            if (message["objects"] != null && message["objects"].Type == JTokenType.Array)
            {
                JArray objectsArray = (JArray)message["objects"];

                Debug.Log($"Scene contains {objectsArray.Count} objects");

                // Process each object in the array
                foreach (JObject obj in objectsArray)
                {
                    // Access object properties based on your structure
                    // This is an example - adjust according to your object structure
                    string objName = obj["name"]?.ToString() ?? "unnamed";
                    int objId = obj["id"]?.ToObject<int>() ?? 0;

                    Debug.Log($"Object: {objName}, ID: {objId}");

                    // Process the object further as needed
                }
            }

            if (meshLoader != null)
            {
                string filePath = $"C:\\Users\\Codey\\ee267 project\\Assets\\Meshes\\scene_{sceneName}\\scan.ply";
                meshLoader.LoadPLY(filePath);
            }
            else
            {
                Debug.LogError("Mesh loader reference not set!");
            }

            var responseMessage = new
            {
                type = "scene_loaded",
                scene_name = sceneName,
                point_count = pointCount
            };

            // Convert to JSON
            string jsonResponse = JsonConvert.SerializeObject(responseMessage);
            // Send the response if the websocket is open
            if (websocket.State == WebSocketState.Open)
            {
                Debug.Log($"Sending scene_loaded response for {sceneName}");
                await websocket.SendText(jsonResponse);
            }
            else
            {
                Debug.LogWarning("WebSocket is not open, cannot send scene_loaded response");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error handling load_scene message: {e.Message}");
        }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif
    }

    async void SendWebSocketMessage()
    {
        if (websocket.State == WebSocketState.Open)
        {
            // Sending bytes
            await websocket.Send(new byte[] { 10, 20, 30 });

            // Sending plain text
            await websocket.SendText("plain text message");
        }
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }

}