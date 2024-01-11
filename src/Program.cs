namespace HttpServer
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Net;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;
    using TICO.GAUDI.Commons;

    class Program
    {
        const string PROPERTY_KEY_OUTPUT = "output";
        const string DEFAULT_PROPERTY_OUTPUT = "output";

        static IModuleClient MyModuleClient { get; set; } = null;

        static Logger MyLogger { get; } = Logger.GetLogger(typeof(Program));

        static bool IsReady { get; set; } = false;

        static CancellationTokenSource cts = new CancellationTokenSource();

        static string outputName = DEFAULT_PROPERTY_OUTPUT;

        static HttpListener listener = null;

        static void Main(string[] args)
        {
            try
            {
                Init().Wait();
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Init failed. {e}", true);
                Environment.Exit(1);
            }

            // Wait until the app unloads or is cancelled
            //var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();

        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// </summary>
        static async Task Init()
        {
            // 取得済みのModuleClientを解放する
            if (MyModuleClient != null)
            {
                await MyModuleClient.CloseAsync();
                MyModuleClient.Dispose();
                MyModuleClient = null;
            }

            // 環境変数から送信トピックを判定
            TransportTopic defaultSendTopic = TransportTopic.Iothub;
            string sendTopicEnv = Environment.GetEnvironmentVariable("DefaultSendTopic");
            if (Enum.TryParse(sendTopicEnv, true, out TransportTopic sendTopic))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultSendTopic\" is {sendTopicEnv}.");
                defaultSendTopic = sendTopic;
            }
            else
            {
                MyLogger.WriteLog(Logger.LogLevel.DEBUG, "Evironment Variable \"DefaultSendTopic\" is not set.");
            }

            // 環境変数から受信トピックを判定
            TransportTopic defaultReceiveTopic = TransportTopic.Iothub;
            string receiveTopicEnv = Environment.GetEnvironmentVariable("DefaultReceiveTopic");
            if (Enum.TryParse(receiveTopicEnv, true, out TransportTopic receiveTopic))
            {
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"DefaultReceiveTopic\" is {receiveTopicEnv}.");
                defaultReceiveTopic = receiveTopic;
            }
            else
            {
                MyLogger.WriteLog(Logger.LogLevel.DEBUG, "Evironment Variable \"DefaultReceiveTopic\" is not set.");
            }

            // MqttModuleClientを作成
            if (Boolean.TryParse(Environment.GetEnvironmentVariable("M2MqttFlag"), out bool m2mqttFlag) && m2mqttFlag)
            {
                string sasTokenEnv = Environment.GetEnvironmentVariable("SasToken");
                MyModuleClient = new MqttModuleClient(sasTokenEnv, defaultSendTopic: defaultSendTopic, defaultReceiveTopic: defaultReceiveTopic);
            }
            // IoTHubModuleClientを作成
            else
            {
                ITransportSettings[] settings = null;
                string protocolEnv = Environment.GetEnvironmentVariable("TransportProtocol");
                if (Enum.TryParse(protocolEnv, true, out TransportProtocol transportProtocol))
                {
                    MyLogger.WriteLog(Logger.LogLevel.INFO, $"Evironment Variable \"TransportProtocol\" is {protocolEnv}.");
                    settings = transportProtocol.GetTransportSettings();
                }
                else
                {
                    MyLogger.WriteLog(Logger.LogLevel.DEBUG, "Evironment Variable \"TransportProtocol\" is not set.");
                }

                MyModuleClient = await IotHubModuleClient.CreateAsync(settings, defaultSendTopic, defaultReceiveTopic).ConfigureAwait(false);
            }

            // edgeHubへの接続
            while (true)
            {
                try
                {
                    await MyModuleClient.OpenAsync().ConfigureAwait(false);
                    break;
                }
                catch (Exception e)
                {
                    MyLogger.WriteLog(Logger.LogLevel.WARN, $"Open a connection to the Edge runtime is failed. {e.Message}");
                    await Task.Delay(1000);
                }
            }

            // Loggerへモジュールクライアントを設定
            Logger.SetModuleClient(MyModuleClient);

            // 環境変数からログレベルを設定
            string logEnv = Environment.GetEnvironmentVariable("LogLevel");
            try
            {
                if (logEnv != null) Logger.SetOutputLogLevel(logEnv);
                MyLogger.WriteLog(Logger.LogLevel.INFO, $"Output log level is: {Logger.OutputLogLevel.ToString()}");
            }
            catch (ArgumentException e)
            {
                MyLogger.WriteLog(Logger.LogLevel.WARN, $"Environment LogLevel does not expected string. Exception:{e.Message}");
            }

            // desiredプロパティの取得
            var twin = await MyModuleClient.GetTwinAsync().ConfigureAwait(false);
            var collection = twin.Properties.Desired;
            IsReady = SetMyProperties(collection);

            // プロパティ更新時のコールバックを登録
            await MyModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null).ConfigureAwait(false);

            if (IsReady)
            {
                // HTTPリクエスト受信処理
                await WaitRequestAsync(cts.Token);
            }
        }

        /// <summary>
        /// プロパティ更新時のコールバック処理
        /// </summary>
        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            MyLogger.WriteLog(Logger.LogLevel.INFO, "OnDesiredPropertiesUpdate Called.");

            try
            {
                await Init();
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"OnDesiredPropertiesUpdate failed. {e}", true);
            }
        }

        /// <summary>
        /// desiredプロパティから自クラスのプロパティをセットする
        /// </summary>
        /// <returns>desiredプロパティに想定しない値があればfalseを返す</returns>
        static bool SetMyProperties(TwinCollection desiredProperties)
        {
            try
            {
                // output
                if (desiredProperties.Contains(Program.PROPERTY_KEY_OUTPUT))
                {
                    outputName = desiredProperties[Program.PROPERTY_KEY_OUTPUT];
                }
                else
                {
                    outputName = Program.DEFAULT_PROPERTY_OUTPUT;
                }

                return true;
            }
            catch (Exception e)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"SetMyProperties failed. {e.ToString()}", true);
                return false;
            }
        }

        /// <summary>
        /// HTTPリクエストを受信して、メッセージとして送信する。
        /// </summary>
        static async Task WaitRequestAsync(CancellationToken ct)
        {
            string prefix = Environment.GetEnvironmentVariable("UriPrefix");
            if (string.IsNullOrEmpty(prefix))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, "Environment Variable \"UriPrefix\" is not set.", true);
                throw new ArgumentException("Environment Variable \"UriPrefix\" is require.");
            }
            MyLogger.WriteLog(Logger.LogLevel.INFO, $"Environment Variable \"UriPrefix\" is {prefix}");

            // listenerが起動している場合、終了させる
            if (Program.listener != null)
            {
                Program.listener.Stop();
                Program.listener = null;
            }

            Program.listener = new HttpListener();
            Program.listener.Prefixes.Add(prefix);
            Program.listener.Start();
            MyLogger.WriteLog(Logger.LogLevel.INFO, "HttpServer has started.");

            ct.Register(() => Program.listener.Stop());

            HttpListenerResponse response = null;
            Stream body = null;
            StreamReader reader = null;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await Program.listener.GetContextAsync();
                    MyLogger.WriteLog(Logger.LogLevel.DEBUG, "Received an HTTP request.");
                    HttpListenerRequest request = context.Request;
                    response = context.Response;

                    if (!ValidateRequest(request))
                    {
                        continue;
                    }
                    IDictionary<string, string> properties = GetAdditionalData(request);
                    body = request.InputStream;
                    reader = new StreamReader(body);
                    string data = reader.ReadToEnd();
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"request body: {data}");
                    var message = new IotMessage(data);
                    if (properties != null)
                    {
                        message.SetProperties( properties, IotMessage.PropertySetMode.Add );
                    }
                    MyLogger.WriteLog(Logger.LogLevel.TRACE, $"message body: {message.GetBodyString()}");
                    foreach (var prop in message.GetProperties())
                    {
                        MyLogger.WriteLog(Logger.LogLevel.TRACE, $"message properties: key={prop.Key}, value={prop.Value}");
                    }

                    await MyModuleClient.SendEventAsync(outputName, message);
                }
                catch (Exception e)
                {
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, e.Message);
                    MyLogger.WriteLog(Logger.LogLevel.ERROR, e.StackTrace);
                }
                finally
                {
                    if (body != null)
                    {
                        body.Close();
                    }
                    if (reader != null)
                    {
                        reader.Close();
                    }
                    SetHttpResponse(response);
                }
            }
        }

        /// <summary>
        /// 受信した HTTPリクエストを検査する。
        /// ・リクエストメソッドが「POST」以外
        /// ・リクエストのボディが存在しない
        /// 上記の場合はログを出力して、false を返す。
        /// </summary>
        private static bool ValidateRequest(HttpListenerRequest request)
        {
            bool valid = true;
            if (!request.HttpMethod.ToUpper().Equals("POST"))
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, $"Received a {request.HttpMethod} request. Must be a POST request.");
                valid = false;
            }
            else if (!request.HasEntityBody)
            {
                MyLogger.WriteLog(Logger.LogLevel.ERROR, "No client data was sent with the request.");
                valid = false;
            }
            return valid;
        }

        /// <summary>
        /// 引数の HttpListenerRequest の Headers に設定されている「additionalData」を取得し、
        /// Dictionary型にして返す。
        /// </summary>
        private static IDictionary<string, string> GetAdditionalData(HttpListenerRequest request)
        {
            IDictionary<string, string> properties = null;
            NameValueCollection headers = request.Headers;
            if (headers != null)
            {
                string value = headers["additionalData"];
                MyLogger.WriteLog(Logger.LogLevel.TRACE, $"additionalData: {value}");
                if (!string.IsNullOrEmpty(value))
                {
                    properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
                }
            }
            return properties;
        }

        /// <summary>
        /// 引数の HttpListenerResponse にステータスコード等を設定して、クローズする。
        /// </summary>
        private static void SetHttpResponse(HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.StatusDescription = "OK";
            response.ProtocolVersion = new Version("1.1");
            response.Close();
        }

    }

}
