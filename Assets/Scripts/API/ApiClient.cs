using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ApiClient : MonoBehaviour
{
    public const string DefaultServerUrl = "http://100.81.92.14:8000";
    public static ApiClient Instance { get; private set; }

    [Header("Connection Settings")]
    [Tooltip("FastAPI server URL")]
    [SerializeField] private string serverUrl = DefaultServerUrl;

    [Tooltip("API Key")]
    [SerializeField] private string apiKey = "";

    [Header("Timeout Settings")]
    [SerializeField] private int timeoutSeconds = 120;

    public string ServerUrl
    {
        get => serverUrl;
        set => serverUrl = value.TrimEnd('/');
    }

    public string ApiKey
    {
        get => apiKey;
        set => apiKey = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
    }

    public void SendChat(string message, string userId, Action<ChatResponse> onSuccess, Action<string> onError)
    {
        SendChatStreaming(message, userId, null, onSuccess, onError);
    }

    public void SendChatStreaming(string message, string userId, Action<string> onChunk, Action<ChatResponse> onComplete, Action<string> onError)
    {
        activeChatCoroutine = StartCoroutine(SendChatStreamingCoroutine(message, userId, onChunk, onComplete, onError));
    }

    public void CancelChat()
    {
        if (activeChatCoroutine != null)
        {
            StopCoroutine(activeChatCoroutine);
            activeChatCoroutine = null;
        }
    }

    private Coroutine activeChatCoroutine;
    private StringBuilder streamBuffer;
    private StringBuilder streamedText;
    private string lastCumulativeResponse;
    private string lastStreamJson;

    private IEnumerator SendChatStreamingCoroutine(string message, string userId, Action<string> onChunk, Action<ChatResponse> onComplete, Action<string> onError)
    {
        var requestBody = new ChatRequest { message = message, user_id = userId };
        string json = JsonUtility.ToJson(requestBody);
        string[] endpoints = { "/api/chat", "/unity/predict" };

        for (int i = 0; i < endpoints.Length; i++)
        {
            streamBuffer = new StringBuilder();
            streamedText = new StringBuilder();
            lastCumulativeResponse = string.Empty;
            lastStreamJson = string.Empty;
            var handler = new StreamingDownloadHandler(ProcessStreamText, onChunk);
            using (var request = new UnityWebRequest($"{serverUrl}{endpoints[i]}", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = handler;
                request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                request.SetRequestHeader("X-API-Key", apiKey);
                request.timeout = timeoutSeconds;

                yield return request.SendWebRequest();
                handler.Flush();
                ProcessStreamLine(string.Empty, true, onChunk);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onComplete?.Invoke(ParseResponse(streamedText.ToString(), lastStreamJson));
                    activeChatCoroutine = null;
                    yield break;
                }

                bool shouldRetry = request.responseCode == 404 && i < endpoints.Length - 1;
                if (!shouldRetry)
                {
                    activeChatCoroutine = null;
                    onError?.Invoke(GetErrorMessage(request));
                    yield break;
                }
            }
        }
    }

    private void ProcessStreamText(string text, Action<string> onChunk)
    {
        streamBuffer.Append(text);
        int newline;
        while ((newline = streamBuffer.ToString().IndexOf('\n')) >= 0)
        {
            string line = streamBuffer.ToString(0, newline).TrimEnd('\r');
            streamBuffer.Remove(0, newline + 1);
            ProcessStreamLine(line, false, onChunk);
        }
    }

    private void ProcessStreamLine(string line, bool flush, Action<string> onChunk)
    {
        if (flush && streamBuffer.Length > 0)
        {
            line = streamBuffer.ToString();
            streamBuffer.Clear();
        }
        if (string.IsNullOrWhiteSpace(line)) return;
        bool isDataLine = line.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
        if (isDataLine)
            line = line.Substring(5).Trim();
        else if (!line.StartsWith("{", StringComparison.Ordinal))
            return;
        if (line == "[DONE]") return;

        string text = ExtractStreamText(line);
        if (string.IsNullOrEmpty(text)) return;
        streamedText.Append(text);
        onChunk?.Invoke(text);
    }

    private string ExtractStreamText(string payload)
    {
        lastStreamJson = payload;
        try
        {
            var chunk = JsonUtility.FromJson<StreamChunk>(payload);
            if (chunk != null && !string.IsNullOrEmpty(chunk.text))
                return chunk.text;
            if (chunk != null && chunk.choices != null && chunk.choices.Length > 0 && chunk.choices[0].delta != null)
                return chunk.choices[0].delta.content ?? string.Empty;
            if (chunk != null && !string.IsNullOrEmpty(chunk.response))
            {
                string cumulative = chunk.response;
                if (cumulative == lastCumulativeResponse) return string.Empty;
                string suffix = cumulative.StartsWith(streamedText.ToString(), StringComparison.Ordinal)
                    ? cumulative.Substring(streamedText.Length)
                    : cumulative;
                lastCumulativeResponse = cumulative;
                return suffix;
            }
        }
        catch (ArgumentException) { }
        return payload;
    }

    private ChatResponse ParseResponse(string text, string lastJson)
    {
        ChatResponse response = null;
        if (!string.IsNullOrEmpty(lastJson))
        {
            try { response = JsonUtility.FromJson<ChatResponse>(lastJson); }
            catch (ArgumentException) { }
        }
        if (response == null) response = new ChatResponse();
        response.response = text;
        return response;
    }

    public void CheckHealth(Action<HealthResponse> onSuccess, Action<string> onError)
    {
        StartCoroutine(CheckHealthCoroutine(onSuccess, onError));
    }

    private IEnumerator CheckHealthCoroutine(Action<HealthResponse> onSuccess, Action<string> onError)
    {
        string[] endpoints = { "/api/health", "/health" };

        for (int i = 0; i < endpoints.Length; i++)
        {
            using (var request = UnityWebRequest.Get($"{serverUrl}{endpoints[i]}"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var response = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                        onSuccess?.Invoke(response);
                        yield break;
                    }
                    catch (Exception e)
                    {
                        onError?.Invoke("Parse error: " + e.Message);
                        yield break;
                    }
                }

                bool shouldRetry = request.responseCode == 404 && i < endpoints.Length - 1;
                if (!shouldRetry)
                {
                    onError?.Invoke(GetErrorMessage(request));
                    yield break;
                }
            }
        }
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetString("ServerUrl", serverUrl);
        PlayerPrefs.SetString("ApiKey", apiKey);
        PlayerPrefs.Save();
        Debug.Log("[ApiClient] Settings saved");
    }

    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey("ServerUrl"))
            serverUrl = PlayerPrefs.GetString("ServerUrl");
        if (PlayerPrefs.HasKey("ApiKey"))
            apiKey = PlayerPrefs.GetString("ApiKey");
    }

    private string GetErrorMessage(UnityWebRequest request)
    {
        switch (request.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                return string.IsNullOrEmpty(request.error)
                    ? "Connection error（サーバーに接続できません。バックエンド起動と URL を確認してください）"
                    : $"Connection error: {request.error}";
            case UnityWebRequest.Result.ProtocolError:
                if (request.responseCode == 403)
                    return "API Key error";
                return "Server error: " + request.responseCode;
            case UnityWebRequest.Result.DataProcessingError:
                return "Data error";
            default:
                return "Error: " + request.error;
        }
    }
}

public sealed class StreamingDownloadHandler : DownloadHandlerScript
{
    private readonly Action<string, Action<string>> onText;
    private readonly Action<string> onChunk;
    private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
    private readonly char[] charBuffer = new char[8192];

    public string LastJson { get; private set; }

    public StreamingDownloadHandler(Action<string, Action<string>> onText, Action<string> onChunk)
        : base(new byte[8192])
    {
        this.onText = onText;
        this.onChunk = onChunk;
    }

    protected override bool ReceiveData(byte[] data, int dataLength)
    {
        if (data == null || dataLength <= 0) return true;
        int charCount = decoder.GetChars(data, 0, dataLength, charBuffer, 0, false);
        if (charCount > 0)
            onText?.Invoke(new string(charBuffer, 0, charCount), onChunk);
        return true;
    }

    public void Flush()
    {
        int charCount = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, true);
        if (charCount > 0)
            onText?.Invoke(new string(charBuffer, 0, charCount), onChunk);
    }
}
