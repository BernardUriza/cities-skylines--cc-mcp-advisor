using System;
using System.IO;
using System.Text;
using ICities;
using UnityEngine;
using ColossalFramework;

namespace ClaudeAdvisor
{
    public class ClaudeAdvisorMod : IUserMod
    {
        public string Name { get { return "Claude City Advisor"; } }
        public string Description { get { return "Exports city stats to JSON so Claude Code can read and roast your city"; } }
    }

    public class ClaudeAdvisorLoading : LoadingExtensionBase
    {
        private static GameObject _gameObject;

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (mode == LoadMode.LoadGame || mode == LoadMode.NewGame)
            {
                _gameObject = new GameObject("ClaudeAdvisorExporter");
                _gameObject.AddComponent<CityExporter>();
                Debug.Log("[ClaudeAdvisor] City Advisor loaded!");
            }
        }

        public override void OnLevelUnloading()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }
    }

    public class CityExporter : MonoBehaviour
    {
        private float _timer = 0f;
        private float _interval = 30f;
        private string _exportPath;

        void Start()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            _exportPath = Path.Combine(home, "Library/Application Support/Colossal Order/Cities_Skylines/claude_city_report.json");
            if (!Directory.Exists(Path.GetDirectoryName(_exportPath)))
                _exportPath = Path.Combine(Application.dataPath, "../claude_city_report.json");
            try { ExportStats(); Debug.Log("[ClaudeAdvisor] First export to: " + _exportPath); }
            catch (Exception ex) { Debug.LogError("[ClaudeAdvisor] Export failed: " + ex.ToString()); }
        }

        void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _interval)
            {
                _timer = 0f;
                try { ExportStats(); }
                catch (Exception ex) { Debug.LogError("[ClaudeAdvisor] " + ex.Message); }
            }
        }

        void ExportStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            SimulationManager sim = Singleton<SimulationManager>.instance;
            string cityName = (sim.m_metaData != null && sim.m_metaData.m_CityName != null) ? sim.m_metaData.m_CityName : "Unknown";
            sb.AppendLine("  \"cityName\": \"" + Esc(cityName) + "\",");
            sb.AppendLine("  \"exportTime\": \"" + DateTime.Now.ToString("o") + "\",");
            DistrictManager districtMgr = Singleton<DistrictManager>.instance;
            District city = districtMgr.m_districts.m_buffer[0];
            uint pop = city.m_populationData.m_finalCount;
            sb.AppendLine("  \"population\": " + pop + ",");
            sb.AppendLine("  \"populationChildren\": " + city.m_childData.m_finalCount + ",");
            sb.AppendLine("  \"populationTeens\": " + city.m_teenData.m_finalCount + ",");
            sb.AppendLine("  \"populationYoungAdults\": " + city.m_youngData.m_finalCount + ",");
            sb.AppendLine("  \"populationAdults\": " + city.m_adultData.m_finalCount + ",");
            sb.AppendLine("  \"populationSeniors\": " + city.m_seniorData.m_finalCount + ",");
            EconomyManager economy = Singleton<EconomyManager>.instance;
            long money = economy.LastCashAmount;
            long delta = economy.LastCashDelta;
            sb.AppendLine("  \"money\": " + money + ",");
            sb.AppendLine("  \"moneyFormatted\": \"$" + (money / 100).ToString("N0") + "\",");
            sb.AppendLine("  \"weeklyProfit\": " + delta + ",");
            ZoneManager zoneMgr = Singleton<ZoneManager>.instance;
            sb.AppendLine("  \"demandResidential\": " + zoneMgr.m_residentialDemand + ",");
            sb.AppendLine("  \"demandCommercial\": " + zoneMgr.m_commercialDemand + ",");
            sb.AppendLine("  \"demandWorkplace\": " + zoneMgr.m_workplaceDemand + ",");
            sb.AppendLine("  \"services\": {");
            sb.AppendLine("    \"electricityCapacity\": " + city.GetElectricityCapacity() + ",");
            sb.AppendLine("    \"electricityConsumption\": " + city.GetElectricityConsumption() + ",");
            sb.AppendLine("    \"waterCapacity\": " + city.GetWaterCapacity() + ",");
            sb.AppendLine("    \"waterConsumption\": " + city.GetWaterConsumption() + ",");
            sb.AppendLine("    \"sewageCapacity\": " + city.GetSewageCapacity() + ",");
            sb.AppendLine("    \"sewageAccumulation\": " + city.GetSewageAccumulation() + ",");
            sb.AppendLine("    \"garbageCapacity\": " + city.GetGarbageCapacity() + ",");
            sb.AppendLine("    \"garbageAccumulation\": " + city.GetGarbageAccumulation() + ",");
            sb.AppendLine("    \"heatingCapacity\": " + city.GetHeatingCapacity() + ",");
            sb.AppendLine("    \"heatingConsumption\": " + city.GetHeatingConsumption() + ",");
            sb.AppendLine("    \"crimeRate\": " + city.m_finalCrimeRate + ",");
            sb.AppendLine("    \"happiness\": " + city.m_finalHappiness + ",");
            sb.AppendLine("    \"education1Rate\": " + city.GetEducation1Rate() + ",");
            sb.AppendLine("    \"education2Rate\": " + city.GetEducation2Rate() + ",");
            sb.AppendLine("    \"education3Rate\": " + city.GetEducation3Rate() + ",");
            sb.AppendLine("    \"deadCount\": " + city.GetDeadCount() + ",");
            sb.AppendLine("    \"deadCapacity\": " + city.GetDeadCapacity() + ",");
            sb.AppendLine("    \"hospitalCount\": " + city.GetHospitalCount() + ",");
            sb.AppendLine("    \"healCapacity\": " + city.GetHealCapacity() + ",");
            sb.AppendLine("    \"landValue\": " + city.GetLandValue() + ",");
            sb.AppendLine("    \"groundPollution\": " + city.GetGroundPollution() + ",");
            sb.AppendLine("    \"incomeAccumulation\": " + city.GetIncomeAccumulation());
            sb.AppendLine("  },");
            BuildingManager buildingMgr = Singleton<BuildingManager>.instance;
            sb.AppendLine("  \"totalBuildings\": " + buildingMgr.m_buildingCount + ",");
            int res = 0, com = 0, ind = 0, ofc = 0, abn = 0, brn = 0;
            Building[] blds = buildingMgr.m_buildings.m_buffer;
            for (int i = 0; i < blds.Length; i++)
            {
                if (blds[i].m_flags == Building.Flags.None) continue;
                BuildingInfo info = blds[i].Info;
                if (info == null) continue;
                if ((blds[i].m_flags & Building.Flags.Abandoned) != 0) abn++;
                if ((blds[i].m_flags & Building.Flags.BurnedDown) != 0) brn++;
                if (info.m_class != null)
                {
                    switch (info.m_class.m_service)
                    {
                        case ItemClass.Service.Residential: res++; break;
                        case ItemClass.Service.Commercial: com++; break;
                        case ItemClass.Service.Industrial: ind++; break;
                        case ItemClass.Service.Office: ofc++; break;
                    }
                }
            }
            sb.AppendLine("  \"residentialBuildings\": " + res + ",");
            sb.AppendLine("  \"commercialBuildings\": " + com + ",");
            sb.AppendLine("  \"industrialBuildings\": " + ind + ",");
            sb.AppendLine("  \"officeBuildings\": " + ofc + ",");
            sb.AppendLine("  \"abandonedBuildings\": " + abn + ",");
            sb.AppendLine("  \"burnedBuildings\": " + brn + ",");
            VehicleManager vehicleMgr = Singleton<VehicleManager>.instance;
            sb.AppendLine("  \"activeVehicles\": " + vehicleMgr.m_vehicleCount + ",");
            sb.AppendLine("  \"parkedVehicles\": " + vehicleMgr.m_parkedCount + ",");
            NetManager netMgr = Singleton<NetManager>.instance;
            long totalDensity = 0; int segCount = 0;
            NetSegment[] segs = netMgr.m_segments.m_buffer;
            for (int i = 0; i < segs.Length; i++)
            {
                if (segs[i].m_flags == NetSegment.Flags.None) continue;
                if (segs[i].Info != null && segs[i].Info.m_class != null && segs[i].Info.m_class.m_service == ItemClass.Service.Road)
                { totalDensity += (long)segs[i].m_trafficDensity; segCount++; }
            }
            int avgD = segCount > 0 ? (int)(totalDensity / segCount) : 0;
            int flow = Math.Max(0, 100 - avgD);
            sb.AppendLine("  \"roadSegments\": " + segCount + ",");
            sb.AppendLine("  \"avgTrafficDensity\": " + avgD + ",");
            sb.AppendLine("  \"trafficFlowPercent\": " + flow + ",");
            sb.AppendLine("  \"congestedRoads\": [");
            int cg = 0;
            for (int i = 0; i < segs.Length && cg < 10; i++)
            {
                if (segs[i].m_flags == NetSegment.Flags.None) continue;
                if (segs[i].m_trafficDensity > 70 && segs[i].Info != null)
                {
                    string rn = segs[i].Info.name != null ? segs[i].Info.name : "Unknown";
                    if (cg > 0) sb.Append(",\n");
                    sb.Append("    {\"id\": " + i + ", \"name\": \"" + Esc(rn) + "\", \"density\": " + segs[i].m_trafficDensity + "}");
                    cg++;
                }
            }
            sb.AppendLine("\n  ],");
            TransportManager tMgr = Singleton<TransportManager>.instance;
            int bus=0, metro=0, train=0, tram=0, other=0;
            TransportLine[] tl = tMgr.m_lines.m_buffer;
            for (int i = 0; i < tl.Length; i++)
            {
                if ((tl[i].m_flags & TransportLine.Flags.Created) == 0) continue;
                if (tl[i].Info == null) continue;
                switch (tl[i].Info.m_transportType)
                {
                    case TransportInfo.TransportType.Bus: bus++; break;
                    case TransportInfo.TransportType.Metro: metro++; break;
                    case TransportInfo.TransportType.Train: train++; break;
                    case TransportInfo.TransportType.Tram: tram++; break;
                    default: other++; break;
                }
            }
            sb.AppendLine("  \"transportLines\": { \"bus\": "+bus+", \"metro\": "+metro+", \"train\": "+train+", \"tram\": "+tram+", \"other\": "+other+" },");
            sb.AppendLine("  \"problems\": [");
            string p = "";
            if (abn > 5) p += "    \"High abandoned: " + abn + "\",\n";
            if (brn > 0) p += "    \"Burned: " + brn + " - fire failing!\",\n";
            if (flow < 70) p += "    \"CRITICAL traffic: " + flow + "%\",\n";
            else if (flow < 85) p += "    \"Traffic warning: " + flow + "%\",\n";
            if (city.m_finalCrimeRate > 30) p += "    \"High crime: " + city.m_finalCrimeRate + "\",\n";
            int dc = city.GetDeadCount();
            if (dc > (int)(pop*0.02f) && dc > 5) p += "    \"Death wave! " + dc + " dead\",\n";
            if (money < 0) p += "    \"IN DEBT!\",\n";
            if (delta < 0) p += "    \"Losing money!\",\n";
            if (pop > 500 && bus+metro+train+tram == 0) p += "    \"No public transport!\",\n";
            sb.AppendLine(p.TrimEnd('\n',','));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"advice\": [");
            string a = "";
            if (zoneMgr.m_residentialDemand > 60) a += "    \"Zone more residential (demand:" + zoneMgr.m_residentialDemand + ")\",\n";
            if (zoneMgr.m_commercialDemand > 60) a += "    \"Zone more commercial (demand:" + zoneMgr.m_commercialDemand + ")\",\n";
            if (zoneMgr.m_workplaceDemand > 60) a += "    \"Zone more industry/offices (demand:" + zoneMgr.m_workplaceDemand + ")\",\n";
            if (pop > 5000 && metro == 0) a += "    \"Add metro lines!\",\n";
            if (avgD > 50) a += "    \"High traffic - add roundabouts\",\n";
            if (city.m_finalHappiness < 50) a += "    \"Low happiness (" + city.m_finalHappiness + ")\",\n";
            sb.AppendLine(a.TrimEnd('\n',','));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(_exportPath, sb.ToString());
        }

        static string Esc(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
