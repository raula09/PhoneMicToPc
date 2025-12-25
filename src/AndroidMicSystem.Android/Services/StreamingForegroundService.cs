using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidMicSystem.Core.Audio;
using AndroidMicSystem.Core.Services;
using AndroidMicSystem.Android.AudioCapture;
using System.Net;

namespace AndroidMicSystem.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMicrophone)]
public class StreamingForegroundService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "AndroidMicSystemChannel";
    private const string ChannelName = "Android Mic System";
    
    private StreamingService? _streamingService;
    private AndroidAudioCapture? _audioCapture;
    private bool _isStreaming;
    
    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }
    
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent == null)
            return StartCommandResult.NotSticky;
            
        var action = intent.Action;
        
        switch (action)
        {
            case "START_STREAMING":
                var serverAddress = intent.GetStringExtra("ServerAddress");
                if (!string.IsNullOrEmpty(serverAddress))
                {
                    StartStreaming(serverAddress);
                }
                break;
                
            case "STOP_STREAMING":
                StopStreaming();
                break;
        }
        
        return StartCommandResult.Sticky;
    }
    
    private void StartStreaming(string serverAddress)
    {
        if (_isStreaming)
            return;
            
        try
        {
            // Create notification
            CreateNotificationChannel();
            var notification = CreateNotification("Streaming audio...");
            StartForeground(NotificationId, notification);
            
            // Initialize streaming service
            _streamingService = new StreamingService();
            
            // Initialize audio capture
            var audioSettings = new AudioSettings
            {
                SampleRate = 48000,
                Channels = 1,
                BitsPerSample = 16
            };
            
            _audioCapture = new AndroidAudioCapture(audioSettings);
            
            if (!_audioCapture.Initialize())
            {
                throw new Exception("Failed to initialize audio capture");
            }
            
            // Setup event handlers
            _audioCapture.AudioDataCaptured += OnAudioDataCaptured;
            _audioCapture.Error += OnError;
            
            // Connect to server
            var connectTask = _streamingService.ConnectToServerAsync(serverAddress, 5001);
            connectTask.ContinueWith(async task =>
            {
                if (task.Result)
                {
                    // Start streaming
                    await _streamingService.StartStreamingAsync(serverAddress, 5000);
                    
                    // Start audio capture
                    await _audioCapture.StartRecordingAsync();
                    
                    _isStreaming = true;
                    UpdateNotification("Connected and streaming");
                }
                else
                {
                    StopStreaming();
                }
            });
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("AndroidMicSystem", $"Error starting streaming: {ex.Message}");
            StopStreaming();
        }
    }
    
    private void StopStreaming()
    {
        _isStreaming = false;
        
        _audioCapture?.StopRecording();
        _audioCapture?.Dispose();
        _audioCapture = null;
        
        _streamingService?.Disconnect();
        _streamingService?.Dispose();
        _streamingService = null;
        
        StopForeground(true);
        StopSelf();
    }
    
    private async void OnAudioDataCaptured(byte[] audioData)
    {
        if (_streamingService != null && _isStreaming)
        {
            await _streamingService.SendAudioAsync(audioData);
        }
    }
    
    private void OnError(Exception ex)
    {
        global::Android.Util.Log.Error("AndroidMicSystem", $"Audio capture error: {ex.Message}");
        UpdateNotification($"Error: {ex.Message}");
    }
    
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Low)
            {
                Description = "Android Microphone System notifications"
            };
            
            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }
    }
    
    private Notification CreateNotification(string contentText)
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Android Mic System")
            .SetContentText(contentText)
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuMicrophone)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow);
        
        return builder.Build();
    }
    
    private void UpdateNotification(string contentText)
    {
        var notification = CreateNotification(contentText);
        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.Notify(NotificationId, notification);
    }
    
    public override void OnDestroy()
    {
        StopStreaming();
        base.OnDestroy();
    }
}