using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using NativeWebSocket;

public class HyperateManager : MonoBehaviour
{
    public static HyperateManager Instance { get; private set; }
    public int ConnectionAttempts { get; private set; }
    public event Action<int, float> ReadingReceived;
    public void ChangeDevice(string id)
    {
        deviceId=(id??"").Trim();
        channel="hr:"+deviceId;
        currentHeartRate=0;subscribed=false;
        socket?.CancelConnection();
        retryAt=0;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetInstance(){Instance=null;}
    void Awake()
    {
        if(Instance!=null && Instance!=this)
        {
            if(heartRateText!=null)heartRateText.enabled=false;
            enabled=false;Destroy(this);return;
        }
        Instance=this;
        DontDestroyOnLoad(gameObject);
    }
    [Header("HypeRate API")]
    public string apiKey = "";
    public string deviceId = "B78A3";
    public Text heartRateText;
    public int currentHeartRate;
    public string ConnectionStatus { get; private set; } = "Not connected";
    public bool HasLiveHeartRate => currentHeartRate > 0 && Time.realtimeSinceStartup-lastReading < 15f;
    public bool HasServerHeartbeat { get; private set; }
    public bool IsSubscribed => subscribed;
    WebSocket socket;
    bool running, connecting, connected, subscribed, sending;
    float retryAt, connectedAt, lastHeartbeat, lastReply, lastReading;
    int failures, reference;
    string channel;

    public static string BuildUrl(string key) => "wss://app.hyperate.io/socket/websocket?token=" + Uri.EscapeDataString(key.Trim());
    public static string Packet(string topic, string eventName, string reference) =>
        new JObject { ["topic"]=topic, ["event"]=eventName, ["payload"]=new JObject(), ["ref"]=reference }.ToString(Newtonsoft.Json.Formatting.None);

    void Start()
    {
        apiKey=(apiKey??"").Trim(); deviceId=(deviceId??"").Trim();
        running=true;
        if(string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deviceId))
        { running=false; SetStatus("Missing API key or device ID"); return; }
        channel="hr:"+deviceId;
        _=Connect();
    }

    async Task Connect()
    {
        if(!running || connecting)return;
        ConnectionAttempts++;
        connecting=true;connected=false;subscribed=false;currentHeartRate=0;HasServerHeartbeat=false;
        connectedAt=Time.realtimeSinceStartup;
        SetStatus("Connecting...");
        var client=new WebSocket(BuildUrl(apiKey)); socket=client;
        client.OnOpen+=()=>
        {
            if(!running || socket!=client)return;
            connected=true;
            lastHeartbeat=lastReply=Time.realtimeSinceStartup;
            SetStatus("Connected; joining watch...");
            _=Send(client,Packet(channel,"phx_join","join"));
        };
        client.OnMessage+=bytes=>
        {
            if(!running || socket!=client)return;
            HandleMessage(Encoding.UTF8.GetString(bytes));
        };
        // Do not log transport messages or URLs: they can contain the API key.
        client.OnError+=error=>{if(running && socket==client)SetStatus("Connection failed; check API key / network");};
        client.OnClose+=code=>{if(running && socket==client){connected=false;subscribed=false;currentHeartRate=0;}};
        try { await client.Connect(); }
        catch { if(running)SetStatus("Connection failed; retrying"); }
        finally
        {
            if(socket==client)
            {
                connected=false;subscribed=false;connecting=false;currentHeartRate=0;socket=null;
                if(running){retryAt=Time.realtimeSinceStartup+Mathf.Min(30f,Mathf.Pow(2,Mathf.Min(++failures,5)));SetStatus("Disconnected; retrying...");}
            }
        }
    }

    public void HandleMessage(string message)
    {
        try
        {
            var data=JObject.Parse(message);
            string topic=(string)data["topic"], eventName=(string)data["event"];
            // The live server echoes heartbeat; some Phoenix versions reply phx_reply.
            if(topic=="phoenix" && eventName=="heartbeat")
            {lastReply=Time.realtimeSinceStartup;HasServerHeartbeat=true;return;}
            if(eventName=="phx_reply")
            {
                if(topic=="phoenix"){lastReply=Time.realtimeSinceStartup;HasServerHeartbeat=true;}
                if(topic==channel)
                {
                    subscribed=(string)data["payload"]?["status"]=="ok";
                    if(subscribed){failures=0;SetStatus("Connected; waiting for watch...");}
                    else {SetStatus("Watch subscription rejected; check device ID");socket?.CancelConnection();}
                }
            }
            else if(eventName=="hr_update" && topic==channel && subscribed)
            {
                int value;
                if(int.TryParse(data["payload"]?["hr"]?.ToString(),out value) && value>0 && value<=300)
                {currentHeartRate=value;lastReading=lastReply=Time.realtimeSinceStartup;SetStatus("Live");ReadingReceived?.Invoke(value,lastReading);}
            }
            else if(eventName=="phx_error" || eventName=="phx_close")socket?.CancelConnection();
        }
        catch(Newtonsoft.Json.JsonException){SetStatus("Invalid server message ignored");}
    }

    async Task Send(WebSocket client,string message)
    {
        try { await client.SendText(message); }
        catch { if(running && socket==client)client.CancelConnection(); }
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        socket?.DispatchMessageQueue();
#endif
        if(!running)return;
        float now=Time.realtimeSinceStartup;
        if(!connecting && now>=retryAt)_=Connect();
        if(connecting && !connected && now-connectedAt>15f)socket?.CancelConnection();
        if(connected)
        {
            if(now-lastReply>45f){SetStatus("Server timed out; reconnecting...");socket?.CancelConnection();}
            else if(!sending && now-lastHeartbeat>=15f){lastHeartbeat=now;_=Heartbeat();}
            if(subscribed && !HasLiveHeartRate)
            {currentHeartRate=0;SetStatus("Connected; waiting for watch...");}
        }
        if(heartRateText!=null)heartRateText.text=HasLiveHeartRate?$"HR: {currentHeartRate} BPM":"HR: --  " + ConnectionStatus;
    }

    async Task Heartbeat()
    {
        sending=true;
        try { if(socket!=null)await Send(socket,Packet("phoenix","heartbeat",(++reference).ToString())); }
        finally {sending=false;}
    }
    void SetStatus(string value){ConnectionStatus=value;}
    void OnDestroy(){running=false;socket?.CancelConnection();socket=null;if(Instance==this)Instance=null;}
    void OnApplicationQuit(){running=false;socket?.CancelConnection();}
}
