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

        public static Dictionary<string, object> GetProblems()
        {
            var result = new Dictionary<string, object>();
            var bm = Singleton<BuildingManager>.instance;
            Building[] blds = bm.m_buildings.m_buffer;

            var problemCounts = new Dictionary<string, int>();
            var problemBuildings = new List<Dictionary<string, object>>();

            string[] problemNames = new string[] {
                "Garbage", "Electricity", "Water", "Fire", "DirtyWater",
                "Crime", "Pollution", "TurnedOff", "TooFewServices",
                "LandValueLow", "ElectricityNotConnected", "NoFuel",
                "RoadNotConnected", "WaterNotConnected", "Sewage",
                "Death", "LandfillFull", "LineNotConnected",
                "NoCustomers", "NoResources", "NoGoods",
                "NoPlaceforGoods", "NoWorkers", "NoEducatedWorkers"
            };
            Notification.Problem1[] problemFlags = new Notification.Problem1[] {
                Notification.Problem1.Garbage, Notification.Problem1.Electricity,
                Notification.Problem1.Water, Notification.Problem1.Fire,
                Notification.Problem1.DirtyWater, Notification.Problem1.Crime,
                Notification.Problem1.Pollution, Notification.Problem1.TurnedOff,
                Notification.Problem1.TooFewServices, Notification.Problem1.LandValueLow,
                Notification.Problem1.ElectricityNotConnected, Notification.Problem1.NoFuel,
                Notification.Problem1.RoadNotConnected, Notification.Problem1.WaterNotConnected,
                Notification.Problem1.Sewage, Notification.Problem1.Death,
                Notification.Problem1.LandfillFull, Notification.Problem1.LineNotConnected,
                Notification.Problem1.NoCustomers, Notification.Problem1.NoResources,
                Notification.Problem1.NoGoods, Notification.Problem1.NoPlaceforGoods,
                Notification.Problem1.NoWorkers, Notification.Problem1.NoEducatedWorkers
            };

            foreach (string pn in problemNames)
                problemCounts[pn] = 0;

            for (int i = 0; i < blds.Length; i++)
            {
                if (blds[i].m_flags == Building.Flags.None) continue;
                BuildingInfo info = blds[i].Info;
                if (info == null) continue;

                Notification.Problem1 p1 = blds[i].m_problems.m_Problems1;
                if (p1 == Notification.Problem1.None) continue;

                var buildingProblems = new List<object>();

                for (int j = 0; j < problemFlags.Length; j++)
                {
                    if ((p1 & problemFlags[j]) != 0)
                    {
                        problemCounts[problemNames[j]]++;
                        buildingProblems.Add(problemNames[j]);
                    }
                }

                if (buildingProblems.Count > 0 && problemBuildings.Count < 50)
                {
                    var bd = new Dictionary<string, object>();
                    bd["id"] = i;
                    bd["name"] = info.name ?? "Unknown";
                    bd["service"] = (info.m_class != null) ? info.m_class.m_service.ToString().ToLower() : "unknown";
                    bd["problems"] = buildingProblems;
                    bd["posX"] = (float)blds[i].m_position.x;
                    bd["posZ"] = (float)blds[i].m_position.z;
                    problemBuildings.Add(bd);
                }
            }

            var summary = new Dictionary<string, object>();
            int totalProblems = 0;
            foreach (var kv in problemCounts)
            {
                if (kv.Value > 0)
                {
                    summary[kv.Key] = kv.Value;
                    totalProblems += kv.Value;
                }
            }

            result["totalProblems"] = totalProblems;
            result["summary"] = summary;
            result["buildings"] = problemBuildings;
            return result;
        }

        public static Dictionary<string, object> GetBudgetDetailed()
        {
            var b = new Dictionary<string, object>();
            var econ = Singleton<EconomyManager>.instance;
            long money = econ.LastCashAmount;
            b["money"] = money;
            b["moneyFormatted"] = "$" + (money / 100).ToString("N0");
            b["weeklyProfit"] = econ.LastCashDelta;

            var income = new Dictionary<string, object>();
            var expenses = new Dictionary<string, object>();

            string[] serviceNames = new string[] {
                "Residential", "Commercial", "Industrial", "Office",
                "Road", "Electricity", "Water", "Garbage",
                "HealthCare", "FireDepartment", "PoliceDepartment",
                "Education", "Monument", "Beautification"
            };
            ItemClass.Service[] services = new ItemClass.Service[] {
                ItemClass.Service.Residential, ItemClass.Service.Commercial,
                ItemClass.Service.Industrial, ItemClass.Service.Office,
                ItemClass.Service.Road, ItemClass.Service.Electricity,
                ItemClass.Service.Water, ItemClass.Service.Garbage,
                ItemClass.Service.HealthCare, ItemClass.Service.FireDepartment,
                ItemClass.Service.PoliceDepartment, ItemClass.Service.Education,
                ItemClass.Service.Monument, ItemClass.Service.Beautification
            };

            long totalIncome = 0;
            long totalExpenses = 0;

            for (int i = 0; i < services.Length; i++)
            {
                long inc = 0;
                long exp = 0;
                econ.GetIncomeAndExpenses(services[i], ItemClass.SubService.None,
                    ItemClass.Level.None, out inc, out exp);
                if (inc > 0)
                {
                    income[serviceNames[i]] = inc / 100;
                    totalIncome += inc;
                }
                if (exp > 0)
                {
                    expenses[serviceNames[i]] = exp / 100;
                    totalExpenses += exp;
                }
            }

            b["totalIncome"] = totalIncome / 100;
            b["totalExpenses"] = totalExpenses / 100;
            b["income"] = income;
            b["expenses"] = expenses;
            return b;
        }

        // --- Traffic Graph (for GNN / PyTorch) ---

        public static Dictionary<string, object> GetTrafficGraph(int limit, int minDensity)
        {
            var result = new Dictionary<string, object>();
            var nm = Singleton<NetManager>.instance;
            NetSegment[] segs = nm.m_segments.m_buffer;
            NetNode[] nodes = nm.m_nodes.m_buffer;

            var edges = new List<Dictionary<string, object>>();
            var nodeIds = new HashSet<int>();

            for (int i = 0; i < segs.Length && edges.Count < limit; i++)
            {
                if (segs[i].m_flags == NetSegment.Flags.None) continue;
                if (segs[i].Info == null || segs[i].Info.m_class == null) continue;
                if (segs[i].Info.m_class.m_service != ItemClass.Service.Road) continue;

                int density = (int)segs[i].m_trafficDensity;
                if (density < minDensity) continue;

                int startNode = (int)segs[i].m_startNode;
                int endNode = (int)segs[i].m_endNode;

                var edge = new Dictionary<string, object>();
                edge["id"] = i;
                edge["startNode"] = startNode;
                edge["endNode"] = endNode;
                edge["density"] = density;
                edge["roadType"] = segs[i].Info.name ?? "Unknown";
                edge["lanes"] = segs[i].Info.m_lanes != null ? segs[i].Info.m_lanes.Length : 0;
                edge["length"] = (float)segs[i].m_averageLength;
                edge["oneWay"] = !segs[i].Info.m_hasBackwardVehicleLanes ||
                                 !segs[i].Info.m_hasForwardVehicleLanes;
                edges.Add(edge);

                nodeIds.Add(startNode);
                nodeIds.Add(endNode);
            }

            var graphNodes = new List<Dictionary<string, object>>();
            foreach (int nid in nodeIds)
            {
                if (nid <= 0 || nid >= nodes.Length) continue;
                if (nodes[nid].m_flags == NetNode.Flags.None) continue;

                var n = new Dictionary<string, object>();
                n["id"] = nid;
                n["x"] = (float)nodes[nid].m_position.x;
                n["y"] = (float)nodes[nid].m_position.y;
                n["z"] = (float)nodes[nid].m_position.z;

                // Count how many road segments connect to this node
                int connections = 0;
                for (int s = 0; s < 8; s++)
                {
                    ushort segId = nodes[nid].GetSegment(s);
                    if (segId != 0) connections++;
                }
                n["connections"] = connections;
                n["isIntersection"] = connections > 2;

                graphNodes.Add(n);
            }

            result["nodes"] = graphNodes;
            result["edges"] = edges;
            result["nodeCount"] = graphNodes.Count;
            result["edgeCount"] = edges.Count;
            result["minDensity"] = minDensity;
            return result;
        }

        // --- Change Detection ---
        private static Dictionary<string, object> _lastSnapshot;

        public static Dictionary<string, object> GetChanges()
        {
            var current = TakeSnapshot();
            var result = new Dictionary<string, object>();

            if (_lastSnapshot == null)
            {
                result["firstPoll"] = true;
                result["message"] = "Baseline captured. Call again to see changes.";
                result["snapshot"] = current;
                _lastSnapshot = current;
                return result;
            }

            result["firstPoll"] = false;
            var changes = new List<Dictionary<string, object>>();

            foreach (var key in current.Keys)
            {
                if (!_lastSnapshot.ContainsKey(key)) continue;

                if (current[key] is int && _lastSnapshot[key] is int)
                {
                    int curr = (int)current[key];
                    int prev = (int)_lastSnapshot[key];
                    int delta = curr - prev;
                    if (delta != 0)
                    {
                        var c = new Dictionary<string, object>();
                        c["metric"] = key;
                        c["previous"] = prev;
                        c["current"] = curr;
                        c["delta"] = delta;
                        c["direction"] = delta > 0 ? "up" : "down";
                        changes.Add(c);
                    }
                }
                else if (current[key] is long && _lastSnapshot[key] is long)
                {
                    long curr = (long)current[key];
                    long prev = (long)_lastSnapshot[key];
                    long delta = curr - prev;
                    if (delta != 0)
                    {
                        var c = new Dictionary<string, object>();
                        c["metric"] = key;
                        c["previous"] = prev;
                        c["current"] = curr;
                        c["delta"] = delta;
                        c["direction"] = delta > 0 ? "up" : "down";
                        changes.Add(c);
                    }
                }
            }

            result["changes"] = changes;
            result["snapshot"] = current;
            _lastSnapshot = current;
            return result;
        }

        private static Dictionary<string, object> TakeSnapshot()
        {
            var s = new Dictionary<string, object>();
            var dm = Singleton<DistrictManager>.instance;
            District city = dm.m_districts.m_buffer[0];

            s["population"] = (int)city.m_populationData.m_finalCount;
            s["happiness"] = (int)city.m_finalHappiness;
            s["crimeRate"] = (int)city.m_finalCrimeRate;

            var econ = Singleton<EconomyManager>.instance;
            s["money"] = econ.LastCashAmount;
            s["weeklyProfit"] = econ.LastCashDelta;

            var zm = Singleton<ZoneManager>.instance;
            s["demandResidential"] = zm.m_residentialDemand;
            s["demandCommercial"] = zm.m_commercialDemand;
            s["demandWorkplace"] = zm.m_workplaceDemand;

            // Building counts
            var bm = Singleton<BuildingManager>.instance;
            Building[] blds = bm.m_buildings.m_buffer;
            int abandoned = 0, burned = 0, problems = 0;
            for (int i = 0; i < blds.Length; i++)
            {
                if (blds[i].m_flags == Building.Flags.None) continue;
                if ((blds[i].m_flags & Building.Flags.Abandoned) != 0) abandoned++;
                if ((blds[i].m_flags & Building.Flags.BurnedDown) != 0) burned++;
                if (blds[i].m_problems.m_Problems1 != Notification.Problem1.None) problems++;
            }
            s["abandoned"] = abandoned;
            s["burned"] = burned;
            s["buildingsWithProblems"] = problems;

            // Traffic
            var nm = Singleton<NetManager>.instance;
            NetSegment[] segs = nm.m_segments.m_buffer;
            long totalDensity = 0;
            int segCount = 0;
            for (int i = 0; i < segs.Length; i++)
            {
                if (segs[i].m_flags == NetSegment.Flags.None) continue;
                if (segs[i].Info == null || segs[i].Info.m_class == null) continue;
                if (segs[i].Info.m_class.m_service != ItemClass.Service.Road) continue;
                totalDensity += (long)segs[i].m_trafficDensity;
                segCount++;
            }
            s["avgTrafficDensity"] = segCount > 0 ? (int)(totalDensity / segCount) : 0;

            return s;
        }
    }
}
