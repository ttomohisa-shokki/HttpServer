using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TICO.GAUDI.Commons;

namespace IotedgeV2HttpServer
{
    /// <summary>
    /// Application Main class
    /// </summary>
    internal class MyApplicationMain : IApplicationMain
    {
        static ILogger MyLogger { get; } = LoggerFactory.GetLogger(typeof(MyApplicationMain));
        const string PROPERTY_KEY_OUTPUT = "output";
        const string DEFAULT_PROPERTY_OUTPUT = "output";
        static CancellationTokenSource cts = new CancellationTokenSource();
        static string outputName = DEFAULT_PROPERTY_OUTPUT;
        static HttpListener listener = null;

        public class ExclusiveException : Exception
        {
            public ExclusiveException(string message) : base(message) { }
        }

        public void Dispose()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: Dispose");

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: Dispose");
        }

        /// <summary>
        /// アプリケーション初期化					
        /// システム初期化前に呼び出される
        /// </summary>
        /// <returns></returns>
        public async Task<bool> InitializeAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: InitializeAsync");

            // ここでApplicationMainの初期化処理を行う。
            // 通信は未接続、DesiredPropertiesなども未取得の状態
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            outputName = DEFAULT_PROPERTY_OUTPUT;

            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: InitializeAsync");
            return retStatus;
        }

        /// <summary>
        /// アプリケーション起動処理					
        /// システム初期化完了後に呼び出される
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public async Task<bool> StartAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: StartAsync");

            // ここでApplicationMainの起動処理を行う。
            // 通信は接続済み、DesiredProperties取得済みの状態
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;
            // HTTPリクエスト受信処理
            await WaitRequestAsync(cts.Token);
            var httpHandlerTask = Task.Run(async () =>
               {
                   bool ret = true;
                   try
                   {
                       await HandleHttpRequests(cts.Token);
                   }
                   catch (Exception ex)
                   {
                       var errmsg = $"HandleHttpRequests task failed.";
                       MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                       MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: StartAsync caused by {errmsg}");
                       ret = false;
                   }
                   return ret;
               });
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: StartAsync");
            return retStatus;
        }

        /// <summary>
        /// アプリケーション解放。					
        /// </summary>
        /// <returns></returns>
        public async Task<bool> TerminateAsync()
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: TerminateAsync");

            // ここでApplicationMainの終了処理を行う。
            // アプリケーション終了時や、
            // DesiredPropertiesの更新通知受信後、
            // 通信切断時の回復処理時などに呼ばれる。
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;

            // listenerが起動している場合、終了させる
            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }

            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: TerminateAsync");
            return retStatus;
        }


        /// <summary>
        /// DesiredPropertis更新コールバック。					
        /// </summary>
        /// <param name="desiredProperties">DesiredPropertiesデータ。JSONのルートオブジェクトに相当。</param>
        /// <returns></returns>
        public async Task<bool> OnDesiredPropertiesReceivedAsync(JObject desiredProperties)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: OnDesiredPropertiesReceivedAsync");

            // DesiredProperties更新時の反映処理を行う。
            // 必要に応じて、メンバ変数への格納等を実施。
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここから＝＝＝＝＝＝＝＝＝＝＝＝＝
            bool retStatus = true;
            try
            {
                // output
                if (desiredProperties.ContainsKey(PROPERTY_KEY_OUTPUT))
                {
                    outputName = Util.GetRequiredValue<string>(desiredProperties, PROPERTY_KEY_OUTPUT);
                }
                else
                {
                    outputName = DEFAULT_PROPERTY_OUTPUT;
                }
                retStatus = true;
            }
            catch (Exception ex)
            {
                var errmsg = $"Property {PROPERTY_KEY_OUTPUT} does not exist : {ex.ToString()}";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: OnDesiredPropertiesReceivedAsync caused by {errmsg}");
                retStatus = false;
                return retStatus;
            }
            await Task.CompletedTask;
            // ＝＝＝＝＝＝＝＝＝＝＝＝＝ここまで＝＝＝＝＝＝＝＝＝＝＝＝＝

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: OnDesiredPropertiesReceivedAsync");
            return retStatus;
        }


        /// <summary>
        /// HTTPリクエストを受信して、メッセージとして送信する。
        /// </summary>
        static async Task WaitRequestAsync(CancellationToken ct)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: WaitRequestAsync");
            string prefix = Environment.GetEnvironmentVariable("UriPrefix");
            if (string.IsNullOrEmpty(prefix))
            {
                var errmsg = $"Environment Variable \"UriPrefix\" is not set.";
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: WaitRequestAsync caused by {errmsg}");
                throw new ArgumentException(errmsg);
            }
            MyLogger.WriteLog(ILogger.LogLevel.INFO, $"Environment Variable \"UriPrefix\" is {prefix}");

            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            MyLogger.WriteLog(ILogger.LogLevel.INFO, "HttpServer has started.");

            ct.Register(() => listener.Stop());
            await Task.CompletedTask;
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: WaitRequestAsync");
        }

        /// <summary>
        /// 受信した HTTPリクエストを検査する。
        /// ・リクエストメソッドが「POST」以外
        /// ・リクエストのボディが存在しない
        /// 上記の場合はログを出力して、false を返す。
        /// </summary>
        private static bool ValidateRequest(HttpListenerRequest request)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: ValidateRequest");
            bool valid = true;
            if (!request.HttpMethod.ToUpper().Equals("POST"))
            {
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"Received a {request.HttpMethod} request. Must be a POST request.", true);
                valid = false;
            }
            else if (!request.HasEntityBody)
            {
                MyLogger.WriteLog(ILogger.LogLevel.ERROR, "No client data was sent with the request.", true);
                valid = false;
            }
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: ValidateRequest");
            return valid;
        }

        /// <summary>
        /// 引数の HttpListenerRequest の Headers に設定されている「additionalData」を取得し、
        /// Dictionary型にして返す。
        /// </summary>
        private static IDictionary<string, string> GetAdditionalData(HttpListenerRequest request)
        {
            // 不要な処理を回避の為、TRACEログを出力する設定か確認
            if (MyLogger.IsLogLevelToOutput(ILogger.LogLevel.TRACE))
            {
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: GetAdditionalData");
            }
            IDictionary<string, string> properties = null;
            NameValueCollection headers = request.Headers;
            if (headers != null)
            {
                string value = headers["additionalData"];
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"additionalData: {value}");
                if (!string.IsNullOrEmpty(value))
                {
                    properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
                }
            }
            // 不要な処理を回避の為、TRACEログを出力する設定か確認
            if (MyLogger.IsLogLevelToOutput(ILogger.LogLevel.TRACE))
            {
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: GetAdditionalData");
            }
            return properties;
        }

        /// <summary>
        /// リクエスト待ち処理及びレスポンス設定処理
        /// </summary>
        static async Task HandleHttpRequests(CancellationToken ct)
        {
            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Start Method: HandleHttpRequests");

            Stream body = null;
            StreamReader reader = null;
            bool restartFlag = false;
            IApplicationEngine appEngine = ApplicationEngineFactory.GetEngine();
            ApplicationStateChangeResult stateChangeResult = ApplicationStateChangeResult.Ignored;
            while (!ct.IsCancellationRequested)
            {
                HttpListenerResponse response = null;
                try
                {
                    stateChangeResult = ApplicationStateChangeResult.Ignored;
                    HttpListenerContext context = await listener.GetContextAsync();
                    response = context.Response;
                    if (response != null)
                    {
                        response.StatusCode = (int)HttpStatusCode.OK;
                        response.StatusDescription = "OK";
                        response.ProtocolVersion = new Version("1.1");
                    }
                    MyLogger.WriteLog(ILogger.LogLevel.DEBUG, "Received an HTTP request.");
                    HttpListenerRequest request = context.Request;
                    if (!ValidateRequest(request))
                    {
                        continue;
                    }
                    stateChangeResult = await appEngine.SetApplicationRunningAsync();
                    if (ApplicationStateChangeResult.Success == stateChangeResult)
                    {
                        IDictionary<string, string> properties = GetAdditionalData(request);
                        body = request.InputStream;
                        reader = new StreamReader(body);
                        string data = reader.ReadToEnd();
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"request body: {data}");
                        var message = new IotMessage(data);
                        if (properties != null)
                        {
                            message.SetProperties(properties, IotMessage.PropertySetMode.Add);
                        }
                        MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message body: {message.GetBodyString()}");
                        foreach (var prop in message.GetProperties())
                        {
                            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"message properties: key={prop.Key}, value={prop.Value}");
                        }
                        await appEngine.SendMessageAsync(outputName, message);
                        MyLogger.WriteLog(ILogger.LogLevel.INFO, "1 message sent");
                    }
                    else
                    {
                        var errmsg = $"SetApplicationRunningAsync is not 'Success'.";
                        throw new ExclusiveException(errmsg);
                    }
                }
                catch (ExclusiveException ex)
                {
                    if (response != null)
                    {
                        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                        response.StatusDescription = "Service Unavailable";
                    }
                    var errmsg = $"Exclusive processing failed.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: HandleHttpRequests caused by {errmsg}");
                    restartFlag = true;
                    break;
                }
                catch (HttpListenerException ex)
                {
                    if (response != null)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        response.StatusDescription = "HttpListenerException Error";
                    }
                    var errmsg = $"HttpListenerException failed.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                }
                catch (ObjectDisposedException)
                {
                    if (response != null)
                    {
                        response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                        response.StatusDescription = "HttpListener has been disposed";
                    }
                    var msg = "HttpListener has been disposed.";
                    MyLogger.WriteLog(ILogger.LogLevel.INFO, $"{msg}");
                    restartFlag = false;
                    break;
                }   
                catch (Exception ex)
                {
                    if (response != null)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        response.StatusDescription = "Exception Error";
                    }
                    var errmsg = $"Exception failed.";
                    MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg} {ex}", true);
                    MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: HandleHttpRequests caused by {errmsg}");
                    restartFlag = true;
                    break;
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
                    if (response != null)
                    {
                        response.Close();
                    }
                    if (ApplicationStateChangeResult.Success == stateChangeResult)
                    {
                        if (ApplicationStateChangeResult.Success != await appEngine.UnsetApplicationRunningAsync())
                        {
                            var errmsg = $"UnsetApplicationRunningAsync is not 'Success'.";
                            MyLogger.WriteLog(ILogger.LogLevel.ERROR, $"{errmsg}", true);
                        }
                    }
                }
            }

            if(ApplicationStateChangeResult.Success == stateChangeResult && true == restartFlag)
            {
                var exitmsg = $"restartFlag = {restartFlag}";
                MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"Exit Method: HandleHttpRequests caused by {exitmsg}");
                await appEngine.SetApplicationRestartAsync();
            }

            MyLogger.WriteLog(ILogger.LogLevel.TRACE, $"End Method: HandleHttpRequests");
        }
    }
}
