using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace ClaudeAdvisor
{
    public static class CityDataCollector
    {
        public static Dictionary<string, object> GetFullStats()
        {
            var data = new Dictionary<string, object>();

            var sim = Singleton<SimulationManager>.instance;
            string cityName = (sim.m_metaData != null && sim.m_metaData.m_CityName != null)
                ? sim.m_metaData.m_CityName : "Unknown";

            data["cityName"] = cityName;
            data["exportTime"] = DateTime.Now.ToString("o");
            data["paused"] = sim.SimulationPaused;
            data["speed"] = (int)sim.SelectedSimulationSpeed;

            var dm = Singleton<DistrictManager>.instance;
            District city = dm.m_districts.m_buffer[0];
            uint pop = city.m_populationData.m_finalCount;

            data["population"] = (int)pop;
            data["populationChildren"] = (int)city.m_childData.m_finalCount;
            data["populationTeens"] = (int)city.m_teenData.m_finalCount;
            data["populationYoungAdults"] = (int)city.m_youngData.m_finalCount;
            data["populationAdults"] = (int)city.m_adultData.m_finalCount;
            data["populationSeniors"] = (int)city.m_seniorData.m_finalCount;

            var econ = Singleton<EconomyManager>.instance;
            long money = econ.LastCashAmount;
            long delta = econ.LastCashDelta;
            data["money"] = money;
            data["moneyFormatted"] = "$" + (money / 100).ToString("N0");
            data["weeklyProfit"] = delta;

            var zm = Singleton<ZoneManager>.instance;
            data["demandResidential"] = zm.m_residentialDemand;
            data["demandCommercial"] = zm.m_commercialDemand;
            data["demandWorkplace"] = zm.m_workplaceDemand;

            data["services"] = GetServices(city);
            data["buildings"] = GetBuildingSummary();
            data["traffic"] = GetTrafficSummary();
            data["transport"] = GetTransportSummary();

            return data;
        }

        public static Dictionary<string, object> GetServices(District city)
        {
            var s = new Dictionary<string, object>();
            s["electricityCapacity"] = (int)city.GetElectricityCapacity();
            s["electricityConsumption"] = (int)city.GetElectricityConsumption();
            s["waterCapacity"] = (int)city.GetWaterCapacity();
            s["waterConsumption"] = (int)city.GetWaterConsumption();
            s["sewageCapacity"] = (int)city.GetSewageCapacity();
            s["sewageAccumulation"] = (int)city.GetSewageAccumulation();
            s["garbageCapacity"] = (int)city.GetGarbageCapacity();
            s["garbageAccumulation"] = (int)city.GetGarbageAccumulation();
            s["heatingCapacity"] = (int)city.GetHeatingCapacity();
            s["heatingConsumption"] = (int)city.GetHeatingConsumption();
            s["crimeRate"] = (int)city.m_finalCrimeRate;
            s["happiness"] = (int)city.m_finalHappiness;
            s["education1Rate"] = (int)city.GetEducation1Rate();
            s["education2Rate"] = (int)city.GetEducation2Rate();
            s["education3Rate"] = (int)city.GetEducation3Rate();
            s["deadCount"] = (int)city.GetDeadCount();
            s["deadCapacity"] = (int)city.GetDeadCapacity();
            s["hospitalCount"] = (int)city.GetHospitalCount();
            s["healCapacity"] = (int)city.GetHealCapacity();
            s["landValue"] = (int)city.GetLandValue();
            s["groundPollution"] = (int)city.GetGroundPollution();
            return s;
        }

        public static Dictionary<string, object> GetBuildingSummary()
        {
            var bm = Singleton<BuildingManager>.instance;
            var b = new Dictionary<string, object>();
            b["total"] = bm.m_buildingCount;

            int res = 0, com = 0, ind = 0, ofc = 0, abn = 0, brn = 0;
            Building[] blds = bm.m_buildings.m_buffer;
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
            b["residential"] = res;
            b["commercial"] = com;
            b["industrial"] = ind;
            b["office"] = ofc;
            b["abandoned"] = abn;
            b["burned"] = brn;
            return b;
        }

        public static List<Dictionary<string, object>> GetBuildingsList(string typeFilter, string flagFilter, int limit)
        {
            var results = new List<Dictionary<string, object>>();
            var bm = Singleton<BuildingManager>.instance;
            Building[] blds = bm.m_buildings.m_buffer;

            for (int i = 0; i < blds.Length && results.Count < limit; i++)
            {
                if (blds[i].m_flags == Building.Flags.None) continue;
                BuildingInfo info = blds[i].Info;
                if (info == null || info.m_class == null) continue;

                if (flagFilter == "abandoned" && (blds[i].m_flags & Building.Flags.Abandoned) == 0) continue;
                if (flagFilter == "burned" && (blds[i].m_flags & Building.Flags.BurnedDown) == 0) continue;

                string svc = info.m_class.m_service.ToString().ToLower();
                if (!string.IsNullOrEmpty(typeFilter) && svc != typeFilter.ToLower()) continue;

                var bd = new Dictionary<string, object>();
                bd["id"] = i;
                bd["name"] = info.name ?? "Unknown";
                bd["service"] = svc;
                bd["abandoned"] = (blds[i].m_flags & Building.Flags.Abandoned) != 0;
                bd["burned"] = (blds[i].m_flags & Building.Flags.BurnedDown) != 0;
                bd["posX"] = (float)blds[i].m_position.x;
                bd["posZ"] = (float)blds[i].m_position.z;
                results.Add(bd);
            }
            return results;
        }

        public static Dictionary<string, object> GetTrafficSummary()
        {
            var t = new Dictionary<string, object>();
            var nm = Singleton<NetManager>.instance;
            NetSegment[] segs = nm.m_segments.m_buffer;

            long totalDensity = 0;
            int segCount = 0;
            var congested = new List<Dictionary<string, object>>();

            for (int i = 0; i < segs.Length; i++)
            {
                if (segs[i].m_flags == NetSegment.Flags.None) continue;
                if (segs[i].Info == null || segs[i].Info.m_class == null) continue;
                if (segs[i].Info.m_class.m_service != ItemClass.Service.Road) continue;

                totalDensity += (long)segs[i].m_trafficDensity;
                segCount++;

                if (segs[i].m_trafficDensity > 70 && congested.Count < 15)
                {
                    var r = new Dictionary<string, object>();
                    r["id"] = i;
                    r["name"] = segs[i].Info.name ?? "Unknown";
                    r["density"] = (int)segs[i].m_trafficDensity;
                    congested.Add(r);
                }
            }

            int avgD = segCount > 0 ? (int)(totalDensity / segCount) : 0;
            t["roadSegments"] = segCount;
            t["avgDensity"] = avgD;
            t["flowPercent"] = Math.Max(0, 100 - avgD);
            t["congestedRoads"] = congested;
            return t;
        }

        public static Dictionary<string, object> GetTransportSummary()
        {
            var t = new Dictionary<string, object>();
            var tm = Singleton<TransportManager>.instance;
            int bus = 0, metro = 0, train = 0, tram = 0, other = 0;
            TransportLine[] tl = tm.m_lines.m_buffer;

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
            t["bus"] = bus;
            t["metro"] = metro;
            t["train"] = train;
            t["tram"] = tram;
            t["other"] = other;
            return t;
        }

        public static Dictionary<string, object> GetBudgetInfo()
        {
            var b = new Dictionary<string, object>();
            var econ = Singleton<EconomyManager>.instance;
            long money = econ.LastCashAmount;
            b["money"] = money;
            b["moneyFormatted"] = "$" + (money / 100).ToString("N0");
            b["weeklyProfit"] = econ.LastCashDelta;
            return b;
        }

        public static List<Dictionary<string, object>> GetDistrictsList()
        {
            var results = new List<Dictionary<string, object>>();
            var dm = Singleton<DistrictManager>.instance;

            for (int i = 0; i < 128; i++)
            {
                if ((dm.m_districts.m_buffer[i].m_flags & District.Flags.Created) == 0) continue;

                var d = new Dictionary<string, object>();
                d["id"] = i;
                d["name"] = i == 0 ? "City" : dm.GetDistrictName(i);
                d["population"] = (int)dm.m_districts.m_buffer[i].m_populationData.m_finalCount;
                d["happiness"] = (int)dm.m_districts.m_buffer[i].m_finalHappiness;
                results.Add(d);
            }
            return results;
        }
    }
}
