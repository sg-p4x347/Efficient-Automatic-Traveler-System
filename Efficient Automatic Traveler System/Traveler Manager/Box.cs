﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data.Odbc;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    public enum FoldType
    {
        FPF,
        TD
    }
    public class Box : Traveler
    {
        #region Public Methods
        public static bool IsBox(string itemCode)
        {
            return itemCode.Length >= 2 && itemCode.Substring(0, 2) == "90";
        }
        public Box() : base()
        {
            
            Station = StationClass.GetStation("Box");
            m_boxSize = "";
        }
        // create Box by parsing json string
        public Box(string json,Version version) : base(json,version)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(json).ParseJSON();
                if (obj.ContainsKey("boxSize")) BoxSize = obj["boxSize"];
            } catch (Exception ex)
            {
                Server.LogException(ex);
            }
        }
        // create a Box for a traveler
        public Box(Traveler traveler) : base()
        {
            NewID();
            Station = StationClass.GetStation("Box");
            //m_quantity = traveler.Quantity;
            m_quantity = 1;

            traveler.AddChild(this);
        }
        public override string ToString()
        {
            string inherited = base.ToString();
            Dictionary<string, string> obj = new StringStream(inherited).ParseJSON(false);
            obj.Add("boxSize", BoxSize.Quotate());
            return obj.Stringify();
        }
        public override bool CombinesWith(object[] args)
        {
            return false;
        }
        public override string ExportHuman()
        {
            string inherited = base.ExportHuman();
            Dictionary<string, string> obj = new StringStream(inherited).ParseJSON(false);
            obj.Add("Box size", m_boxSize.Quotate());
            return obj.Stringify();
        }
        // returns a JSON formatted string to be sent to a client
        public override string ExportTableRows(StationClass station)
        {
            try
            {
                Dictionary<string, string> obj = new StringStream(base.ExportTableRows(station)).ParseJSON(false);
                List<string> members = new StringStream(obj["members"]).ParseJSONarray(false);
                members.Add(new NameValueQty<string, string>("Parent Traveler", ParentTravelers[0].ID.ToString("D6"), "").ToString());
                members.Add(new NameValueQty<string, string>("ItemCode", ItemCode, "").ToString());
                members.Add(new NameValueQty<string, string>("Box Size", BoxSize, "").ToString());
                obj["members"] = members.Stringify(false);
                return obj.Stringify();
            } catch (Exception ex)
            {
                Server.LogException(ex);
                return "";
            }
        }
        //public override void AdvanceItem(ushort ID,ITravelerManager travelerManager = null)
        //{
        //    FindItem(ID).Station = GetNextStation(ID);
        //}
        // labels
        public override string GetLabelFields(ushort itemID, LabelType type)
        {
            TravelerItem item = FindItem(itemID);
            string json = "\"Barcode\":" + '"' + ID.ToString("D6") + '-' + itemID.ToString("D4") + '"'; // 11 digits [000000]-[0000]
            switch (type)
            {
                case LabelType.Tracking:
                    json += ",\"ID\":\"" + PrintID() + "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + "" + "\"";
                    break;
                case LabelType.Scrap:
                    json += ",\"ID\":\"" + ID.ToString("D6") + '-' + itemID + "\"";
                    json += ",\"Desc1\":\"" + BoxSize + "\"";
                    json += ",\"Desc2\":\"" + "!! " + PrintID() + " !!" + "\"";
                    break;
                case LabelType.Pack:
                    json += ",\"Order#\":\"" + (FindItem(itemID).Order != null ? "Order: " + FindItem(itemID).Order.SalesOrderNo : "To inventory") + "\"";
                    break;
            }
            return json;
        }
        // returns the next station for this box
        public override StationClass GetNextStation(UInt16 itemID)
        {
            StationClass station = Items.Find(x => x.ID == itemID).Station;
            if (station == StationClass.GetStation("Start"))
            {
                return StationClass.GetStation("Start");
            }
            else if (station == StationClass.GetStation("Box"))
            {
                return StationClass.GetStation("Finished");

            }
            else if (station == StationClass.GetStation("Finished"))
            {
                return StationClass.GetStation("Finished");
            }
            else
            {
                return station;
            }
        }
        public override double GetCurrentLabor(StationClass station)
        {
            return (m_boxLabor != null ? m_boxLabor.QuantityPerBill : 0.0);
        }

        public override double GetTotalLabor(StationClass station)
        {
            if (m_boxLabor != null)
            {
                return GetRate(m_boxLabor, station, true);
            }
            return 0.0;
        }
        public override Task<string> ImportInfo(ITravelerManager travelerManager, IOrderManager orderManager, OdbcConnection MAS)
        {
            return base.ImportInfo(travelerManager,orderManager,MAS);
        }
        #endregion
        //--------------------------------------------------------
        #region Private Methods
        public override Dictionary<string,string> ExportProperties(StationClass station)
        {
            return base.ExportProperties(station);
        }
        protected virtual void ImportBoxSize(string csvTable, string itemCode)
        {

        }
        



        public override Dictionary<string, Node> ExportViewProperties()
        {
            Dictionary<string,Node> list = base.ExportViewProperties();
            list.Add("Box Size", new TextNode(BoxSize));
            return list;
        }
        #endregion
        //--------------------------------------------------------
        #region Properties

        // Box
        private string m_boxSize;
        private FoldType m_foldType;
        private string m_contents;
        // labor
        private Item m_boxLabor;
        #endregion
        //--------------------------------------------------------
        #region Interface
        public virtual string BoxSize
        {
            get
            {
                return m_boxSize;
            }

            set
            {
                m_boxSize = value;
            }
        }
        public string Contents
        {
            get
            {
                return m_contents;
            }

            set
            {
                m_contents = value;
            }
        }

        public Item BoxLabor
        {
            get
            {
                return m_boxLabor;
            }

            set
            {
                m_boxLabor = value;
            }
        }

        public FoldType FoldType
        {
            get
            {
                return m_foldType;
            }

            set
            {
                m_foldType = value;
            }
        }
        #endregion
        //--------------------------------------------------------
    }
}
