using System.Diagnostics;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Whisper.Utils;
using Button = UnityEngine.UI.Button;
using Toggle = UnityEngine.UI.Toggle;

namespace Whisper.Samples
{
    /// <summary>
    /// Record audio clip from microphone and make a transcription.
    /// </summary>
    public class MicrophoneDemo : MonoBehaviour
    {
        [Header("Cloud Server API")]
        public string serverTranscribeUrl = "";

        [Header("Scripts")]
        public MicrophoneRecord microphoneRecord;


        public bool streamSegments = true;
        public bool printLanguage = true;

        [Header("UI")] 
        public Button button;
        public Text buttonText;
        public Text outputText;
        public Text timeText;
        public Dropdown languageDropdown;
        public Toggle translateToggle;
        public Toggle vadToggle;
        public ScrollRect scroll;
        
        private string _buffer;

        private void Awake()
        {
            //whisper.OnNewSegment += OnNewSegment;
            //whisper.OnProgress += OnProgressHandler;
            
            microphoneRecord.OnRecordStop += OnRecordStop;
            
            button.onClick.AddListener(OnButtonPressed);
            //languageDropdown.value = languageDropdown.options
            //    .FindIndex(op => op.text == whisper.language);
            //languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            //translateToggle.isOn = whisper.translateToEnglish;
            //translateToggle.onValueChanged.AddListener(OnTranslateChanged);

            vadToggle.isOn = microphoneRecord.vadStop;
            vadToggle.onValueChanged.AddListener(OnVadChanged);
        }

        private void OnVadChanged(bool vadStop)
        {
            microphoneRecord.vadStop = vadStop;
        }

        private void OnButtonPressed()
        {
            if (!microphoneRecord.IsRecording)
            {
                //whisper.initialPrompt = "Cube, Sphere, Cylinder, Capsule, Spawn, Create, Make,Delete, Remove, Red, Blue, Green, Metal, Wood, Search Wikipedia, Information";

                microphoneRecord.StartRecord();
                buttonText.text = "Stop";
                if (outputText) outputText.text = "Listening...";
            }
            else
            {
                microphoneRecord.StopRecord();
                buttonText.text = "Record";
                if (outputText) outputText.text = "Uploading to Cloud...";
            }
        }
        
        private async void OnRecordStop(AudioChunk recordedAudio)
        {
            buttonText.text = "Record";
            //_buffer = "";

            var sw = new Stopwatch();
            sw.Start();
            
            //var res = await whisper.GetTextAsync(recordedAudio.Data, recordedAudio.Frequency, recordedAudio.Channels);
            //if (res == null || !outputText) 
            //    return;

            //var time = sw.ElapsedMilliseconds;
            //var rate = recordedAudio.Length / (time * 0.001f);
            //timeText.text = $"Time: {time} ms\nRate: {rate:F1}x";

            //var text = res.Result;
            //if (printLanguage)
            //    text += $"\n\nLanguage: {res.Language}";
            
            //outputText.text = text;
            //UiUtils.ScrollDown(scroll);

            //if (AppManager.Instance != null)
            //{
            //    AppManager.Instance.ProcessVoiceCommand(res.Result);
            //}

            StartCoroutine(SendAudioToColab(recordedAudio, sw));

        }

        private IEnumerator SendAudioToColab(AudioChunk audioChunk, Stopwatch sw)
        {
            // 1. Convert the raw float data into a standard WAV byte array
            byte[] wavData = ConvertToWav(audioChunk.Data, audioChunk.Frequency, audioChunk.Channels);

            // 2. Package it into a web form
            WWWForm form = new WWWForm();
            form.AddBinaryData("audio", wavData, "recording.wav", "audio/wav");

            // 3. Post to Colab Server
            using (UnityWebRequest request = UnityWebRequest.Post(serverTranscribeUrl, form))
            {
                yield return request.SendWebRequest();
                sw.Stop();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    // Parse the JSON returned from the Python server
                    string jsonResponse = request.downloadHandler.text;
                    string textResult = ExtractTextFromJson(jsonResponse);

                    // Update UI Stats
                    var time = sw.ElapsedMilliseconds;
                    var rate = audioChunk.Length / (time * 0.001f);

                    if (timeText) timeText.text = $"Cloud Latency: {time} ms\nRate: {rate:F1}x";
                    if (outputText) outputText.text = textResult;
                    UiUtils.ScrollDown(scroll);

                    // Send the text to your NLP Spawner!
                    if (AppManager.Instance != null)
                    {
                        AppManager.Instance.ProcessVoiceCommand(textResult);
                    }
                }
                else
                {
                    string errorMsg = $"[CLOUD ERROR] {request.error}";
                    if (outputText) outputText.text = errorMsg;
                    UnityEngine.Debug.LogError(errorMsg);
                }
            }
        }

        private string ExtractTextFromJson(string json)
        {
            var match = System.Text.RegularExpressions.Regex.Match(json, @"""text""\s*:\s*""(.*)""");
            if (match.Success) return match.Groups[1].Value;
            return json;
        }

        // Transforms Unity audio float arrays into network-ready WAV files
        private byte[] ConvertToWav(float[] samples, int frequency, int channels)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples.Length * 2);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples.Length * 2);

                foreach (float sample in samples)
                {
                    short intSample = (short)(Mathf.Clamp(sample, -1f, 1f) * 32767);
                    writer.Write(intSample);
                }
                return stream.ToArray();
            }
        }

        //private void OnLanguageChanged(int ind)
        //{
        //    var opt = languageDropdown.options[ind];
        //    whisper.language = opt.text;
        //}

        //private void OnTranslateChanged(bool translate)
        //{
        //    whisper.translateToEnglish = translate;
        //}

        //private void OnProgressHandler(int progress)
        //{
        //    if (!timeText)
        //        return;
        //    timeText.text = $"Progress: {progress}%";
        //}

        //private void OnNewSegment(WhisperSegment segment)
        //{
        //    if (!streamSegments || !outputText)
        //        return;

        //    _buffer += segment.Text;
        //    outputText.text = _buffer + "...";
        //    UiUtils.ScrollDown(scroll);
        //}
    }
}