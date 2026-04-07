using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace ClaudeAdvisor
{
    public class HttpCommandServer : MonoBehaviour
    {
        private HttpListener _listener;
        private bool _running;
        private const int PORT = 7828;

        // Screenshot support — queued from HTTP thread, executed on Unity main thread
        private volatile bool _screenshotRequested;
        private volatile bool _screenshotReady;
        private string _screenshotPath;
        private static readonly string SCREENSHOT_DIR = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            "Library/Application Support/Colossal Order/Cities_Skylines"
        );

        void Update()
        {
            // Execute screenshot on main thread (Unity requirement)
            if (_screenshotRequested)
            {
                _screenshotRequested = false;
                try
                {
                    if (!Directory.Exists(SCREENSHOT_DIR))
                        Directory.CreateDirectory(SCREENSHOT_DIR);
                    _screenshotPath = Path.Combine(SCREENSHOT_DIR, "claude_screenshot.png");
                    Application.CaptureScreenshot(_screenshotPath);
                    Debug.Log("[ClaudeAdvisor] Screenshot captured to: " + _screenshotPath);
                    // Wait a frame for the file to be written
                    StartCoroutine(MarkScreenshotReady());
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Screenshot failed: " + ex.Message);
                    _screenshotReady = true; // unblock the waiting thread even on failure
                }
            }
        }

        private System.Collections.IEnumerator MarkScreenshotReady()
        {
            // Wait 2 frames for Unity to finish writing the file
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            _screenshotReady = true;
        }

        void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:" + PORT + "/");
                _listener.Start();
                _running = true;
                _listener.BeginGetContext(OnRequest, null);
                Debug.Log("[ClaudeAdvisor] HTTP server running on http://localhost:" + PORT);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ClaudeAdvisor] Failed to start HTTP server: " + ex.Message);
            }
        }

        public void StopServer()
        {
            _running = false;
            if (_listener != null)
            {
                try { _listener.Stop(); _listener.Close(); }
                catch (Exception) { }
                _listener = null;
            }
            Debug.Log("[ClaudeAdvisor] HTTP server stopped.");
        }

        void OnDestroy()
        {
            StopServer();
        }

        private void OnRequest(IAsyncResult ar)
        {
            if (!_running || _listener == null) return;

            HttpListenerContext ctx = null;
            try
            {
                ctx = _listener.EndGetContext(ar);
            }
            catch (Exception)
            {
                if (_running) try { _listener.BeginGetContext(OnRequest, null); } catch { }
                return;
            }

            // Listen for next request immediately
            try { _listener.BeginGetContext(OnRequest, null); } catch { }

            // Handle this request on ThreadPool
            ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                string method = ctx.Request.HttpMethod;
                string path = ctx.Request.Url.AbsolutePath;
                var query = ctx.Request.QueryString;

                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

                // CORS preflight
                if (method == "OPTIONS")
                {
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
                    SendResponse(ctx, 200, "{\"ok\":true}");
                    return;
                }

                // Route dispatch
                if (method == "GET")
                {
                    switch (path)
                    {
                        case "/api/v1/health":
                            SendResponse(ctx, 200, JsonHelper.ToJson(new Dictionary<string, object> {
                                {"status", "ok"}, {"mod", "ClaudeAdvisor MCP"}, {"port", PORT}
                            }));
                            return;

                        case "/api/v1/stats":
                            var stats = CityDataCollector.GetFullStats();
                            SendResponse(ctx, 200, WrapSuccess(JsonHelper.ToJson(stats)));
                            return;

                        case "/api/v1/buildings":
                            string typeFilter = query["type"] ?? "";
                            string flagFilter = query["flags"] ?? "";
                            int limit = 100;
                            if (query["limit"] != null) int.TryParse(query["limit"], out limit);
                            var buildings = CityDataCollector.GetBuildingsList(typeFilter, flagFilter, limit);
                            SendResponse(ctx, 200, WrapSuccess("{\"buildings\":" + JsonHelper.ValueToJson(buildings) + ",\"count\":" + buildings.Count + "}"));
                            return;

                        case "/api/v1/traffic":
                            var dm = ColossalFramework.Singleton<DistrictManager>.instance;
                            var traffic = CityDataCollector.GetTrafficSummary();
                            SendResponse(ctx, 200, WrapSuccess(JsonHelper.ToJson(traffic)));
                            return;

                        case "/api/v1/transport":
                            var transport = CityDataCollector.GetTransportSummary();
                            SendResponse(ctx, 200, WrapSuccess(JsonHelper.ToJson(transport)));
                            return;

                        case "/api/v1/districts":
                            var districts = CityDataCollector.GetDistrictsList();
                            SendResponse(ctx, 200, WrapSuccess("{\"districts\":" + JsonHelper.ValueToJson(districts) + "}"));
                            return;

                        case "/api/v1/budget":
                            var budget = CityDataCollector.GetBudgetInfo();
                            SendResponse(ctx, 200, WrapSuccess(JsonHelper.ToJson(budget)));
                            return;

                        case "/api/v1/screenshot":
                            HandleScreenshot(ctx);
                            return;

                        case "/api/v1/screenshot/image":
                            ServeScreenshotImage(ctx);
                            return;

                        default:
                            SendResponse(ctx, 404, WrapError("Unknown endpoint: " + path));
                            return;
                    }
                }
                else if (method == "POST")
                {
                    string body = "";
                    using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    {
                        body = reader.ReadToEnd();
                    }
                    var parsed = JsonHelper.ParseSimpleJson(body);

                    switch (path)
                    {
                        case "/api/v1/actions/demolish":
                            HandleDemolish(ctx, parsed);
                            return;

                        case "/api/v1/actions/demolish-abandoned":
                            HandleDemolishAbandoned(ctx);
                            return;

                        case "/api/v1/actions/money":
                            HandleMoney(ctx, parsed);
                            return;

                        case "/api/v1/actions/tax":
                            HandleTax(ctx, parsed);
                            return;

                        case "/api/v1/actions/budget":
                            HandleBudget(ctx, parsed);
                            return;

                        case "/api/v1/actions/speed":
                            HandleSpeed(ctx, parsed);
                            return;

                        case "/api/v1/actions/pause":
                            HandlePause(ctx, parsed);
                            return;

                        default:
                            SendResponse(ctx, 404, WrapError("Unknown action: " + path));
                            return;
                    }
                }
                else
                {
                    SendResponse(ctx, 405, WrapError("Method not allowed"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[ClaudeAdvisor] Request error: " + ex.ToString());
                try { SendResponse(ctx, 500, WrapError(ex.Message)); } catch { }
            }
        }

        // --- Screenshot Handlers ---

        private void HandleScreenshot(HttpListenerContext ctx)
        {
            _screenshotReady = false;
            _screenshotRequested = true;

            // Wait for main thread to capture (up to 5 seconds)
            int waited = 0;
            while (!_screenshotReady && waited < 5000)
            {
                Thread.Sleep(50);
                waited += 50;
            }

            if (!_screenshotReady)
            {
                SendResponse(ctx, 500, WrapError("Screenshot timed out"));
                return;
            }

            // Verify file exists
            if (!string.IsNullOrEmpty(_screenshotPath) && File.Exists(_screenshotPath))
            {
                var info = new FileInfo(_screenshotPath);
                SendResponse(ctx, 200, WrapSuccess(JsonHelper.ToJson(new Dictionary<string, object> {
                    {"action", "screenshot"},
                    {"path", _screenshotPath},
                    {"size_kb", (int)(info.Length / 1024)},
                    {"imageUrl", "http://localhost:" + PORT + "/api/v1/screenshot/image"},
                    {"timestamp", DateTime.Now.ToString("o")}
                })));
            }
            else
            {
                SendResponse(ctx, 500, WrapError("Screenshot file not found after capture"));
            }
        }

        private void ServeScreenshotImage(HttpListenerContext ctx)
        {
            string path = Path.Combine(SCREENSHOT_DIR, "claude_screenshot.png");
            if (!File.Exists(path))
            {
                SendResponse(ctx, 404, WrapError("No screenshot available. Call /api/v1/screenshot first."));
                return;
            }

            try
            {
                byte[] imageBytes = File.ReadAllBytes(path);
                ctx.Response.ContentType = "image/png";
                ctx.Response.ContentLength64 = imageBytes.Length;
                ctx.Response.StatusCode = 200;
                ctx.Response.OutputStream.Write(imageBytes, 0, imageBytes.Length);
                ctx.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                SendResponse(ctx, 500, WrapError("Failed to serve image: " + ex.Message));
            }
        }

        // --- Action Handlers ---

        private void HandleDemolish(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string idStr;
            if (!body.TryGetValue("buildingId", out idStr))
            {
                SendResponse(ctx, 400, WrapError("Missing buildingId"));
                return;
            }
            int buildingId;
            if (!int.TryParse(idStr, out buildingId))
            {
                SendResponse(ctx, 400, WrapError("Invalid buildingId"));
                return;
            }
            GameActionExecutor.DemolishBuilding((ushort)buildingId);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"demolish\",\"buildingId\":" + buildingId + ",\"queued\":true}"));
        }

        private void HandleDemolishAbandoned(HttpListenerContext ctx)
        {
            int count = GameActionExecutor.DemolishAllAbandoned();
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"demolish-abandoned\",\"count\":" + count + ",\"queued\":true}"));
        }

        private void HandleMoney(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string amtStr;
            if (!body.TryGetValue("amount", out amtStr))
            {
                SendResponse(ctx, 400, WrapError("Missing amount"));
                return;
            }
            int amount;
            if (!int.TryParse(amtStr, out amount))
            {
                SendResponse(ctx, 400, WrapError("Invalid amount"));
                return;
            }
            GameActionExecutor.InjectMoney(amount);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"money\",\"amount\":" + amount + ",\"queued\":true}"));
        }

        private void HandleTax(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string rateStr;
            if (!body.TryGetValue("rate", out rateStr))
            {
                SendResponse(ctx, 400, WrapError("Missing rate"));
                return;
            }
            int rate;
            if (!int.TryParse(rateStr, out rate) || rate < 0 || rate > 29)
            {
                SendResponse(ctx, 400, WrapError("Invalid rate (0-29)"));
                return;
            }
            string service = "";
            body.TryGetValue("service", out service);
            GameActionExecutor.SetTaxRate(service ?? "Residential", rate);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"tax\",\"service\":\"" + JsonHelper.Escape(service) + "\",\"rate\":" + rate + ",\"queued\":true}"));
        }

        private void HandleBudget(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string budgetStr;
            if (!body.TryGetValue("budget", out budgetStr))
            {
                SendResponse(ctx, 400, WrapError("Missing budget"));
                return;
            }
            int budget;
            if (!int.TryParse(budgetStr, out budget) || budget < 50 || budget > 150)
            {
                SendResponse(ctx, 400, WrapError("Invalid budget (50-150)"));
                return;
            }
            string service = "";
            body.TryGetValue("service", out service);
            GameActionExecutor.SetBudget(service ?? "HealthCare", budget);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"budget\",\"service\":\"" + JsonHelper.Escape(service) + "\",\"budget\":" + budget + ",\"queued\":true}"));
        }

        private void HandleSpeed(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string speedStr;
            if (!body.TryGetValue("speed", out speedStr))
            {
                SendResponse(ctx, 400, WrapError("Missing speed"));
                return;
            }
            int speed;
            if (!int.TryParse(speedStr, out speed) || speed < 1 || speed > 3)
            {
                SendResponse(ctx, 400, WrapError("Invalid speed (1-3)"));
                return;
            }
            GameActionExecutor.SetSpeed(speed);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"speed\",\"speed\":" + speed + ",\"queued\":true}"));
        }

        private void HandlePause(HttpListenerContext ctx, Dictionary<string, string> body)
        {
            string pausedStr;
            if (!body.TryGetValue("paused", out pausedStr))
            {
                SendResponse(ctx, 400, WrapError("Missing paused"));
                return;
            }
            bool paused = pausedStr == "true" || pausedStr == "1";
            GameActionExecutor.SetPaused(paused);
            SendResponse(ctx, 200, WrapSuccess("{\"action\":\"pause\",\"paused\":" + (paused ? "true" : "false") + ",\"queued\":true}"));
        }

        // --- Helpers ---

        private void SendResponse(HttpListenerContext ctx, int statusCode, string json)
        {
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                ctx.Response.OutputStream.Close();
            }
            catch (Exception) { }
        }

        private string WrapSuccess(string dataJson)
        {
            return "{\"success\":true,\"data\":" + dataJson + ",\"error\":null,\"timestamp\":\"" + DateTime.Now.ToString("o") + "\"}";
        }

        private string WrapError(string message)
        {
            return "{\"success\":false,\"data\":null,\"error\":\"" + JsonHelper.Escape(message) + "\",\"timestamp\":\"" + DateTime.Now.ToString("o") + "\"}";
        }
    }
}
