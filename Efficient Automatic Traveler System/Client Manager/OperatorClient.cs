﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

using System.Timers;

namespace Efficient_Automatic_Traveler_System
{
    class OperatorClient : Client, IOperator, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, ITravelerManager travelerManager) : base(client)
        {
            AccessLevel = AccessLevel.Operator;
            m_travelerManager = travelerManager;
            SendMessage((new ClientMessage("InitStations", StationClass.GetStations().Stringify())).ToString());
        }

        public string SetStation(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                m_station = StationClass.GetStation(obj["station"]);
                HandleTravelersChanged(m_travelerManager.GetTravelers);
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
            }
            return "";
        }
        public void HandleTravelersChanged(List<Traveler> travelers)
        {
            bool mirror = true; // travelers.Count == m_travelerManager.GetTravelers.Count;
            travelers = m_travelerManager.GetTravelers;
            Dictionary<string, string> message = new Dictionary<string, string>();
            List<string> travelerStrings = new List<string>();

            foreach (Traveler traveler in m_travelerManager.GetTravelers.Where(x => x.State == ItemState.InProcess && (x.QuantityPendingAt(m_station) > 0 || x.QuantityAt(m_station) > 0)).ToList())
            {
                travelerStrings.Add(ExportTraveler(traveler));
            }
            message.Add("travelers", travelerStrings.Stringify(false));
            message.Add("mirror", mirror.ToString().ToLower());
            SendMessage(new ClientMessage("HandleTravelersChanged", message.Stringify()).ToString());
        }
        private string ExportTraveler(Traveler traveler)
        {
            string travelerJSON = traveler.ToString();
            Dictionary<string, string> queueItem = new Dictionary<string, string>();
            queueItem.Add("queueItem", traveler.ExportStationSummary(m_station));
            travelerJSON = travelerJSON.MergeJSON(queueItem.Stringify()); // merge station properties
            travelerJSON = travelerJSON.MergeJSON(traveler.ExportTableRows(m_station));
            travelerJSON = travelerJSON.MergeJSON(traveler.ExportProperties(m_station).Stringify());
            return travelerJSON;
        }
        public ClientMessage ChecklistSubmit(string json)
        {
            m_partStart = DateTime.Now;
            return new ClientMessage();
        }
        //------------------------------
        // Private members
        //------------------------------
        private void DisplayChecklist()
        {
            try
            {
                Dictionary<string, string> stationType = new StringStream(new StringStream(ConfigManager.Get("stationTypes")).ParseJSON()[m_station.Type]).ParseJSON();
                if (stationType.ContainsKey("checklist"))
                {
                    string list = stationType["checklist"];
                    ClientMessage message = new ClientMessage("DisplayChecklist", list);
                    SendMessage(message.ToString());
                }
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        //------------------------------
        // Properties
        //------------------------------
        protected ITravelerManager m_travelerManager;
        protected StationClass m_station;
        protected Traveler m_current;
        protected DateTime m_partStart;
        internal StationClass Station
        {
            get
            {
                return m_station;
            }

            set
            {
                m_station = value;
            }
        }
        //----------
        // Events
        //----------
        public event TravelersChangedSubscriber TravelersChanged;
        // JS client interface (these are the properties visible to the js interface calling system)
        public List<Traveler> GetTravelers
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ClientMessage Login(string json)
        {
            try
            {
                Dictionary<string, string> obj = (new StringStream(json)).ParseJSON();
                ClientMessage message = base.Login(json);
                if (message.Method == "LoginSuccess" && obj.ContainsKey("station"))
                {
                    SetStation(json);
                    Dictionary<string, string> paramObj = new Dictionary<string, string>()
                    {
                        {"user",message.Parameters },
                        {"station",obj["station"].Quotate()}
                    };
                    return new ClientMessage("LoginSuccess", paramObj.Stringify());
                }
                else
                {
                    return message;
                }
            }
            catch (Exception ex)
            {
                Server.WriteLine(ex.Message + "stack trace: " + ex.StackTrace);
                return new ClientMessage("LoginPopup", ("System error! oops...").Quotate());
            }
        }

        public ClientMessage AddTravelerEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                //traveler.GetCurrentLabor(m_station) - Convert.ToDouble(obj["time"])
                DateTime now = DateTime.Now;
                TimeSpan duration = now.Subtract(m_partStart);
                m_partStart = now;
                ProcessEvent evt = new ProcessEvent(m_user, m_station, duration.TotalMinutes, (ProcessType)Enum.Parse(typeof(ProcessType), obj["eventType"]));
                
                TravelerItem item = (obj["itemID"] != "undefined" ? traveler.FindItem(Convert.ToUInt16(obj["itemID"])) : null);
                return m_travelerManager.AddTravelerEvent(evt,traveler,item);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage ScrapEvent(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                ScrapEvent evt = new ScrapEvent(m_user, m_station, traveler.GetCurrentLabor(m_station) - Convert.ToDouble(obj["time"]), Convert.ToBoolean(obj["startedWork"].ToLower()),obj["source"],obj["reason"]);

                TravelerItem item = (obj["itemID"] != "undefined" ? traveler.FindItem(Convert.ToUInt16(obj["itemID"])) : null);
                ClientMessage message =  m_travelerManager.AddTravelerEvent(evt, traveler, item);
                int userScrapQty = m_travelerManager.GetTravelers.Sum(t => t.Items.Count(i => i.History.OfType<ScrapEvent>().ToList().Exists(e => e.User.UID == m_user.UID && e.Date >= DateTime.Today)));
                if (userScrapQty >= 5)
                {
                    if (userScrapQty >= 10)
                    {
                        if (userScrapQty >= 15)
                        {
                            message.Parameters = (message.Parameters.DeQuote() +  "<br>You have scrapped " + userScrapQty + " items today, STOP doing that").Quotate();
                        } else
                        {
                            message.Parameters = (message.Parameters.DeQuote() + "<br>Please stop scrapping so many items!").Quotate();
                        }
                    } else
                    {
                        message.Parameters = (message.Parameters.DeQuote() + "<br>You should think about not scrapping so much").Quotate();
                    }
                }
                
                return message;
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage DisplayScrapReport(string json)
        {
            try
            {
                string scrapReport = ConfigManager.Get("scrapReport");
                return new ClientMessage("DisplayScrapReport", scrapReport);
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        public ClientMessage SubmitTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                AddHistory(new Dictionary<string, object>() { { "traveler", traveler }, { "items", traveler.CompletedItems(m_station) } });
                return m_travelerManager.SubmitTraveler(
                    traveler,
                    m_station
                );
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info","Error occured");
            }
        }

        public ClientMessage OpenDrawing(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("Redirect", ("../drawings/" + (traveler as IPart).Part.DrawingNo.Split('-')[0] + ".pdf").Quotate());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error occured");
            }
        }
        //public ClientMessage LoadTraveler(string json)
        //{
        //    Traveler freshTraveler = m_travelerManager.FindTraveler(Convert.ToInt32(new StringStream(json).ParseJSON()["travelerID"]));
        //    if (freshTraveler != null && ( m_current == null || freshTraveler.ID != m_current.ID))
        //    {
        //        DisplayChecklist();
        //        // auto-submit completed items
        //        m_travelerManager.SubmitTraveler(m_current, m_station);
        //    }
        //    m_current = freshTraveler;
            
        //    return new ClientMessage();
        //}
        public ClientMessage LoadTravelerJSON(string json)
        {
            return m_travelerManager.LoadTravelerJSON(json);
        }
        public ClientMessage LoadTraveler(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (m_current == null || (traveler != null && traveler.ID != m_current.ID))
                {
                    if (traveler.CurrentStations().Exists(t => t == m_station))
                    {
                        DisplayChecklist();
                        m_travelerManager.SubmitTraveler(m_current, m_station);
                        m_current = traveler;
                        return new ClientMessage("LoadTraveler", ExportTraveler(traveler));
                    } else
                    {
                        return new ClientMessage("Info", "Traveler " + traveler.ID.ToString("D6") + " is not at this station  :(");
                    }
                }
                return new ClientMessage();
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading traveler");
            }
        }
        public ClientMessage LoadItem(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                TravelerItem item = traveler.FindItem(Convert.ToUInt16(obj["itemID"]));
                LoadTraveler(json);
                Dictionary<string, string> returnParams = new Dictionary<string, string>()
                {
                    {"traveler", ExportTraveler(traveler)},
                    {"item",item.ToString() },
                    {"sequenceID",traveler.PrintSequenceID(item).Quotate() }
                };
                if (m_station == StationClass.GetStation("Table-Pack"))
                {
                    SendMessage(new ClientMessage("Info", traveler.PrintLabel(item.ID, LabelType.Table)).ToString());
                }
                return new ClientMessage("LoadItem", returnParams.Stringify());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error loading item");
            }
        }
        public ClientMessage SearchSubmitted(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                if (traveler != null)
                {
                    ushort itemID;
                    if (ushort.TryParse(obj["itemID"], out itemID))
                    {
                        TravelerItem item = traveler.FindItem(itemID);
                        if (item != null)
                        {
                            if (item.Station == m_station)
                            {
                                SendMessage(LoadItem(json).ToString());
                                // if this is Table pack station, print Table label on search submission 
                                // (they scanned the barcode)
                                //if (m_station == StationClass.GetStation("Table-Pack"))
                                //{
                                //    return new ClientMessage("Info", traveler.PrintLabel(Convert.ToUInt16(obj["itemID"]), LabelType.Table));
                                //}
                            }
                            else
                            {
                                return new ClientMessage("Info", traveler.PrintID(item) + " is not at your station;<br/>It is at " + item.Station.Name);
                            }
                        }
                        else
                        {
                            SendMessage(LoadTraveler(json).ToString());
                            return new ClientMessage("Info", traveler.ID.ToString() + "-" + obj["itemID"] + " does not exist");
                        }
                    } else
                    {
                        SendMessage(LoadTraveler(json).ToString());
                        return new ClientMessage();
                    }
                } else
                {
                    return new ClientMessage("Info", obj["travelerID"] + " does not exist");
                }
                
                return new ClientMessage();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error processing search event");
            }
        }
        public ClientMessage LabelPopup(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                string returnParam = new Dictionary<string, string>()
                {
                    {"traveler",traveler.ToString() },
                    {"labelTypes",ExtensionMethods.GetNames<LabelType>().Stringify() }
                }.Stringify();
                return new ClientMessage("PrintLabelPopup", returnParam);
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening print dialog");
            }
        }
        public ClientMessage PrintLabel(string json)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                int qty = Convert.ToInt32(obj["quantity"]);
                Traveler traveler = m_travelerManager.FindTraveler(Convert.ToInt32(obj["travelerID"]));
                return new ClientMessage("Info", traveler.PrintLabel(Convert.ToUInt16(obj["itemID"]), (LabelType)Enum.Parse(typeof(LabelType), obj["labelType"]), qty > 0 ? qty : 1, true));

            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Could not print label(s) due to a pesky error :(");
            }
        }
        public ClientMessage OptionsMenu(string json)
        {
            try
            {
               

                Column options = new Column()
                {
                    new Button("Undo", "Undo",styleClasses: new Style("undoImg"))
                };
                // options that require a traveler
                if (m_current != null)
                {
                    // the parameter that returns with all the control events
                    string returnParam = new Dictionary<string, string>()
                    {
                        {"travelerID", m_current.ID.ToString() }
                    }.Stringify();
                    options.Add(new Button("Print Labels", "LabelPopup", returnParam));

                }
                ControlPanel panel = new ControlPanel("Options", options);
                return new ClientMessage("ControlPanel", panel.ToString());
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Error opening options menu");
            }
        }
        public ClientMessage Undo(string json)
        {
            try
            {
                UserAction lastAction = m_history.Last();
                m_history.Remove(lastAction);
                if (lastAction.Method == "SubmitTraveler")
                {
                    Traveler traveler = (Traveler)lastAction.Parameters["traveler"];
                    List<TravelerItem> items = (List<TravelerItem>)lastAction.Parameters["items"];
                    foreach (TravelerItem item in items)
                    {
                        item.Station = m_station;
                        if (traveler.GetNextStation(item.ID) == StationClass.GetStation("Finished"))
                        {
                            item.History.Remove(item.History.Last());
                        }
                    }
                    if (traveler is Table && m_station.CreatesThis(traveler))
                    {
                        if (traveler.ChildTravelers.Count > 0)
                        {
                            m_travelerManager.RemoveTraveler(traveler.ChildTravelers.Last().ID);
                        }
                    }
                    m_travelerManager.OnTravelersChanged(new List<Traveler>() { traveler });
                }

                return new ClientMessage("CloseAll");
            }
            catch (Exception ex)
            {
                Server.LogException(ex);
                return new ClientMessage("Info", "Can't undo..");
            }
        }
        public override ClientMessage Logout(string json)
        {
            // auto-submit completed items
            m_travelerManager.SubmitTraveler(m_current, m_station);
            return base.Logout(json);
        }
    }
}
