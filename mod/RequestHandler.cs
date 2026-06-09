using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace ClaudeAdvisor
{
    public class ServiceResult
    {
        public int StatusCode;
        public string Json;
        public byte[] Binary;
        public string ContentType;

        public static ServiceResult Ok(string json)
        {
            return new ServiceResult { StatusCode = 200, Json = WrapSuccess(json), ContentType = "application/json" };
        }

        public static ServiceResult Error(int statusCode, string message)
        {
            Logger.Warn("Response", "Error " + statusCode, message);
            return new ServiceResult { StatusCode = statusCode, Json = WrapError(message), ContentType = "application/json" };
        }

        public static ServiceResult Image(byte[] data)
        {
            return new ServiceResult { StatusCode = 200, Binary = data, ContentType = "image/png" };
        }

        private static string WrapSuccess(string dataJson)
        {
            return "{\"success\":true,\"data\":" + dataJson
                + ",\"error\":null,\"correlationId\":\"" + Logger.CorrelationId
                + "\",\"timestamp\":\"" + DateTime.Now.ToString("o") + "\"}";
        }

        private static string WrapError(string message)
        {
            return "{\"success\":false,\"data\":null,\"error\":\"" + JsonHelper.Escape(message)
                + "\",\"correlationId\":\"" + Logger.CorrelationId
                + "\",\"timestamp\":\"" + DateTime.Now.ToString("o") + "\"}";
        }
    }

    public class RequestHandler
    {
        private const int PORT = 7828;
        private static readonly string SCREENSHOT_DIR = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            "Library/Application Support/Colossal Order/Cities_Skylines"
        );

        // --- Read Handlers ---

        public ServiceResult Health()
        {
            return new ServiceResult
            {
                StatusCode = 200,
                Json = JsonHelper.ToJson(new Dictionary<string, object> {
                    {"status", "ok"}, {"mod", "ClaudeAdvisor MCP"}, {"port", PORT}
                }),
                ContentType = "application/json"
            };
        }

        public ServiceResult GetStats()
        {
            try
            {
                var stats = CityDataCollector.GetFullStats();
                return ServiceResult.Ok(JsonHelper.ToJson(stats));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetStats failed", ex);
                return ServiceResult.Error(500, "Failed to collect city stats: " + ex.Message);
            }
        }

        public ServiceResult GetBuildings(string typeFilter, string flagFilter, int limit)
        {
            try
            {
                Logger.Debug("Handler", "GetBuildings", "type=" + typeFilter + " flags=" + flagFilter + " limit=" + limit);
                var buildings = CityDataCollector.GetBuildingsList(typeFilter, flagFilter, limit);
                Logger.Debug("Handler", "GetBuildings result", "count=" + buildings.Count);
                return ServiceResult.Ok("{\"buildings\":" + JsonHelper.ValueToJson(buildings) + ",\"count\":" + buildings.Count + "}");
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetBuildings failed", ex);
                return ServiceResult.Error(500, "Failed to list buildings: " + ex.Message);
            }
        }

        public ServiceResult GetTraffic()
        {
            try
            {
                var traffic = CityDataCollector.GetTrafficSummary();
                return ServiceResult.Ok(JsonHelper.ToJson(traffic));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetTraffic failed", ex);
                return ServiceResult.Error(500, "Failed to collect traffic data: " + ex.Message);
            }
        }

        public ServiceResult GetTransport()
        {
            try
            {
                var transport = CityDataCollector.GetTransportSummary();
                return ServiceResult.Ok(JsonHelper.ToJson(transport));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetTransport failed", ex);
                return ServiceResult.Error(500, "Failed to collect transport data: " + ex.Message);
            }
        }

        public ServiceResult GetDistricts()
        {
            try
            {
                var districts = CityDataCollector.GetDistrictsList();
                return ServiceResult.Ok("{\"districts\":" + JsonHelper.ValueToJson(districts) + "}");
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetDistricts failed", ex);
                return ServiceResult.Error(500, "Failed to collect district data: " + ex.Message);
            }
        }

        public ServiceResult GetBudget()
        {
            try
            {
                var budget = CityDataCollector.GetBudgetDetailed();
                return ServiceResult.Ok(JsonHelper.ToJson(budget));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetBudget failed", ex);
                return ServiceResult.Error(500, "Failed to collect budget data: " + ex.Message);
            }
        }

        public ServiceResult GetProblems()
        {
            try
            {
                var problems = CityDataCollector.GetProblems();
                return ServiceResult.Ok(JsonHelper.ToJson(problems));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetProblems failed", ex);
                return ServiceResult.Error(500, "Failed to collect problem data: " + ex.Message);
            }
        }

        public ServiceResult GetChanges()
        {
            try
            {
                var changes = CityDataCollector.GetChanges();
                return ServiceResult.Ok(JsonHelper.ToJson(changes));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetChanges failed", ex);
                return ServiceResult.Error(500, "Failed to compute changes: " + ex.Message);
            }
        }

        public ServiceResult GetScreenshotInfo(string screenshotPath)
        {
            if (!string.IsNullOrEmpty(screenshotPath) && File.Exists(screenshotPath))
            {
                var info = new FileInfo(screenshotPath);
                Logger.Info("Handler", "Screenshot info", "size=" + (info.Length / 1024) + "KB");
                return ServiceResult.Ok(JsonHelper.ToJson(new Dictionary<string, object> {
                    {"action", "screenshot"},
                    {"path", screenshotPath},
                    {"size_kb", (int)(info.Length / 1024)},
                    {"imageUrl", "http://localhost:" + PORT + "/api/v1/screenshot/image"},
                    {"timestamp", DateTime.Now.ToString("o")}
                }));
            }
            Logger.Error("Handler", "Screenshot file not found at " + (screenshotPath ?? "null"));
            return ServiceResult.Error(500, "Screenshot file not found after capture");
        }

        public ServiceResult GetScreenshotImage()
        {
            string path = Path.Combine(SCREENSHOT_DIR, "claude_screenshot.png");
            if (!File.Exists(path))
            {
                Logger.Warn("Handler", "Screenshot image not found", "path=" + path);
                return ServiceResult.Error(404, "No screenshot available. Call /api/v1/screenshot first.");
            }

            try
            {
                byte[] imageBytes = File.ReadAllBytes(path);
                Logger.Debug("Handler", "Screenshot image read", "bytes=" + imageBytes.Length);
                return ServiceResult.Image(imageBytes);
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "Screenshot image read failed", ex);
                return ServiceResult.Error(500, "Failed to read screenshot: " + ex.Message);
            }
        }

        public ServiceResult GetTrafficGraph(int limit, int minDensity)
        {
            try
            {
                Logger.Debug("Handler", "GetTrafficGraph", "limit=" + limit + " minDensity=" + minDensity);
                var graph = CityDataCollector.GetTrafficGraph(limit, minDensity);
                return ServiceResult.Ok(JsonHelper.ToJson(graph));
            }
            catch (Exception ex)
            {
                Logger.Error("Handler", "GetTrafficGraph failed", ex);
                return ServiceResult.Error(500, "Failed to collect traffic graph: " + ex.Message);
            }
        }

        // --- Action Handlers ---

        public ServiceResult Demolish(Dictionary<string, string> body)
        {
            string idStr;
            if (!body.TryGetValue("buildingId", out idStr))
                return ServiceResult.Error(400, "Missing buildingId");

            int buildingId;
            if (!int.TryParse(idStr, out buildingId))
                return ServiceResult.Error(400, "Invalid buildingId: " + idStr);

            Logger.Info("Handler", "Demolish requested", "buildingId=" + buildingId);
            GameActionExecutor.DemolishBuilding((ushort)buildingId);
            return ServiceResult.Ok("{\"action\":\"demolish\",\"buildingId\":" + buildingId + ",\"queued\":true}");
        }

        public ServiceResult DemolishAbandoned()
        {
            Logger.Info("Handler", "DemolishAbandoned requested");
            int count = GameActionExecutor.DemolishAllAbandoned();
            Logger.Info("Handler", "DemolishAbandoned queued", "count=" + count);
            return ServiceResult.Ok("{\"action\":\"demolish-abandoned\",\"count\":" + count + ",\"queued\":true}");
        }

        public ServiceResult AddMoney(Dictionary<string, string> body)
        {
            string amtStr;
            if (!body.TryGetValue("amount", out amtStr))
                return ServiceResult.Error(400, "Missing amount");

            int amount;
            if (!int.TryParse(amtStr, out amount))
                return ServiceResult.Error(400, "Invalid amount: " + amtStr);

            Logger.Info("Handler", "AddMoney requested", "amount=" + amount);
            GameActionExecutor.InjectMoney(amount);
            return ServiceResult.Ok("{\"action\":\"money\",\"amount\":" + amount + ",\"queued\":true}");
        }

        public ServiceResult SetTax(Dictionary<string, string> body)
        {
            string rateStr;
            if (!body.TryGetValue("rate", out rateStr))
                return ServiceResult.Error(400, "Missing rate");

            int rate;
            if (!int.TryParse(rateStr, out rate) || rate < 0 || rate > 29)
                return ServiceResult.Error(400, "Invalid rate (0-29): " + rateStr);

            string service = "";
            body.TryGetValue("service", out service);
            Logger.Info("Handler", "SetTax requested", "service=" + service + " rate=" + rate);
            GameActionExecutor.SetTaxRate(service ?? "Residential", rate);
            return ServiceResult.Ok("{\"action\":\"tax\",\"service\":\"" + JsonHelper.Escape(service) + "\",\"rate\":" + rate + ",\"queued\":true}");
        }

        public ServiceResult SetBudget(Dictionary<string, string> body)
        {
            string budgetStr;
            if (!body.TryGetValue("budget", out budgetStr))
                return ServiceResult.Error(400, "Missing budget");

            int budget;
            if (!int.TryParse(budgetStr, out budget) || budget < 50 || budget > 150)
                return ServiceResult.Error(400, "Invalid budget (50-150): " + budgetStr);

            string service = "";
            body.TryGetValue("service", out service);
            Logger.Info("Handler", "SetBudget requested", "service=" + service + " budget=" + budget);
            GameActionExecutor.SetBudget(service ?? "HealthCare", budget);
            return ServiceResult.Ok("{\"action\":\"budget\",\"service\":\"" + JsonHelper.Escape(service) + "\",\"budget\":" + budget + ",\"queued\":true}");
        }

        public ServiceResult SetSpeed(Dictionary<string, string> body)
        {
            string speedStr;
            if (!body.TryGetValue("speed", out speedStr))
                return ServiceResult.Error(400, "Missing speed");

            int speed;
            if (!int.TryParse(speedStr, out speed) || speed < 1 || speed > 3)
                return ServiceResult.Error(400, "Invalid speed (1-3): " + speedStr);

            Logger.Info("Handler", "SetSpeed requested", "speed=" + speed);
            GameActionExecutor.SetSpeed(speed);
            return ServiceResult.Ok("{\"action\":\"speed\",\"speed\":" + speed + ",\"queued\":true}");
        }

        public ServiceResult SetPaused(Dictionary<string, string> body)
        {
            string pausedStr;
            if (!body.TryGetValue("paused", out pausedStr))
                return ServiceResult.Error(400, "Missing paused");

            bool paused = pausedStr == "true" || pausedStr == "1";
            Logger.Info("Handler", "SetPaused requested", "paused=" + paused);
            GameActionExecutor.SetPaused(paused);
            return ServiceResult.Ok("{\"action\":\"pause\",\"paused\":" + (paused ? "true" : "false") + ",\"queued\":true}");
        }

        public ServiceResult SendChirp(Dictionary<string, string> body)
        {
            string message;
            if (!body.TryGetValue("message", out message) || string.IsNullOrEmpty(message))
                return ServiceResult.Error(400, "Missing message");

            Logger.Info("Handler", "SendChirp requested", "length=" + message.Length);
            GameActionExecutor.SendChirperMessage(message);
            return ServiceResult.Ok("{\"action\":\"chirp\",\"message\":\"" + JsonHelper.Escape(message) + "\",\"queued\":true}");
        }
    }
}
