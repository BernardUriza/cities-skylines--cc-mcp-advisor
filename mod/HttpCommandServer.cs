using System;
using System.Diagnostics;
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
        private RequestHandler _handler;

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
            if (_screenshotRequested)
            {
                _screenshotRequested = false;
                try
                {
                    if (!Directory.Exists(SCREENSHOT_DIR))
                        Directory.CreateDirectory(SCREENSHOT_DIR);
                    _screenshotPath = Path.Combine(SCREENSHOT_DIR, "claude_screenshot.png");
                    Application.CaptureScreenshot(_screenshotPath);
                    Logger.Info("Screenshot", "Captured to " + _screenshotPath);
                    StartCoroutine(MarkScreenshotReady());
                }
                catch (Exception ex)
                {
                    Logger.Error("Screenshot", "Capture failed", ex);
                    _screenshotReady = true;
                }
            }
        }

        private System.Collections.IEnumerator MarkScreenshotReady()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            _screenshotReady = true;
        }

        void Start()
        {
            _handler = new RequestHandler();
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add("http://localhost:" + PORT + "/");
                _listener.Start();
                _running = true;
                _listener.BeginGetContext(OnRequest, null);
                Logger.Info("Server", "HTTP server started on port " + PORT);
            }
            catch (Exception ex)
            {
                Logger.Error("Server", "Failed to start HTTP server", ex);
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
            Logger.Info("Server", "HTTP server stopped");
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
            catch (Exception ex)
            {
                Logger.Warn("Server", "EndGetContext failed", ex.GetType().Name);
                if (_running) try { _listener.BeginGetContext(OnRequest, null); } catch { }
                return;
            }

            try { _listener.BeginGetContext(OnRequest, null); } catch { }

            ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
        }

        private void HandleRequest(HttpListenerContext ctx)
        {
            string method = ctx.Request.HttpMethod;
            string path = ctx.Request.Url.AbsolutePath;

            // Read or generate correlation ID for end-to-end tracing
            string cid = ctx.Request.Headers["X-Correlation-ID"];
            if (string.IsNullOrEmpty(cid))
                cid = Logger.NewCorrelationId();
            else
                Logger.CorrelationId = cid;

            Stopwatch sw = Logger.RequestStart(method, path);

            try
            {
                var query = ctx.Request.QueryString;

                ctx.Response.ContentType = "application/json";
                ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                ctx.Response.Headers.Add("X-Correlation-ID", cid);

                if (method == "OPTIONS")
                {
                    ctx.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    ctx.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, X-Correlation-ID");
                    SendJson(ctx, 200, "{\"ok\":true}");
                    Logger.RequestEnd(method, path, 200, sw);
                    return;
                }

                ServiceResult result = null;

                if (method == "GET")
                {
                    switch (path)
                    {
                        case "/api/v1/health":
                            result = _handler.Health();
                            break;
                        case "/api/v1/stats":
                            result = _handler.GetStats();
                            break;
                        case "/api/v1/buildings":
                            string typeFilter = query["type"] ?? "";
                            string flagFilter = query["flags"] ?? "";
                            int limit = 100;
                            if (query["limit"] != null) int.TryParse(query["limit"], out limit);
                            result = _handler.GetBuildings(typeFilter, flagFilter, limit);
                            break;
                        case "/api/v1/traffic":
                            result = _handler.GetTraffic();
                            break;
                        case "/api/v1/traffic/graph":
                            int graphLimit = 10000;
                            int graphMinDensity = 0;
                            if (query["limit"] != null) int.TryParse(query["limit"], out graphLimit);
                            if (query["minDensity"] != null) int.TryParse(query["minDensity"], out graphMinDensity);
                            result = _handler.GetTrafficGraph(graphLimit, graphMinDensity);
                            break;
                        case "/api/v1/transport":
                            result = _handler.GetTransport();
                            break;
                        case "/api/v1/districts":
                            result = _handler.GetDistricts();
                            break;
                        case "/api/v1/budget":
                            result = _handler.GetBudget();
                            break;
                        case "/api/v1/problems":
                            result = _handler.GetProblems();
                            break;
                        case "/api/v1/changes":
                            result = _handler.GetChanges();
                            break;
                        case "/api/v1/screenshot":
                            result = WaitForScreenshot();
                            break;
                        case "/api/v1/screenshot/image":
                            result = _handler.GetScreenshotImage();
                            break;
                        default:
                            Logger.Warn("HTTP", "Unknown endpoint", "path=" + path);
                            result = ServiceResult.Error(404, "Unknown endpoint: " + path);
                            break;
                    }
                }
                else if (method == "POST")
                {
                    string body = "";
                    using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    {
                        body = reader.ReadToEnd();
                    }
                    Logger.Debug("HTTP", "POST body received", "length=" + body.Length);
                    var parsed = JsonHelper.ParseSimpleJson(body);

                    switch (path)
                    {
                        case "/api/v1/actions/demolish":
                            result = _handler.Demolish(parsed);
                            break;
                        case "/api/v1/actions/demolish-abandoned":
                            result = _handler.DemolishAbandoned();
                            break;
                        case "/api/v1/actions/money":
                            result = _handler.AddMoney(parsed);
                            break;
                        case "/api/v1/actions/tax":
                            result = _handler.SetTax(parsed);
                            break;
                        case "/api/v1/actions/budget":
                            result = _handler.SetBudget(parsed);
                            break;
                        case "/api/v1/actions/speed":
                            result = _handler.SetSpeed(parsed);
                            break;
                        case "/api/v1/actions/pause":
                            result = _handler.SetPaused(parsed);
                            break;
                        case "/api/v1/actions/chirp":
                            result = _handler.SendChirp(parsed);
                            break;
                        default:
                            Logger.Warn("HTTP", "Unknown action", "path=" + path);
                            result = ServiceResult.Error(404, "Unknown action: " + path);
                            break;
                    }
                }
                else
                {
                    result = ServiceResult.Error(405, "Method not allowed");
                }

                SendResult(ctx, result);
                Logger.RequestEnd(method, path, result.StatusCode, sw);
            }
            catch (Exception ex)
            {
                Logger.Error("HTTP", "Unhandled exception in " + method + " " + path, ex);
                try
                {
                    var errResult = ServiceResult.Error(500, ex.Message);
                    SendResult(ctx, errResult);
                    Logger.RequestEnd(method, path, 500, sw);
                }
                catch (Exception sendEx)
                {
                    Logger.Error("HTTP", "Failed to send error response", sendEx);
                }
            }
            finally
            {
                Logger.ClearCorrelationId();
            }
        }

        // --- Screenshot orchestration (needs Unity main thread) ---

        private ServiceResult WaitForScreenshot()
        {
            Logger.Info("Screenshot", "Waiting for main thread capture");
            _screenshotReady = false;
            _screenshotRequested = true;

            int waited = 0;
            while (!_screenshotReady && waited < 5000)
            {
                Thread.Sleep(50);
                waited += 50;
            }

            if (!_screenshotReady)
            {
                Logger.Error("Screenshot", "Timed out after 5000ms");
                return ServiceResult.Error(500, "Screenshot timed out");
            }

            Logger.Info("Screenshot", "Ready", "waited=" + waited + "ms");
            return _handler.GetScreenshotInfo(_screenshotPath);
        }

        // --- HTTP Response Helpers ---

        private void SendResult(HttpListenerContext ctx, ServiceResult result)
        {
            try
            {
                if (result.Binary != null)
                {
                    ctx.Response.ContentType = result.ContentType;
                    ctx.Response.ContentLength64 = result.Binary.Length;
                    ctx.Response.StatusCode = result.StatusCode;
                    ctx.Response.OutputStream.Write(result.Binary, 0, result.Binary.Length);
                    ctx.Response.OutputStream.Close();
                }
                else
                {
                    SendJson(ctx, result.StatusCode, result.Json);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("HTTP", "SendResult failed", ex);
            }
        }

        private void SendJson(HttpListenerContext ctx, int statusCode, string json)
        {
            try
            {
                byte[] buf = Encoding.UTF8.GetBytes(json);
                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentLength64 = buf.Length;
                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                ctx.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Logger.Error("HTTP", "SendJson failed", ex);
            }
        }
    }
}
