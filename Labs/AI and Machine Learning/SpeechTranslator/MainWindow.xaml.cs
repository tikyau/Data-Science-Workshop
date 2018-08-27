using Microsoft.MT.Api;
using Microsoft.MT.Api.TestUtils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;

namespace SpeechTranslator
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum UiState //list of items for use in the code
        {
            GettingLanguageList,
            MissingLanguageList,
            ReadyToConnect,
            Connecting,
            Connected,
            Disconnecting,
            InvalidCredentials
        }


        private enum MessageKind  //list of items for use throughout the code
        {
            Chat, // Translate mode
            ChatDetect1, // Detect and translate - result for first language
            ChatDetect2, // Detect and translate - result for second language
            Error,
            Status
        }

        private UiState currentState; //create a variable of enum type UiState

        private Dictionary<string, string> spokenLanguages; //create dictionary for the speech translation langauges 
        private Dictionary<string, string> textLanguages; //create dictionary for the text translation languages
        private Dictionary<string, bool> isLTR; //If this language ID is LTR or RTL
        private Dictionary<string, List<TTsDetail>> voices; //convert a list into a dictionary and call it voices TTsDetails is a class in this file
        private Dictionary<string, string> fromLanguages; //hold the mapping from spokenLangauge IDs (en-US) to textLanguageIDs (en). Only purpose is to maintain the set of Bidi languages.


        private WaveIn recorder; //WaveIn is a class

        private WaveFileWriter audioSent; //WaveFileWriter is a class

        private int audioBytesSent = 0;

        private BinaryMessageDecoder audioReceived; //BinaryMessageDecoder is a testutils class

        private string correlationId;

        private SpeechClient s2smtClient; //SpeechClient is a class

        private WaveOut player; //WaveOut is a class

        private BufferedWaveProvider playerTextToSpeechWaveProvider; //BufferedWaveProvider is a class

        private BufferedWaveProvider playerAudioInputWaveProvider;

        private string finaltranslationhistory = "";

        private int textToSpeechBytes = 0;

        // If (DateTime.Now < suspendInputAudioUntil) then ignore input audio to avoid echo.
        private DateTime suspendInputAudioUntil = DateTime.MinValue; // DateTime is a struct - DateTime.MinValue represents the smallest possible value of dateTime

        private CancellationTokenSource streamAudioFromFileInterrupt = null; //CancellationTokenSource is a class

        // When auto-saving, save the slice Logs.Items[autoSaveFrom:]
        private int autoSaveFrom = 0;

        private string baseUrl = "dev.microsofttranslator.com";

        private class TTsDetail
        {
            public string Code { get; set; }
            public string DisplayName { get; set; }
        }

        private MiniWindow miniwindow = new MiniWindow();

        private int screennumber = 0;   //keeps track of the screen # the miniwindow is positioned on

        private int resetcycle = 0;     //keeps track of cycling through the window positions

        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Holds one utterance for the transcript
        /// </summary>
        private class TranscriptUtterance
        {
            public TimeSpan Timespan;
            public string Recognition;
            public string Translation;
        }

        /// <summary>
        /// Holds the set of utterances in this conversation;
        /// </summary>
        private List<TranscriptUtterance> Transcript = new List<TranscriptUtterance>();


        public MainWindow()
        {
            InitializeComponent();
            Debug.Print("This is a debug message");

            Closing += MainWindow_Closing;
            if (miniwindow != null) miniwindow.Closing += Miniwindow_Closing;
            this.PreviewKeyDown += MainWindow_PreviewKeyDown;

            int waveInDevices = WaveIn.DeviceCount; //how many recording devices are there on the device
            for (int waveInDevice = 0; waveInDevice < waveInDevices; waveInDevice++) //loop through and find all of the devices
            {
                WaveInCapabilities deviceInfo = WaveIn.GetCapabilities(waveInDevice);
                Mic.Items.Add(new ComboBoxItem() { Content = deviceInfo.ProductName, Tag = waveInDevice }); //add the devices to the combo box to show the user
            }
            // Special case: audio source is a file
            Mic.Items.Add(new ComboBoxItem() { Content = "Play audio from file", Tag = "File" });

            Mic.SelectedIndex = Properties.Settings.Default.MicIndex;

            int waveOutDevices = WaveOut.DeviceCount; //get the waveout device count
            for (int waveOutDevice = 0; waveOutDevice < waveOutDevices; waveOutDevice++) //get all the wavout audio devices on the device and put them in a combo box
            {
                WaveOutCapabilities deviceInfo = WaveOut.GetCapabilities(waveOutDevice);
                Speaker.Items.Add(new ComboBoxItem() { Content = deviceInfo.ProductName, Tag = waveOutDevice });
            }

            Speaker.SelectedIndex = Properties.Settings.Default.SpeakerIndex;
            
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "1" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "2" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "3" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "4" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "5" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "6" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "7" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "8" });
            MiniWindow_Lines.Items.Add(new ComboBoxItem() { Content = "9" });

            MiniWindow_Lines.SelectedIndex = Properties.Settings.Default.MiniWindow_Lines;

            
            ShowMiniWindow.IsChecked = Properties.Settings.Default.ShowMiniWindow;
            if (ShowMiniWindow.IsChecked.Value) UpdateMiniWindowUI(MiniWindowUIState.open);
            else UpdateMiniWindowUI(MiniWindowUIState.closed);

            FeatureTTS.IsChecked = Properties.Settings.Default.TTS;
            CutInputAudioCheckBox.IsChecked = Properties.Settings.Default.CutInputDuringTTS;
            FeaturePartials.IsChecked = Properties.Settings.Default.PartialResults;
            Voice.SelectedIndex = Properties.Settings.Default.VoiceIndex;
            MenuItem_Experimental.IsChecked = Properties.Settings.Default.ExperimentalLanguages;

            UpdateLanguageSettings(); 

        }

        private void MainWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F5) UpdateLanguageSettings();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Disconnect();
            miniwindow.Closing -= Miniwindow_Closing;
            if (miniwindow != null) miniwindow.Close();
            Properties.Settings.Default.ShowMiniWindow = ShowMiniWindow.IsChecked.Value;
            Properties.Settings.Default.TTS = FeatureTTS.IsChecked.Value;
            Properties.Settings.Default.CutInputDuringTTS = CutInputAudioCheckBox.IsChecked.Value;
            Properties.Settings.Default.PartialResults = FeaturePartials.IsChecked.Value;
            Properties.Settings.Default.FromLanguageIndex = FromLanguage.SelectedIndex;
            Properties.Settings.Default.ToLanguageIndex = ToLanguage.SelectedIndex;
            Properties.Settings.Default.VoiceIndex = Voice.SelectedIndex;
            Properties.Settings.Default.ExperimentalLanguages = MenuItem_Experimental.IsChecked;
            Properties.Settings.Default.Save();
            Environment.Exit(0);
        }

        /// <summary>
        /// Retrieve the language names and IDs from the Translator service
        /// </summary>
        private void UpdateLanguageSettings()
        {
            Task<bool> checkcredentialsTask = IsValidCredentialsAsync();
            UpdateUiState(UiState.GettingLanguageList);
            UpdateLanguageSettingsAsync().ContinueWith(async (t) =>
                {
                    var state = UiState.ReadyToConnect;
                    if (t.IsFaulted || t.IsCanceled)
                    {
                        state = UiState.MissingLanguageList;
                        this.Log(t.Exception, "E: Failed to get language list: {0}", t.IsCanceled ? "Timeout" : "");
                    }
                    if (await checkcredentialsTask) SafeInvoke(() => UpdateUiState(state));
                    else SafeInvoke(() => UpdateUiState(UiState.InvalidCredentials));
                });
        }

        private static string GetRequestId(HttpResponseMessage response)
        {
            IEnumerable<string> keys = null;
            if (!response.Headers.TryGetValues("X-RequestId", out keys))
                return null;
            return keys.First();
        }

        private async Task UpdateLanguageSettingsAsync()
        {
            Uri baseUri = new Uri("https://" + baseUrl);
            string fullUriString = "/Languages?api-version=1.0&scope=text,speech,tts";
            if (MenuItem_Experimental.IsChecked) fullUriString += "&flight=experimental";            
            Uri fullUri = new Uri(baseUri, fullUriString);

            using (HttpClient client = new HttpClient()) //'client' is the var - using statement ensures the dispose method is used even after an exception.
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, fullUri);

                // get language names for current UI culture:
                request.Headers.Add("Accept-Language", CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

                // add a client-side trace Id. In case of issues, one can contact support and provide this:
                //string traceId = "SpeechTranslator" + Guid.NewGuid().ToString();
                //request.Headers.Add("X-ClientTraceId", traceId);
                //Debug.Print("TraceId: {0}", traceId);

                client.Timeout = TimeSpan.FromMilliseconds(10000);
                HttpResponseMessage response = await client.SendAsync(request); //make the async call to the web using the client var and passing the built up URI
                response.EnsureSuccessStatusCode(); //causes exception if the return is false

                Debug.Print("Request Id returned: {0}", GetRequestId(response));

                //create dictionaries to hold the language specific data
                spokenLanguages = new Dictionary<string, string>();
                fromLanguages = new Dictionary<string, string>();
                textLanguages = new Dictionary<string, string>();
                isLTR = new Dictionary<string, bool>();
                voices = new Dictionary<string, List<TTsDetail>>();

                JObject jResponse = JObject.Parse(await response.Content.ReadAsStringAsync()); //get the json from the async call with the response var created above, parse it and put it in a var called jResponse - JObject is a newton class

                //Gather the set of TTS voices
                foreach (JProperty jTts in jResponse["tts"])
                {
                    JObject ttsDetails = (JObject)jTts.Value;

                    string code = jTts.Name;
                    string language = ttsDetails["language"].ToString();
                    string displayName = ttsDetails["displayName"].ToString();
                    string gender = ttsDetails["gender"].ToString();

                    if (!voices.ContainsKey(language)) //check dictionary for a specific key value
                    {
                        voices.Add(language, new List<TTsDetail>()); //add to the dictionary the locale key and a ttsDetail object
                    }

                    voices[language].Add(new TTsDetail() { Code = code, DisplayName = string.Format("{0} ({1})", displayName, gender) });
                }

                // Gather the set of speech translation languages
                foreach (JProperty jSpeech in jResponse["speech"])
                {
                    JObject languageDetails = (JObject)jSpeech.Value;
                    string code = jSpeech.Name;
                    string simplecode = languageDetails["language"].ToString();
                    string displayName = languageDetails["name"].ToString();
                    spokenLanguages.Add(code, displayName);
                    fromLanguages.Add(code,simplecode);
                }

                spokenLanguages = spokenLanguages.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                FromLanguage.Items.Clear();
                foreach (var language in spokenLanguages)
                {
                    bool isSelected = (CultureInfo.CurrentUICulture.Name.Equals(language.Key, StringComparison.OrdinalIgnoreCase)) ? true : false;
                    FromLanguage.Items.Add(new ComboBoxItem() { Content = language.Value, Tag = language.Key, IsSelected = isSelected});
                }

                // Gather the set of text translation languages
                foreach (JProperty jText in jResponse["text"])
                {
                    JObject languageDetails = (JObject)jText.Value;
                    string code = jText.Name;
                    string displayName = languageDetails["name"].ToString();
                    textLanguages.Add(code, displayName);

                    string direction = languageDetails["dir"].ToString().ToLowerInvariant();
                    bool LTR = true;
                    if (direction.ToLowerInvariant() == "rtl") LTR = false;
                    isLTR.Add(code, LTR);
                }

                textLanguages = textLanguages.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
                ToLanguage.Items.Clear();
                foreach (var language in textLanguages)
                {
                    ToLanguage.Items.Add(new ComboBoxItem() { Content = language.Value, Tag = language.Key });
                }

                if (Properties.Settings.Default.FromLanguageIndex >= 0) FromLanguage.SelectedIndex = Properties.Settings.Default.FromLanguageIndex;
                if (Properties.Settings.Default.ToLanguageIndex >= 0) ToLanguage.SelectedIndex = Properties.Settings.Default.ToLanguageIndex;
                else
                {
                    Random rnd = new Random(DateTime.Now.Millisecond);
                    ToLanguage.SelectedIndex = (rnd.Next() % textLanguages.Count);
                }
            }
        }

        private void ToLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var voiceCombo = this.Voice;
            this.UpdateVoiceComboBox(voiceCombo, ToLanguage.SelectedItem as ComboBoxItem);
            string code = ((ComboBoxItem)this.ToLanguage.SelectedItem).Tag.ToString();
            miniwindow.DisplayText.Language = System.Windows.Markup.XmlLanguage.GetLanguage(code);
            bool LTR = true;
            isLTR.TryGetValue(code, out LTR);
            if (LTR)
            {
                miniwindow.DisplayText.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                miniwindow.DisplayText.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
                DialogTranslation.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                DialogTranslation.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            }
            else
            {
                miniwindow.DisplayText.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                miniwindow.DisplayText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
                DialogTranslation.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                DialogTranslation.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }
            finaltranslationhistory = "";
            miniwindow.DisplayText.Text = "";
            Transcript.Clear();
        }

        private void FromLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.FromLanguageIndex = FromLanguage.SelectedIndex;
            string code = ((ComboBoxItem)this.FromLanguage.SelectedItem).Tag.ToString();
            fromLanguages.TryGetValue(code, out code);  //map fully specified language code "en-us" to simple language code "en"
            bool LTR = true;
            if (isLTR.Count > 1) isLTR.TryGetValue(code, out LTR);
            if (LTR)
            {
                DialogRecognition.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                DialogRecognition.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            }
            else
            {
                DialogRecognition.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                DialogRecognition.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            }
            Transcript.Clear();
        }

        private void UpdateVoiceComboBox(System.Windows.Controls.ComboBox voiceComboBox, ComboBoxItem languageSelectedItem)
        {
            voiceComboBox.Items.Clear();
            if (languageSelectedItem != null)
            {
                if (voices.ContainsKey(languageSelectedItem.Tag.ToString())) 
                {
                    var selectedVoice = voices[languageSelectedItem.Tag.ToString()];
                    foreach (var voice in selectedVoice)
                    {
                        voiceComboBox.Items.Add(new ComboBoxItem() { Content = voice.DisplayName, Tag = voice.Code });
                    }
                    voiceComboBox.SelectedIndex = Math.Min(Math.Abs(Voice.SelectedIndex), voiceComboBox.Items.Count-1);
                    FeatureTTS.IsEnabled = true;
                    CutInputAudioCheckBox.IsEnabled = true;
                    voiceComboBox.IsEnabled = true;
                }
                else
                {
                    FeatureTTS.IsEnabled = false;
                    CutInputAudioCheckBox.IsEnabled = false;
                    voiceComboBox.IsEnabled = false;
                }
            }
        }
        private void Mic_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = Mic.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string tag = selectedItem.Tag as string;
                this.AudioFileInput.Visibility = (tag == "File") ? Visibility.Visible : Visibility.Collapsed;
                this.AudioFileInputButton.Visibility = this.AudioFileInput.Visibility;
                Properties.Settings.Default.MicIndex = Mic.SelectedIndex;
            }
        }

        private void AudioFileInputButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog(); //**this code opens the file UI for file selection
            dialog.InitialDirectory = Properties.Settings.Default.OutputDirectory;
            dialog.Filter = "wav files (*.wav)|*.wav|All files (*.*)|*.*";
            dialog.FilterIndex = 1;
            dialog.RestoreDirectory = true;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                this.AudioFileInput.Text = dialog.FileName;
            }
        }

        private void StartListening_Click(object sender, RoutedEventArgs e) //this either starts the connection by calling Connect() or Disconnects by calling Disconnect()
        {
            switch (this.currentState)
            {
                case UiState.ReadyToConnect:
                    stopwatch.Start();
                    Connect();
                    break;
                case UiState.Connected:
                    stopwatch.Stop();
                    Disconnect();
                    break;
                default:
                    return;
            }
        }

        private async Task ConnectAsync(SpeechClientOptions options, bool suspendInputAudioDuringTTS)
        {
            await ADMAuthenticate(options);

            // Create the client
            TextMessageDecoder textDecoder;

            if (options.GetType() == typeof(SpeechTranslateClientOptions))
            {
                s2smtClient = new SpeechClient((SpeechTranslateClientOptions)options, CancellationToken.None);
                textDecoder = TextMessageDecoder.CreateTranslateDecoder();
            }
            else
            {
                throw new InvalidOperationException("Type of SpeechClientOptions is not supported.");
            }

            s2smtClient.OnBinaryData += (c, a) => { AddSamplesToPlay(a, suspendInputAudioDuringTTS); };
            s2smtClient.OnEndOfBinaryData += (c, a) => { AddSamplesToPlay(a, suspendInputAudioDuringTTS); };
            s2smtClient.OnTextData += (c, a) => { textDecoder.AppendData(a); };
            s2smtClient.OnEndOfTextData += (c, a) =>
            {
                textDecoder.AppendData(a);
                textDecoder
                    .Decode()
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Log(t.Exception, "E: Failed to decode incoming text message.");
                        }
                        else
                        {
                            object msg = t.Result;
                            if (msg.GetType() == typeof(FinalResultMessage))
                            {
                                var final = msg as FinalResultMessage;
                                Log("Final recognition {0}: {1}", final.Id, final.Recognition);
                                Log("Final translation {0}: {1}", final.Id, final.Translation);
                                SafeInvoke(() => SetMessage(final.Recognition, final.Translation, MessageKind.Chat));
                                finaltranslationhistory = final.Translation + "\n" + finaltranslationhistory.Substring(0, Math.Min(500, finaltranslationhistory.Length));
                                TranscriptUtterance utterance = new TranscriptUtterance();
                                utterance.Recognition = final.Recognition;
                                utterance.Translation = final.Translation;
                                utterance.Timespan = stopwatch.Elapsed;
                                Transcript.Add(utterance);
                            }
                            if (msg.GetType() == typeof(PartialResultMessage))
                            {
                                var partial = msg as PartialResultMessage;
                                Log("Partial recognition {0}: {1}", partial.Id, partial.Recognition);
                                Log("Partial translation {0}: {1}", partial.Id, partial.Translation);
                                SafeInvoke(() => SetMessage(partial.Recognition, partial.Translation, MessageKind.Chat));
                            }
                        }
                    });
            };
            s2smtClient.Failed += (c, ex) =>
            {
                Log(ex, "E: SpeechTranslation client reported an error.");
            };
            s2smtClient.Disconnected += (c, ea) =>
            {
                SafeInvoke(() =>
                {
                    // We only care to react to server disconnect when our state is Connected. 
                    if (currentState == UiState.Connected)
                    {
                        Log("E: Connection has been lost.");
                        Disconnect();
                    }
                });
            };
            await s2smtClient.Connect();
        }

        private async Task ADMAuthenticate(SpeechClientOptions options)
        {
            // Authenticate
            string admClientId = Properties.Settings.Default.ClientID;
            Microsoft.Translator.API.AzureAuthToken tokenSource = new Microsoft.Translator.API.AzureAuthToken(admClientId);
            options.AuthHeaderValue = await tokenSource.GetAccessTokenAsync();
            if (options.AuthHeaderValue.Length < 10)
            {
                UpdateUiState(UiState.InvalidCredentials);
            }
        }

        private async Task<bool> IsValidCredentialsAsync()
        {
            // Authenticate
            string admClientId = Properties.Settings.Default.ClientID;
            Microsoft.Translator.API.AzureAuthToken tokenSource = new Microsoft.Translator.API.AzureAuthToken(admClientId);
            string token = await tokenSource.GetAccessTokenAsync();
            if (token.Length>10) return true;
            else return false;
        }


        private bool IsMissingInput(object item, string name)
        {
            ComboBoxItem cboItem = item as ComboBoxItem;
            if (item == null)
            {
                SetMessage(String.Format("No {0} selected.", name), "", MessageKind.Error);
                UpdateUiState(UiState.ReadyToConnect);
                return true;
            }
            return false;
        }

        private void Connect()
        {
            if (this.currentState != UiState.ReadyToConnect) return;

            Stopwatch watch = Stopwatch.StartNew();
            UpdateUiState(UiState.Connecting);

            //This section is putting default values in case there are missing values in the UI
            // Minimal validation
            if (IsMissingInput(FromLanguage.SelectedItem, "source language")) return;
            if (IsMissingInput(ToLanguage.SelectedItem, "target language")) return;
            //if (this.IsMissingInput(this.Voice.SelectedItem, "voice")) return;
            if (IsMissingInput(Mic.SelectedItem, "microphone")) return;
            if (IsMissingInput(Speaker.SelectedItem, "speaker")) return;

            if (LogAutoSave.IsChecked)
            {
                autoSaveFrom = Logs.Items.Count;
            }

            string tag = ((ComboBoxItem)Mic.SelectedItem).Tag as string;
            string audioFileInputPath = null;
            if (tag == "File")
            {
                audioFileInputPath = this.AudioFileInput.Text;
                if (!File.Exists(audioFileInputPath))
                {
                    SetMessage(String.Format("Invalid audio source: selected file does not exist."), "", MessageKind.Error);
                    UpdateUiState(UiState.ReadyToConnect);
                    return;
                }
            }
            bool shouldSuspendInputAudioDuringTTS = this.CutInputAudioCheckBox.IsChecked.HasValue ? this.CutInputAudioCheckBox.IsChecked.Value : false;

            correlationId = Guid.NewGuid().ToString("D").Split('-')[0].ToUpperInvariant();

            // Setup speech translation client options
            SpeechClientOptions options;

            string voicename = "";
            if (this.Voice.SelectedItem != null)
            {
                voicename = ((ComboBoxItem)this.Voice.SelectedItem).Tag.ToString();
            }
            options = new SpeechTranslateClientOptions()
            {
                TranslateFrom = ((ComboBoxItem)this.FromLanguage.SelectedItem).Tag.ToString(),
                TranslateTo = ((ComboBoxItem)this.ToLanguage.SelectedItem).Tag.ToString(),
                Voice = voicename,
            };
            
            options.Hostname = baseUrl;
            options.AuthHeaderKey = "Authorization";
            options.AuthHeaderValue = ""; // set later in ConnectAsync.
            options.ClientAppId = new Guid("EA66703D-90A8-436B-9BD6-7A2707A2AD99");
            options.CorrelationId = this.correlationId;
            options.Features = GetFeatures().ToString().Replace(" ", "");
            options.Profanity = ((SpeechClient.ProfanityFilter)Enum.Parse(typeof(SpeechClient.ProfanityFilter), GetProfanityLevel(), true)).ToString();
            options.Experimental = MenuItem_Experimental.IsChecked;

            // Setup player and recorder but don't start them yet.
            WaveFormat waveFormat = new WaveFormat(16000, 16, 1);

            // WaveProvider for incoming TTS
            // We use a rather large BufferDuration because we need to be able to hold an entire utterance.
            // TTS audio is received in bursts (faster than real-time).
            textToSpeechBytes = 0;
            playerTextToSpeechWaveProvider = new BufferedWaveProvider(waveFormat);
            playerTextToSpeechWaveProvider.BufferDuration = TimeSpan.FromMinutes(5);

            ISampleProvider sampleProvider = null;
            if (audioFileInputPath != null)
            {
                // Setup mixing of audio from input file and from TTS
                playerAudioInputWaveProvider = new BufferedWaveProvider(waveFormat);
                var srce1 = new Pcm16BitToSampleProvider(playerTextToSpeechWaveProvider);
                var srce2 = new Pcm16BitToSampleProvider(playerAudioInputWaveProvider);
                var mixer = new MixingSampleProvider(srce1.WaveFormat);
                mixer.AddMixerInput(srce1);
                mixer.AddMixerInput(srce2);
                sampleProvider = mixer;
            }
            else
            {
                recorder = new WaveIn();
                recorder.DeviceNumber = (int)((ComboBoxItem)Mic.SelectedItem).Tag;
                recorder.WaveFormat = waveFormat;
                recorder.DataAvailable += OnRecorderDataAvailable;
                sampleProvider = playerTextToSpeechWaveProvider.ToSampleProvider();
            }

            player = new WaveOut();
            player.DeviceNumber = (int)((ComboBoxItem)Speaker.SelectedItem).Tag;
            player.Init(sampleProvider);

            this.audioBytesSent = 0;

            string logAudioFileName = null;
            if (LogSentAudio.IsChecked|| LogReceivedAudio.IsChecked)
            {
                string logAudioPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Properties.Settings.Default.OutputDirectory);
                try
                {
                    Directory.CreateDirectory(logAudioPath);
                }
                catch
                {
                    this.AddItemToLog(string.Format("Could not create folder {0}", logAudioPath));
                }

                if (LogSentAudio.IsChecked)
                {
                    logAudioFileName = System.IO.Path.Combine(logAudioPath, string.Format("audiosent_{0}.wav", this.correlationId));
                }

                if (LogReceivedAudio.IsChecked)
                {
                    string fmt = System.IO.Path.Combine(logAudioPath, string.Format("audiotts_{0}_{{0}}.wav", this.correlationId));
                    this.audioReceived = new BinaryMessageDecoder(fmt);
                }
            }


            ConnectAsync(options, shouldSuspendInputAudioDuringTTS).ContinueWith((t) =>
            {
                if (t.IsFaulted || t.IsCanceled || !s2smtClient.IsConnected()) //t.isfaulted OR t.iscancelled OR NOT s2smtclient.isconnected() do the following
                {
                    Log(t.Exception, "E: Unable to connect: cid='{0}', elapsedMs='{1}'.",
                        this.correlationId, watch.ElapsedMilliseconds);
                    this.SafeInvoke(() => {
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                }
                else
                {
                    // Start playing incoming audio
                    player.Play();
                    // Start recording and sending
                    if (logAudioFileName != null)
                    {
                        audioSent = new WaveFileWriter(logAudioFileName, waveFormat);
                        this.Log("I: Recording outgoing audio in {0}", logAudioFileName);
                    }
                    // Send the WAVE header
                    s2smtClient.SendBinaryMessage(new ArraySegment<byte>(GetWaveHeader(waveFormat)));
                    if (audioFileInputPath != null)
                    {
                        streamAudioFromFileInterrupt = new CancellationTokenSource();
                        Task.Run(() => this.StreamFile(audioFileInputPath, streamAudioFromFileInterrupt.Token))
                            .ContinueWith((x) =>
                            {
                                if (x.IsFaulted)
                                {
                                    Log(x.Exception, "E: Error while playing audio from input file.");
                                }
                                else
                                {
                                    this.Log("I: Done playing audio from input file.");
                                }
                            });
                    }
                    else
                    {
                        // Start sending audio from the recoder.
                        recorder.StartRecording();
                    }
                    this.Log("I: Connected: cid='{0}', elapsedMs='{1}'.",
                        this.correlationId, watch.ElapsedMilliseconds);
                    this.SafeInvoke(() => this.UpdateUiState(UiState.Connected));
                }
            }).ContinueWith((t) => {
                if (t.IsFaulted)
                {
                    Log(t.Exception, "E: Failed to start sending audio.");
                    this.SafeInvoke(() => { 
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                }
            });
        }

        private void StreamFile(string path, CancellationToken token)
        {
            var audioSource = new AudioSourceCollection(new IAudioSource[] {
                new WavFileAudioSource(path, true),
                new WavSilenceAudioSource(2000),
            });

            int audioChunkSizeInMs = 100;
            var handle = new AutoResetEvent(true);
            long audioChunkSizeInTicks = TimeSpan.TicksPerMillisecond * (long)(audioChunkSizeInMs);
            long tnext = DateTime.Now.Ticks + audioChunkSizeInMs;
            int wait = audioChunkSizeInMs;
            foreach (var chunk in audioSource.Emit(audioChunkSizeInMs))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                // Send chunk to speech translation service
                this.OnAudioDataAvailable(chunk);
                // Send chunk to local audio player via the mixer
                playerAudioInputWaveProvider.AddSamples(chunk.Array, chunk.Offset, chunk.Count);

                handle.WaitOne(wait);
                tnext = tnext + audioChunkSizeInTicks;
                wait = (int)((tnext - DateTime.Now.Ticks) / TimeSpan.TicksPerMillisecond);
                if (wait < 0) wait = 0;
            }
        }

        private void Disconnect()
        {
            if (this.currentState != UiState.Connected) return;

            UpdateUiState(UiState.Disconnecting);

            if (recorder != null)
            {
                recorder.StopRecording();
                recorder.DataAvailable -= OnRecorderDataAvailable;
                recorder.Dispose();
                recorder = null;
            }

            if (streamAudioFromFileInterrupt != null)
            {
                streamAudioFromFileInterrupt.Cancel();
                streamAudioFromFileInterrupt = null;
            }

            if (player != null)
            {
                player.Stop();
                player.Dispose();
                player = null;
            }

            // Close the audio file if logging
            if (audioSent != null)
            {
                audioSent.Flush();
                audioSent.Dispose();
                audioSent = null;
            }

            if (this.audioReceived != null)
            {
                this.audioReceived.Dispose();
                this.audioReceived = null;
            }

            var task = s2smtClient.Disconnect()
                .ContinueWith((t) =>
                {
                    if (t.IsFaulted)
                    {
                        this.Log(t.Exception, "E: Disconnect call to client failed.");
                    }
                    s2smtClient.Dispose();
                    s2smtClient = null;
                })
                .ContinueWith((t) => {
                    if (t.IsFaulted)
                    {
                        this.Log(t.Exception, "E: Disconnected but there were errors.");
                    }
                    else
                    {
                        this.Log("I: Disconnected. cid='{0}'", correlationId);
                    }
                    this.SafeInvoke(() => {
                        this.AutoSaveLogs();
                        this.UpdateUiState(UiState.ReadyToConnect);
                    });
                });
        }

        private void AddSamplesToPlay(ArraySegment<byte> a, bool suspendInputAudioDuringTTS)
        {
            int offset = a.Offset;
            int count = a.Count;
            if (this.textToSpeechBytes <= 0)
            {
                int chunkType = BitConverter.ToInt32(a.Array, a.Offset);
                if (chunkType != 0x46464952) throw new InvalidDataException("Invalid WAV file");
                int size = (int)(BitConverter.ToUInt32(a.Array, a.Offset + 4));
                int riffType = BitConverter.ToInt32(a.Array, a.Offset + 8);
                if (riffType != 0x45564157) throw new InvalidDataException("Invalid WAV file");
                textToSpeechBytes = size;
                if (suspendInputAudioDuringTTS)
                {
                    // Assumes PCM: (TTS audio duration in ms) = (size in bytes) / (32 bytes / ms)
                    this.suspendInputAudioUntil = DateTime.Now.AddMilliseconds(size / 32);
                    this.UpdateUiForInputAudioOnOff(false);
                }
                offset += 44;
                count -= 44;
            }
            playerTextToSpeechWaveProvider.AddSamples(a.Array, offset, count);
            textToSpeechBytes -= a.Count;

            if (this.audioReceived != null)
            {
                this.audioReceived.AppendData(a);
            }
        }

        private void TraceButton_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            Logs.Items.Clear();
            this.autoSaveFrom = 0;
        }

        private void AutoSaveLogs()
        {
            if (this.LogAutoSave.IsChecked == false)
            {
                return;
            }

            string cid = String.IsNullOrEmpty(this.correlationId) ? "no-cid" : this.correlationId;
            string filename = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Properties.Settings.Default.OutputDirectory, string.Format("TranslatorLog-{0}.txt", cid));
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Properties.Settings.Default.OutputDirectory));
                using (var writer = new StreamWriter(filename))
                {
                    for (int i = this.autoSaveFrom; i < Logs.Items.Count; i++)
                    {
                        writer.WriteLine(Logs.Items[i].ToString());
                    }
                }
                this.autoSaveFrom = this.Logs.Items.Count;
            }
            catch
            {
                this.AddItemToLog(string.Format("Could not create log file: {0}", filename));
            };
        }

        private void SaveLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Directory.Exists(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Properties.Settings.Default.OutputDirectory)))
                    Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Properties.Settings.Default.OutputDirectory));
            }
            catch { }
            var dlg = new System.Windows.Forms.SaveFileDialog();
            dlg.InitialDirectory = Properties.Settings.Default.OutputDirectory;
            dlg.FileName = string.Format("log-{0}.txt", String.IsNullOrEmpty(this.correlationId) ? "no-cid" : this.correlationId);
            dlg.DefaultExt = "txt";
            dlg.Filter = "txt files (*.txt)|*.txt|All files (*.*)|*.*";
            dlg.FilterIndex = 1;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                using (var writer = new StreamWriter(dlg.OpenFile()))
                {
                    foreach (var item in Logs.Items)
                    {
                        writer.WriteLine(item.ToString());
                    }
                }
            }
        }

        private string Now() { return DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss.ff", DateTimeFormatInfo.InvariantInfo); }

        private void Log(string text)
        {
            this.AddItemToLog(string.Format("{0} {1}", Now(), text));
        }

        private void Log(string format, params object[] args)
        {
            this.AddItemToLog(string.Format("{0} {1}", Now(), string.Format(format, args)));
        }

        private void Log(Exception exception, string format, params object[] args)
        {
            var s = new StringBuilder();
            s.Append(string.Format("{0} {1}", Now(), string.Format(format, args)));
            PrintException(exception, s);
            this.AddItemToLog(s.ToString());
        }

        private void AddItemToLog(string text)
        {
            Dispatcher.Invoke(() => {
                int pos = this.Logs.Items.Add(text);
                this.Logs.ScrollIntoView(this.Logs.Items.GetItemAt(pos));
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void PrintException(Exception exception, StringBuilder s)
        {
            if (exception == null) return;
            if (!(exception is AggregateException))
            {
                s.AppendLine(" ").Append(exception.Message);
            }
            PrintException(exception.InnerException, s);
        }

        private void OnRecorderDataAvailable(object sender, WaveInEventArgs e)
        {
            this.OnAudioDataAvailable(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded));
        }

        private void OnAudioDataAvailable(ArraySegment<byte> data)
        {
            if (DateTime.Now < this.suspendInputAudioUntil)
            {
                Array.Clear(data.Array, data.Offset, data.Count);
            }
            else
            {
                this.suspendInputAudioUntil = DateTime.MinValue;
                this.UpdateUiForInputAudioOnOff(true);
            }
            if (audioSent != null)
            {
                audioSent.Write(data.Array, data.Offset, data.Count);
            }
            if (s2smtClient != null)
            {
                s2smtClient.SendBinaryMessage(new ArraySegment<byte>(data.Array, data.Offset, data.Count));
                audioBytesSent += data.Count;
                this.SafeInvoke(() => this.AudioBytesSentLabel.Content = audioBytesSent);
            }
        }

        private SpeechClient.Features GetFeatures()
        {
            SpeechClient.Features features = 0;

            if (FeaturePartials.IsChecked.Value)
                features |= SpeechClient.Features.Partial;
            if (FeatureTTS.IsChecked.Value)
                features |= SpeechClient.Features.TextToSpeech;
            
            return features;
        }

        private byte[] GetWaveHeader(WaveFormat format)
        {
            using(MemoryStream stream = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8);
                writer.Write(Encoding.UTF8.GetBytes("RIFF"));
                writer.Write(0);
                writer.Write(Encoding.UTF8.GetBytes("WAVE"));
                writer.Write(Encoding.UTF8.GetBytes("fmt "));
                format.Serialize(writer);
                writer.Write(Encoding.UTF8.GetBytes("data"));
                writer.Write(0);

                stream.Position = 0;
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                return buffer;
            }         
        }

        private void SetMessage(string top, string bottom, MessageKind kind)
        {
            Brush borderBrush = Brushes.LightGray;
            Visibility borderVisibiliy = Visibility.Visible;
            Brush foreground1 = Brushes.Black;
            Brush foreground2 = Brushes.Green;
            string top1 = top;
            string top2 = "";
            string bottom1 = bottom;
            string bottom2 = "";

            switch (kind)
            {
                case MessageKind.Error:
                    borderBrush = Brushes.Red;
                    foreground1 = Brushes.Red;
                    borderVisibiliy = Visibility.Collapsed;
                    break;
                case MessageKind.Status:
                    borderBrush = Brushes.Green;
                    foreground1 = Brushes.Green;
                    borderVisibiliy = Visibility.Collapsed;
                    break;
                case MessageKind.ChatDetect1:
                    top1 = top;
                    top2 = bottom;
                    bottom1 = bottom2 = null;
                    break;
                case MessageKind.ChatDetect2:
                    top1 = top2 = null;
                    bottom1 = top;
                    bottom2 = bottom;
                    break;
                case MessageKind.Chat:
                default:
                    break;
            }

            this.DialogRecognitionBorder.BorderBrush = borderBrush;
            this.DialogTranslationBorder.BorderBrush = borderBrush;
            this.DialogBorder.BorderBrush = borderBrush;
            this.DialogBorder.Visibility = borderVisibiliy;

            this.TopRun1.Foreground = foreground1;
            this.TopRun2.Foreground = foreground2;
            this.BottomRun1.Foreground = foreground1;
            this.BottomRun2.Foreground = foreground2;
            if (top1 != null) this.TopRun1.Text = top1;
            if (top2 != null) this.TopRun2.Text = top2;
            if (bottom1 != null)
            {
                this.BottomRun1.Text = bottom1;
                if (kind == MessageKind.Chat) miniwindow.DisplayText.Text = bottom1 + "\n" + finaltranslationhistory;
            }
            if (bottom2 != null) this.BottomRun2.Text = bottom2;
        }


        //this method shows the audio input if an audio file is selected
        private void UpdateUiForInputAudioOnOff(bool isOn)
        {
            this.SafeInvoke(() => this.CutInputAudioLabel.Visibility = isOn ? Visibility.Collapsed : Visibility.Visible);            
        }

        private void UpdateUiState(UiState state)
        {
            this.currentState = state;
            bool isInputAllowed = true;
            this.AudioBytesSentLabel.Visibility = Visibility.Collapsed;

            switch (state)
            {
                case UiState.GettingLanguageList:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Getting language list from {0}", this.baseUrl), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                case UiState.MissingLanguageList:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Invalid credentials. Please enter your account settings."), "", MessageKind.Error);
                    isInputAllowed = true;
                    break;
                case UiState.ReadyToConnect:
                    this.StartListening.IsEnabled = true;
                    this.SetMessage(string.Format("Set your options, then click \"Start\" to start."), "", MessageKind.Status);
                    this.StartListening.Content = "Start";
                    break;
                case UiState.Connecting:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Connecting..."), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                case UiState.Connected:
                    this.StartListening.IsEnabled = true;
                    this.AudioBytesSentLabel.Visibility = Visibility.Visible;
                    this.SetMessage("Connected! After you start speaking, transcripts will show here...",
                            "...and translations will show here.", MessageKind.Status);
                    this.StartListening.Content = "Stop";
                    //this.TraceCmd.Text = this.GetTraceCmd();
                    //this.Log("I: {0}", this.TraceCmd.Text);
                    isInputAllowed = false;
                    break;
                case UiState.Disconnecting:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Disconnecting..."), "", MessageKind.Status);
                    isInputAllowed = false;
                    break;
                case UiState.InvalidCredentials:
                    this.StartListening.IsEnabled = false;
                    this.SetMessage(string.Format("Please enter your client ID and secret in the account settings."), "", MessageKind.Error);
                    isInputAllowed = true;
                    break;
                default:
                    break;
            }


            this.Mic.IsEnabled = isInputAllowed;
            this.AudioFileInput.IsEnabled = isInputAllowed;
            this.AudioFileInputButton.IsEnabled = isInputAllowed;
            this.Speaker.IsEnabled = isInputAllowed;
            this.FromLanguage.IsEnabled = isInputAllowed;
            this.ToLanguage.IsEnabled = isInputAllowed;
            this.FeaturePartials.IsEnabled = isInputAllowed;
            if (Voice.SelectedItem != null)
            {
                this.Voice.IsEnabled = isInputAllowed;
                this.FeatureTTS.IsEnabled = isInputAllowed;
                this.CutInputAudioCheckBox.IsEnabled = isInputAllowed;
            }
            else
            {
                this.Voice.IsEnabled = false;
                this.FeatureTTS.IsEnabled = false;
                this.CutInputAudioCheckBox.IsEnabled = false;
            }
            this.UpdateSettings.Visibility = Visibility.Collapsed;
            this.Profanity.IsEnabled = isInputAllowed;
            this.ShowMiniWindow.IsEnabled = isInputAllowed;

            this.MenuItem_SaveLogs.IsEnabled = isInputAllowed;
            this.MenuItem_ClearLogs.IsEnabled = true;
            this.LogAutoSave.IsEnabled = isInputAllowed;
            this.LogSentAudio.IsEnabled = isInputAllowed;
            this.LogReceivedAudio.IsEnabled = isInputAllowed;
        }

        private void SafeInvoke(Action action)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.Invoke(action);
            }
        }

        private async void SettingsWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (await IsValidCredentialsAsync()) UpdateUiState(UiState.ReadyToConnect);
            else UpdateUiState(UiState.InvalidCredentials);
        }

        private void Speaker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Properties.Settings.Default.SpeakerIndex = Speaker.SelectedIndex;
        }

        private void MiniWindow_Lines_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            miniwindow.SetFontSize(MiniWindow_Lines.SelectedIndex);
            Properties.Settings.Default.MiniWindow_Lines = MiniWindow_Lines.SelectedIndex;
        }

        private void ResetMiniWindow_Click(object sender, RoutedEventArgs e)
        {
            Screen[] screens = Screen.AllScreens;
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle();
            if (screennumber >= screens.Length)
            {
                screennumber = 0;
                resetcycle++;
            }
            rect = screens[screennumber].Bounds;
            miniwindow.Height = Math.Abs(rect.Height / 8);
            miniwindow.Width = rect.Width;
            miniwindow.Left = rect.Left;
            if ((resetcycle % 2) == 0) miniwindow.Top = rect.Bottom - miniwindow.Height;
            else miniwindow.Top = rect.Top;
            //SetMessage(string.Format("rect.Bottom: {0}, Width: {1}", rect.Bottom, rect.Width), string.Format("miniwindow.Top: {0} miniwindow.Left: {1}", miniwindow.Top, miniwindow.Left), MessageKind.Status);
            screennumber++;
            Focus();
        }

        private void ShowMiniWindow_Checked(object sender, RoutedEventArgs e)
        {
            UpdateMiniWindowUI(MiniWindowUIState.open);
        }

        private void ShowMiniWindow_UnChecked(object sender, RoutedEventArgs e)
        {
            UpdateMiniWindowUI(MiniWindowUIState.closed);
        }
                
        private enum MiniWindowUIState { open, closed};
        private void UpdateMiniWindowUI(MiniWindowUIState state)
        {
            miniwindow.DisplayText.Text = string.Empty;
            switch (state)
            {
                case MiniWindowUIState.closed:
                    ShowMiniWindow.IsChecked = false;
                    ResetMiniWindow.Visibility = Visibility.Collapsed;
                    NoOfLines.Visibility = Visibility.Collapsed;
                    MiniWindow_Lines.Visibility = Visibility.Collapsed;
                    if (miniwindow != null) miniwindow.Hide();
                    break;
                case MiniWindowUIState.open:
                    ShowMiniWindow.IsChecked = true;
                    ResetMiniWindow.Visibility = Visibility.Visible;
                    NoOfLines.Visibility = Visibility.Visible;
                    MiniWindow_Lines.Visibility = Visibility.Visible;
                    if (miniwindow == null)
                    {
                        miniwindow = new MiniWindow();
                        miniwindow.Closing += Miniwindow_Closing;
                    }
                    miniwindow.Show();
                    break;
                default: 
                    break;
            }
        }

        private void Miniwindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            UpdateMiniWindowUI(MiniWindowUIState.closed);
            miniwindow.Closing -= Miniwindow_Closing;
            miniwindow.Close();
            miniwindow = new MiniWindow();
            miniwindow.Closing += Miniwindow_Closing;
            miniwindow.Hide();
        }
        private void MenuItem_Experimental_Checked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ExperimentalLanguages = true;
        }
        private void MenuItem_Experimental_Unchecked(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ExperimentalLanguages = false;
        }

        private void MenuItem_FileExit_Click(object sender, RoutedEventArgs e)
        {
            MainWindow_Closing(sender, null);
        }

        private void MenuItem_SettingsAccount_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow sw = new SettingsWindow();
            sw.Show();
            sw.Closing += SettingsWindowClosing;
        }

        private void MenuItem_Profanity_Checked(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.MenuItem mi = (System.Windows.Controls.MenuItem)sender;
            switch (mi.Tag.ToString())
            {
                case "off":
                    Profanity_moderate.IsChecked = false;
                    Profanity_strict.IsChecked = false;
                    break;
                case "moderate":
                    Profanity_off.IsChecked = false;
                    Profanity_strict.IsChecked = false;
                    break;
                case "strict":
                    Profanity_off.IsChecked = false;
                    Profanity_moderate.IsChecked = false;
                    break;
            }
        }

        private string GetProfanityLevel()
        {
            if (Profanity_off.IsChecked) return "off";
            if (Profanity_moderate.IsChecked) return "moderate";
            return "strict";
        }

        private void MenuItem_SaveTranscript_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog savefiledialog = new SaveFileDialog();
            savefiledialog.RestoreDirectory = true;
            savefiledialog.FileName = "Transcript_" + DateTime.Now.ToString("yyMMdd_HHmm") + ".txt";
            savefiledialog.Filter = "Text Files|*.txt|All files|*.*";
            if (savefiledialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string transcriptfilename = Path.ChangeExtension(savefiledialog.FileName, "." + ((ComboBoxItem)FromLanguage.SelectedItem).Tag.ToString() + Path.GetExtension(savefiledialog.FileName));
                string signature = string.Format("\n\n\nCorrelationId: {0}\n{1}", correlationId, DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                using (StreamWriter file = new StreamWriter(transcriptfilename, false, Encoding.UTF8))
                {
                    foreach (TranscriptUtterance utterance in Transcript)
                    {
                        file.WriteLine(utterance.Recognition);
                    }
                    file.WriteLine(signature);
                    file.Close();
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = transcriptfilename;
                        p.Start();
                    }
                }
                if (((ComboBoxItem)FromLanguage.SelectedItem).Content == ((ComboBoxItem)ToLanguage.SelectedItem).Content)
                {
                    string translationfilename = Path.ChangeExtension(savefiledialog.FileName, "." + ((ComboBoxItem)ToLanguage.SelectedItem).Tag.ToString() + Path.GetExtension(savefiledialog.FileName));
                    using (StreamWriter file = new StreamWriter(translationfilename, false, Encoding.UTF8))
                    {
                        foreach (TranscriptUtterance utterance in Transcript)
                        {
                            file.WriteLine(utterance.Translation);
                        }
                        file.WriteLine(signature);
                        file.Close();
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = translationfilename;
                            p.Start();
                        }
                    }

                }
            }
        }

        private void Menu_File_Click(object sender, RoutedEventArgs e)
        {
            if (Transcript.Count > 0)
                MenuItem_SaveTranscript.IsEnabled = true;
            else
                MenuItem_SaveTranscript.IsEnabled = false;
        }
    }

    [DataContract]
    public class AppUserSettings
    {
        [DataMember(EmitDefaultValue = false)]
        public string OutputDirectory { get; set; }
    }

}
