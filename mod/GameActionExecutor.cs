using System;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace ClaudeAdvisor
{
    public static class GameActionExecutor
    {
        public static void DemolishBuilding(ushort buildingId)
        {
            Logger.ActionQueued("DemolishBuilding", "buildingId=" + buildingId);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var bm = Singleton<BuildingManager>.instance;
                    if (buildingId >= bm.m_buildings.m_buffer.Length)
                    {
                        Logger.Warn("Action", "DemolishBuilding: invalid ID", "buildingId=" + buildingId + " bufferLength=" + bm.m_buildings.m_buffer.Length);
                        return;
                    }
                    if (bm.m_buildings.m_buffer[buildingId].m_flags == Building.Flags.None)
                    {
                        Logger.Warn("Action", "DemolishBuilding: building has no flags (already demolished?)", "buildingId=" + buildingId);
                        return;
                    }
                    bm.ReleaseBuilding(buildingId);
                    Logger.ActionExecuted("DemolishBuilding", "buildingId=" + buildingId);
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("DemolishBuilding", ex);
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
                            Logger.ActionFailed("DemolishAbandoned[" + id + "]", ex);
                        }
                    });
                }
            }
            Logger.ActionQueued("DemolishAllAbandoned", "count=" + count);
            return count;
        }

        public static void InjectMoney(int amount)
        {
            Logger.ActionQueued("InjectMoney", "amount=$" + amount);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    econ.AddResource(EconomyManager.Resource.LoanAmount, amount * 100,
                        ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
                    Logger.ActionExecuted("InjectMoney", "amount=$" + amount);
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("InjectMoney", ex);
                }
            });
        }

        public static void SetTaxRate(string serviceName, int rate)
        {
            Logger.ActionQueued("SetTaxRate", "service=" + serviceName + " rate=" + rate + "%");
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    ItemClass.Service service = ParseService(serviceName);
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level1, rate);
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level2, rate);
                    econ.SetTaxRate(service, ItemClass.SubService.None, ItemClass.Level.Level3, rate);
                    Logger.ActionExecuted("SetTaxRate", "service=" + serviceName + " rate=" + rate + "%");
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("SetTaxRate", ex);
                }
            });
        }

        public static void SetBudget(string serviceName, int budget)
        {
            Logger.ActionQueued("SetBudget", "service=" + serviceName + " budget=" + budget + "%");
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var econ = Singleton<EconomyManager>.instance;
                    ItemClass.Service service = ParseService(serviceName);
                    econ.SetBudget(service, ItemClass.SubService.None, budget, false);
                    Logger.ActionExecuted("SetBudget", "service=" + serviceName + " budget=" + budget + "%");
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("SetBudget", ex);
                }
            });
        }

        public static void SetSpeed(int speed)
        {
            Logger.ActionQueued("SetSpeed", "speed=" + speed);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Singleton<SimulationManager>.instance.SelectedSimulationSpeed = speed;
                    Logger.ActionExecuted("SetSpeed", "speed=" + speed);
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("SetSpeed", ex);
                }
            });
        }

        public static void SetPaused(bool paused)
        {
            Logger.ActionQueued("SetPaused", "paused=" + paused);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    Singleton<SimulationManager>.instance.SimulationPaused = paused;
                    Logger.ActionExecuted("SetPaused", "paused=" + paused);
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("SetPaused", ex);
                }
            });
        }

        public static void SendChirperMessage(string message)
        {
            Logger.ActionQueued("SendChirp", "length=" + message.Length);
            Singleton<SimulationManager>.instance.AddAction(() =>
            {
                try
                {
                    var mm = Singleton<MessageManager>.instance;
                    mm.QueueMessage(new ClaudeChirperMessage(message));
                    Logger.ActionExecuted("SendChirp", "message=" + message);
                }
                catch (Exception ex)
                {
                    Logger.ActionFailed("SendChirp", ex);
                }
            });
        }

        private static ItemClass.Service ParseService(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Logger.Warn("Action", "ParseService: empty name, defaulting to Residential");
                return ItemClass.Service.Residential;
            }
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
                default:
                    Logger.Warn("Action", "ParseService: unknown service name, defaulting to Residential", "name=" + name);
                    return ItemClass.Service.Residential;
            }
        }
    }

    public class ClaudeChirperMessage : MessageBase
    {
        private string m_text;

        public ClaudeChirperMessage(string text)
        {
            m_text = text;
        }

        public override string GetSenderName()
        {
            return "Claude Advisor";
        }

        public override string GetText()
        {
            return m_text;
        }

        public override uint GetSenderID()
        {
            return 0;
        }
    }
}
