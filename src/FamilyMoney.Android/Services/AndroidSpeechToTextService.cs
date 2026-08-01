using Android;
using Android.Content;
using Android.OS;
using Android.Speech;
using FamilyMoney.Services;

namespace FamilyMoney.Android.Services;

/// <summary>
/// Распознавание речи через системный SpeechRecognizer (ru-RU).
/// </summary>
public sealed class AndroidSpeechToTextService : ISpeechToTextService
{
    public bool IsAvailable
    {
        get
        {
            var activity = MainActivity.Instance;
            return activity != null && SpeechRecognizer.IsRecognitionAvailable(activity);
        }
    }

    public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
    {
        var activity = MainActivity.Instance
            ?? throw new InvalidOperationException("Activity is not available.");

        if (!SpeechRecognizer.IsRecognitionAvailable(activity))
        {
            return null;
        }

        if (!await activity.RequestRecordAudioPermissionAsync().ConfigureAwait(true))
        {
            return null;
        }

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        await activity.RunOnUiThreadAsync(() =>
        {
            SpeechRecognizer? recognizer = null;
            RecognitionListener? listener = null;

            void Cleanup()
            {
                try
                {
                    recognizer?.Destroy();
                }
                catch
                {
                    // ignore
                }

                recognizer = null;
                listener = null;
            }

            listener = new RecognitionListener(
                onResult: text =>
                {
                    Cleanup();
                    tcs.TrySetResult(text);
                },
                onError: () =>
                {
                    Cleanup();
                    tcs.TrySetResult(null);
                });

            recognizer = SpeechRecognizer.CreateSpeechRecognizer(activity);
            if (recognizer == null)
            {
                tcs.TrySetResult(null);
                return;
            }

            recognizer.SetRecognitionListener(listener);

            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, "ru-RU");
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 3);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, false);
            intent.PutExtra(RecognizerIntent.ExtraCallingPackage, activity.PackageName);

            cancellationToken.Register(() =>
            {
                activity.RunOnUiThread(() =>
                {
                    try
                    {
                        recognizer?.Cancel();
                    }
                    catch
                    {
                        // ignore
                    }

                    Cleanup();
                    tcs.TrySetResult(null);
                });
            });

            try
            {
                recognizer.StartListening(intent);
            }
            catch
            {
                Cleanup();
                tcs.TrySetResult(null);
            }
        }).ConfigureAwait(true);

        return await tcs.Task.ConfigureAwait(true);
    }

    private sealed class RecognitionListener : Java.Lang.Object, IRecognitionListener
    {
        private readonly Action<string?> _onResult;
        private readonly Action _onError;
        private bool _completed;

        public RecognitionListener(Action<string?> onResult, Action onError)
        {
            _onResult = onResult;
            _onError = onError;
        }

        public void OnReadyForSpeech(Bundle? parameters) { }

        public void OnBeginningOfSpeech() { }

        public void OnRmsChanged(float rmsdB) { }

        public void OnBufferReceived(byte[]? buffer) { }

        public void OnEndOfSpeech() { }

        public void OnError(SpeechRecognizerError error)
        {
            CompleteError();
        }

        public void OnResults(Bundle? results)
        {
            var matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
            var text = matches?.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
            CompleteResult(text);
        }

        public void OnPartialResults(Bundle? partialResults) { }

        public void OnEvent(int eventType, Bundle? parameters) { }

        private void CompleteResult(string? text)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _onResult(text);
        }

        private void CompleteError()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            _onError();
        }
    }
}
