using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace Efficient_Automatic_Traveler_System
{
    class OperatorClient : Client, ITravelers
    {
        //------------------------------
        // Public members
        //------------------------------
        public OperatorClient (TcpClient client, ITravelerManager travelerCore) : base(client)
        {
            m_travelerCore = travelerCore;
            m_travelers = m_travelerCore.GetTravelers;
            string stationList = "";
            foreach(string station in Traveler.Stations.Keys)
            {
                stationList += (stationList.Length != 0 ? "," : "") + '"' + station + '"';
            }
            SendMessage(@"{""stationList"":[" + stationList + "]}");
            HandleTravelersChanged();
        }
        public virtual async void ListenAsync()
        {
            try
            {
                string message = await RecieveMessageAsync();
                if (!Connected) return;
                if (message.Length == 0) throw new Exception("bad message");
                if (message[0] == '"') message = message.Remove(0, 1);
                StringStream ss = new StringStream(message);
                Dictionary<string, string> obj = ss.ParseJSON();
                if (obj.ContainsKey("station"))
                {
                    m_station = Traveler.GetStation(obj["station"]);
                    HandleTravelersChanged();
                }
                else if (obj.ContainsKey("completed") && obj.ContainsKey("destination") && obj.ContainsKey("time") && obj.ContainsKey("qtyMade") && obj.ContainsKey("qtyScrapped"))
                {
                    //----------------------
                    // Traveler Completed
                    //----------------------
                    string returnMessage = "";
                    Traveler traveler = m_travelers.Find(x => x.ID == Convert.ToInt32(obj["completed"]));
                    if (traveler != null)
                    {
                        traveler.NextStation = Traveler.GetStation(obj["destination"]);

                        int qtyMade = Convert.ToInt32(obj["qtyMade"]);
                        int qtyScrapped = Convert.ToInt32(obj["qtyScrapped"]);
                        int qtyPending = traveler.Quantity - (qtyMade + qtyScrapped);

                        // SCRAP
                        if (qtyScrapped > 0)
                        {
                            if (qtyScrapped == traveler.Quantity)
                            {
                                // log this event
                                traveler.History.Add(new Event(TravelerEvent.Scrapped, qtyScrapped, traveler.Station, Convert.ToDouble(obj["time"])));
                                traveler.Start(); // the whole thing was scrapped
                            }
                            else
                            {
                                m_travelerCore.CreateScrapChild(traveler, qtyScrapped);
                                
                                //Traveler scrapped = (Traveler)traveler.Clone();
                                //// relational dependencies to original traveler
                                ////scrapped.Parents.Add(traveler.ID);
                                ////traveler.Children.Add(scrapped.ID);
                                ////---------------------------------------------
                                //scrapped.Quantity = qtyScrapped;
                                //traveler.Quantity -= qtyScrapped;
                                //scrapped.Start();
                                //// log this event
                                //scrapped.History.Add(new Event(TravelerEvent.Scrapped, scrapped.Quantity, traveler.Station, Convert.ToDouble(obj["time"])));
                                //m_travelers.Add(scrapped);
                            }
                        }
                        if (qtyMade > 0)
                        {
                            if (qtyMade == traveler.Quantity)
                            {
                                // log this event
                                traveler.History.Add(new Event(TravelerEvent.Completed, traveler.Quantity, traveler.Station, Convert.ToDouble(obj["time"])));
                                if (traveler.LastStation == Traveler.GetStation("Start"))
                                {
                                    traveler.PrintLabel();
                                    returnMessage = "Printed traveler label: " + traveler.ID + "<br>Please place this label with the pallet that you just submitted";
                                }
                                m_travelerCore.AdvanceTraveler(traveler);
                            } else
                            {
                                Traveler made = m_travelerCore.CreateCompletedChild(traveler, qtyMade, Convert.ToDouble(obj["time"]));
                                //Traveler made = (Traveler)traveler.Clone();
                                made.PrintLabel();
                                returnMessage = "Printed traveler label: " + made.ID + "<br>Please place this label with the pallet that you just submitted";
                                //// relational dependencies to original traveler
                                ////made.Parents.Add(traveler.ID);
                                ////traveler.Children.Add(made.ID);
                                ////---------------------------------------------
                                //made.Quantity = qtyMade;
                                //traveler.Quantity -= qtyMade;
                                //made.Station = Traveler.GetStation(obj["destination"]);
                                //made.Advance();
                                //// log this event
                                //made.History.Add(new Event(TravelerEvent.Completed, made.Quantity, traveler.Station, Convert.ToDouble(obj["time"])));
                                //m_travelers.Add(made);
                            }
                        } 
                    }
                    if (returnMessage != "")
                    {
                        SendMessage("{\"confirmation\":\"" + returnMessage + "\"}");
                    }
                    TravelersChanged();
                } else if (obj.ContainsKey("print") && obj.ContainsKey("qty"))
                {
                    // Print label
                    m_travelers.Find(x => x.ID == Convert.ToInt32(obj["print"])).PrintLabel(Convert.ToInt32(obj["qty"]));
                }
            } catch (Exception ex)
            {
                // something went wrong, it is best to just listen for a new message
            }
            ListenAsync();
        }
        public void HandleTravelersChanged()
        {
            List<Traveler> stationSpecific = m_travelers.Where(x => x.Station == m_station).ToList();
            string message = @"{""travelers"":[";
            string travelerJSON = "";
            foreach (Traveler traveler in stationSpecific)
            {
                if (traveler.GetType().Name == "Table")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Table)traveler).Export(this.GetType().Name);
                }
                else if (traveler.GetType().Name == "Chair")
                {
                    travelerJSON += (travelerJSON.Length != 0 ? "," : "") + ((Chair)traveler).Export(this.GetType().Name);
                }
            }
            message += travelerJSON + "]}";
            SendMessage(message);
        }

        //------------------------------
        // Private members
        //------------------------------
        //------------------------------
        // Properties
        //------------------------------
        protected ITravelerManager m_travelerCore;
        protected List<Traveler> m_travelers;
        protected int m_station;
        
        internal int Station
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
    }
}
