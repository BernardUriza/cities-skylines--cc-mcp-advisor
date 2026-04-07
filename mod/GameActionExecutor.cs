using System;
using ColossalFramework;
using UnityEngine;

namespace ClaudeAdvisor
{
    public static class GameActionExecutor
    {
        public static void DemolishBuilding(ushort buildingId)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var bm = Singleton<BuildingManager>.instance;
                    if (buildingId >= bm.m_buildings.m_buffer.Length) return;
                    if (bm.m_buildings.m_buffer[buildingId].m_flags == Building.Flags.None) return;
                    bm.ReleaseBuilding(buildingId);
                    Debug.Log("[ClaudeAdvisor] Demolished building " + buildingId);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Demolish failed: " + ex.Message);
                }
            });
        }

        public static int DemolishAllAbandoned()
        {
            var bm = Singleton<BuildingManager>.instance;
            Building[] blds = bm.m_buildings.m_buffer;
            int count = 0;

            for (int i = 0; i < blds.Length; i++)
            {
                if (blds[i].m_flags == Building.Flags.None) continue;
                if ((blds[i].m_flags & Building.Flags.Abandoned) != 0)
                {
                    ushort id = (ushort)i;
                    count++;
                    Singleton<SimulationManager>.instance.AddAction(() =>
                    {
                        try
                        {
                            var bmInner = Singleton<BuildingManager>.instance;
                            if (bmInner.m_buildings.m_buffer[id].m_flags != Building.Flags.None)
                            {
                                bmInner.ReleaseBuilding(id);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("[ClaudeAdvisor] Demolish abandoned failed: " + ex.Message);
                        }
                    });
                }
            }
            Debug.Log("[ClaudeAdvisor] Queued demolition of " + count + " abandoned buildings");
            return count;
        }

        public static void InjectMoney(int amount)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    // amount is in game units (1 = $0.01, so multiply by 100)
                    econ.AddResource(EconomyManager.Resource.LoanAmount, amount * 100,
                        ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
                    Debug.Log("[ClaudeAdvisor] Injected $" + amount);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Money inject failed: " + ex.Message);
                }
            });
        }

        public static void SetTaxRate(string serviceName, int rate)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    ItemClass.Service service = ParseService(serviceName);
                    // Set tax for all sub-services and levels
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level1, rate);
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level2, rate);
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level3, rate);
                    Debug.Log("[ClaudeAdvisor] Set " + serviceName + " tax to " + rate + "%");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Tax rate failed: " + ex.Message);
                }
            });
        }

        public static void SetBudget(string serviceName, int budget)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    ItemClass.Service service = ParseService(serviceName);
                    econ.SetBudget(service, ItemClass.SubService.None, budget, false);
                    Debug.Log("[ClaudeAdvisor] Set " + serviceName + " budget to " + budget + "%");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Budget failed: " + ex.Message);
                }
            });
        }

        public static void SetSpeed(int speed)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Singleton<SimulationManager>.instance.SelectedSimulationSpeed = speed;
                    Debug.Log("[ClaudeAdvisor] Set speed to " + speed);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Speed failed: " + ex.Message);
                }
            });
        }

        public static void SetPaused(bool paused)
        {
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Singleton<SimulationManager>.instance.SimulationPaused = paused;
                    Debug.Log("[ClaudeAdvisor] " + (paused ? "Paused" : "Unpaused") + " game");
                }
                catch (Exception ex)
                {
                    Debug.LogError("[ClaudeAdvisor] Pause failed: " + ex.Message);
                }
            });
        }

        private static ItemClass.Service ParseService(string name)
        {
            if (string.IsNullOrEmpty(name)) return ItemClass.Service.Residential;
            switch (name.ToLower())
            {
                case "residential": return ItemClass.Service.Residential;
                case "commercial": return ItemClass.Service.Commercial;
                case "industrial": return ItemClass.Service.Industrial;
                case "office": return ItemClass.Service.Office;
                case "road": return ItemClass.Service.Road;
                case "electricity": return ItemClass.Service.Electricity;
                case "water": return ItemClass.Service.Water;
                case "garbage": return ItemClass.Service.Garbage;
                case "healthcare": return ItemClass.Service.HealthCare;
                case "firestation": case "fire": return ItemClass.Service.FireDepartment;
                case "police": return ItemClass.Service.PoliceDepartment;
                case "education": return ItemClass.Service.Education;
                case "monument": return ItemClass.Service.Monument;
                case "beautification": case "parks": return ItemClass.Service.Beautification;
                default: return ItemClass.Service.Residential;
            }
        }
    }
}
