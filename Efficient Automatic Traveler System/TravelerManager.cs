// Module: Used to create traveling objects of information that guide the production process : inherits from the Quick Ship Traveler project
// Developer: Gage Coates
// Date started: 1/23/16

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using Excel = Microsoft.Office.Interop.Excel;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace Efficient_Automatic_Traveler_System
{
    class TravelerManager
    {
        enum Mode
        {
            CreateSelected,
            CreateUnselected,
            CreateSpecific,
            CreatePrinted,
            CreateAll,
        }
        public TravelerManager()
        {
            excelApp = new Excel.Application();
            excelApp.DisplayAlerts = false;
            // Open the traveler template
            workbooks = excelApp.Workbooks;

            PopulateCustomers();
            // connect to MAS

            InitializeManagers();
        }
        ~TravelerManager()
        {
            // close the MAS connection on exit
            MAS.Close();
            // close excel

            workbooks.Close();
            if (workbooks != null) Marshal.FinalReleaseComObject(workbooks);
            excelApp.Quit();
            if (excelApp != null) Marshal.FinalReleaseComObject(excelApp);
        }
        // Properties

        private Excel.Application excelApp;
        private Excel.Workbooks workbooks;
        private OdbcConnection MAS = new OdbcConnection();
        private string sortInfo = "";
        // Opens a connection to the MAS database
        private void ConnectToData()
        {
            MAS = new OdbcConnection();
            // initialize the MAS connection
            MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;";
            MAS.ConnectionString = "DSN=SOTAMAS90;Company=MGI;UID=XXX;PWD=XXXXXXX;";
            try
            {
                MAS.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to log in to MAS:" + ex.Message);
            }
        }
        private void InitializeManagers()
        {
            ConnectToData();

            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            var workbook = workbooks.Open(System.IO.Path.Combine(exeDir, "Kanban Blank Color Cross Reference.xlsx"),
                0, false, 5, "", "", false, 2, "",
                true, false, 0, true, false, false);
            //var workbook = workbooks.Open(@"\\Mgfs01\share\common\Quick Ship Traveler\Kanban Blank Color Cross Reference.xlsx",
            //    0, false, 5, "", "", false, 2, "",
            //    true, false, 0, true, false, false);
            var worksheets = workbook.Worksheets;
            var crossRef = (Excel.Worksheet)worksheets.get_Item("Blank Cross Reference");
            var colorRef = (Excel.Worksheet)worksheets.get_Item("Color Families");
            var blankRef = (Excel.Worksheet)worksheets.get_Item("Blank Parent");
            var boxRef = (Excel.Worksheet)worksheets.get_Item("Box Size");

            tableManager = new TableManager(MAS, crossRef, boxRef, blankRef, colorRef);
            chairManager = new ChairManager(MAS);
        }
        // Fill the customer list with customers
        private void PopulateCustomers()
        {
            List<string> customers = new List<string>
        {
            "ABARGAS","ACEEDUC","ADP    ","AEROBLD","AFC    ","AGILE  ","AJAXSCH","ALABOUT","ALCON  ","ALLGLAS","ALTRA  ","ALUMBAU","AMAZOND","AMERICA","AMTAB  ","ANDERSN","ANIMAL ","AQADVOH","AQRSERV","AQUABUY","AQUADEP","AQUAIMP","AQUALND","AQUARAD","AQUARIA","AQUARIU","AQUATIC","ARVEST ","ATDAMER","ATDCAPL","BARNES ","BASSETT","BEAL   ","BECKER ","BEENEES","BEIMDIK","BIMART ","BLAZARC","BLUEFIN","BLUETHB","BMCOFF ","BOTSON ","BRALEY ","BRANCO ","BRNSNGR","C&HDIST","CAROLIN","CASCO  ","CENTRAL","CENTRPT","CFM    ","CHLDCAR","CHUCK  ","CLAIMS ","CLARK  ","CLAWPAW","COFACE ","COFCROC","COLEMAN","COMMERC","COMPACK","CONTFRN","COSTCO ","COSTCOW","COWPUBS","COYOTE ","CRAFTSH","CREATMK","CREWSMA","CROSBY ","CROSS  ","CROWDER","D&WSALE","DAISYBB","DALEY  ","DALMIDW","DAVCO  ","DAVENPO","DAVIDS ","DAVIDSB","DAVIDSO","DAVIS  ","DETWILE","DFWAQUA","DIABLO ","DONLOLL","DUBOSE ","DWYER  ","EASTSID","EBAY   ","EDUCDEP","ELEGAB ","ERICKSO","EVERFUR","EXFACTO","EXOTIC ","FACTSEL","FCOONEY","FELBAPT","FIECANA","FIESTAD","FINTAST","FISHPLA","FISHTAN","FISHWIS","FOREMAN","FOSTER ","FOURSOP","FRANKS ","FRYE   ","FURNNET","GASKET ","GENERAL","GODSCRE","GODSRES","GREATOU","GRIERIN","GRILLST","GUNLOCK","H&H    ","HARRIS ","HDHAPPY","HEMBREE","HEMBREM","HEMCO  ","HERTZFN","HOMEDEP","HOMETOW","HONCOMP","HOOVER ","HUNTE  ","HUNTER ","IFD    ","IFURN  ","INDOFF ","INTEGFN","IPAEDUC","IQSI   ","JACK'S ","JAMESCH","JAMESSH","JARDEN ","JJAMESO","JMJWORK","JSATRAD","K&S    ","KAMWOOD","KAY12  ","KENDAL ","KLOG   ","KSCONDS","KURTZBR","LAKESQ ","LANDMAN","LARSON ","LATTAS ","LAVACA ","LAZBOY ","LEGGETT","LIBERTY","LOISSCH","LONESTR","LOVELAN","LOWES  ","LOZIER ","MARTIN ","MARTLUT","MATEL  ","MAVIATN","MCALIST","MCCAULE","MCCOOL ","MEIJER ","MENARDS","MIDSTAT","MILLERS","MILLERZ","MILLS' ","MISC   ","MISCELL","MODOR  ","MOP    ","MORETHN","MOSER  ","MSSC   ","NATBOND","NATWIDE","NBFLA  ","NEEL   ","NEOSHOB","NEOSHOD","NEOSHR5","NETSHOP","NEWDISP","NEXTGEN","NOAHS  ","NOBIS  ","NOLANS ","OAKRIDG","OF003  ","OF008  ","OF011  ","OF012  ","OF013  ","OF014  ","OF023  ","OF031  ","OF032  ","OF034  ","OF035  ","OF046  ","OF059  ","OF065  ","OF067  ","OF070  ","OF071  ","OF083  ","OF088  ","OF090  ","OF091  ","OF093  ","OF094  ","OF099  ","OF109  ","OF111  ","OF112  ","OF113  ","OF114  ","OF120  ","OF123  ","OF124  ","OF141  ","OF150  ","OF153  ","OF156  ","OF158  ","OF167  ","OF169  ","OF172  ","OF174  ","OF180  ","OF181  ","OF182  ","OF184  ","OF185  ","OF190  ","OF197  ","OF205  ","OF208  ","OF209  ","OF211  ","OF226  ","OF232  ","OF233  ","OF237  ","OF243  ","OF250  ","OF252  ","OF255  ","OF256  ","OF268  ","OF270  ","OF272  ","OF273  ","OF279  ","OF283  ","OF284  ","OF289  ","OF291  ","OF293  ","OF295  ","OF298  ","OF299  ","OF314  ","OF317  ","OF321  ","OF323  ","OF326  ","OF327  ","OF329  ","OF330  ","OF335  ","OF336  ","OF338  ","OF341  ","OF348  ","OF352  ","OF353  ","OF354  ","OF360  ","OF362  ","OF368  ","OF371  ","OF372  ","OF374  ","OF377  ","OF378  ","OF384  ","OF385  ","OF386  ","OF387  ","OF391  ","OF392  ","OF393  ","OF394  ","OF395  ","OF396  ","OF397  ","OF398  ","OF399  ","OF401  ","OF402  ","OF403  ","OF404  ","OF405  ","OF406  ","OF407  ","OF408  ","OF409  ","OF410  ","OF411  ","OF413  ","OF422  ","OFCCON ","OFFDEP ","OFFDEPF","OFFDEPV","OFFDPBS","OFFMAX ","OFFSOUR","OFFSTAR","OFGPART","OFGWARR","OLDTOWN","OLPI   ","ONEWAYF","OSBORNE","OSULLIV","OVATION","OVERSTO","OZRKPLS","PALLETS","PARTS  ","PAYROLL","PEOPLES","PETCOCO","PETS PA","PETSMAR","PETSPLS","PETSUPE","PETZONE","PLAYTIM","PREWETT","QL     ","R&R MAC","R.G. AP","RACCHRC","REDNECK","REYNOLD","RJRAY  ","RMIND  ","ROEBLNG","ROGARDS","ROGERS ","ROSS   ","RTC    ","SAGECC ","SAMPLES","SAMS   ","SARTINF","SCHAEFE","SCHENKE","SCHLAID","SCHLBOX","SCHLPRD","SCHLSIN","SCLHSPR","SENECR7","SHERWIN","SHICK  ","SHORE  ","SIBLEY ","SMARKET","SPORTS ","SSIFURN","SSWORLD","STANDBY","STAOFMO","STAPLBI","STAPLES","STATELI","STEELMN","STRFRKD","STRONG ","SUNBEAM","TALBOT ","TANKSTO","TARPLEY","TEACHED","TEACHLG","TEENCHA","TEST   ","THOMPSN","THORCO ","TIENLE ","TOEWS  ","TOWNSEN","TPCINC ","TRAVIS ","TROPICL","TURNING","TWIN   ","TXSHCHS","UNBEAT ","UNITY  ","UPDATPT","UPS    ","USTOYCO","VASTMKT","VEASECM","VTINDUS","WALMART","WALMCOM","WAREHSE","WAYFAIR","WBMASON","WES MAT","WESTPOR","WILDSAL","WILPPET","WOODSPE","WORLDS ","WORLDWI","WORTHCN","WORTHDR","WOZEN  ","WYLIE  ","YELLOW ","ZERO   "
        };
            foreach (string customerNo in customers)
            {
                customerList.Items.Add(customerNo, (customerNo == "AMAZOND" || customerNo == "WAYFAIR"));
            }
        }
        private bool CreateTravelers(CreationParams cp)
        {
            // Import new orders
            if (cp.mode == Mode.CreatePrinted || ImportOrders(cp.mode))
            {
                // for a specific traveler ID
                int travelerID = 0;
                try
                {
                    if (specificOrder.Text.Length < 7)
                    {
                        travelerID = Convert.ToInt32(specificOrder.Text);
                    }
                }
                catch (Exception ex)
                {

                }
                // Create travelers
                tableManager.CompileTravelers(backgroundWorker1, cp.mode, travelerID > 0 ? travelerID.ToString("D6") : specificOrder.Text);
                chairManager.CompileTravelers(backgroundWorker1, cp.mode, travelerID > 0 ? travelerID.ToString("D6") : specificOrder.Text);
                return true;
            }
            return false;
        }
        private void Clear()
        {
            summary = null;
            tableListView.Clear();
            tableManager.Orders.Clear();
            tableManager.Travelers.Clear();
            chairListView.Clear();
            chairManager.Orders.Clear();
            chairManager.Travelers.Clear();
        }

        //======================
        // Open Order
        //======================
        private bool ImportOrders(Mode mode)
        {
            //infoLabel.Text = "Importing Orders...";

            string today = DateTime.Today.ToString(@"yyyy\-MM\-dd");
            // OrderDate >= {d '" +  todayString + "'}
            string customerNames = "";
            string displayNames = "";
            for (int i = 0; i < customerList.Items.Count; i++)
            {
                if (customerList.GetItemCheckState(i) == CheckState.Checked)
                {
                    customerNames += (customerNames.Length > 0 ? "," : "") + "'" + customerList.GetItemText(customerList.Items[i]) + "'";
                    displayNames += (displayNames.Length > 0 ? ", " : "") + customerList.GetItemText(customerList.Items[i]);
                }
            }

            sortInfo = mode == Mode.CreateSelected ? "(" + displayNames + ")" : "";
            // get informatino from header
            OdbcCommand command = MAS.CreateCommand();
            string conditions = (mode == Mode.CreateSpecific ? "SalesOrderNo = '" + specificOrder.Text + "'" : (customerNames.Length != 0 && mode != Mode.CreateAll ? ("CustomerNo " + (mode == Mode.CreateUnselected ? "NOT" : "") + " IN (" + customerNames + ")") : "") + (showToday.Checked ? "AND OrderDate >= {d '" + today + "'}" : ""));
            command.CommandText = "SELECT SalesOrderNo, CustomerNo, ShipVia, ShipExpireDate FROM SO_SalesOrderHeader" + (conditions.Length > 0 ? " WHERE " + conditions : "");
            OdbcDataReader reader = command.ExecuteReader();
            // read info
            while (reader.Read())
            {
                // get information from detail
                OdbcCommand detailCommand = MAS.CreateCommand();
                detailCommand.CommandText = "SELECT ItemCode, QuantityOrdered, UnitOfMeasure FROM SO_SalesOrderDetail WHERE SalesOrderNo = '" + reader.GetString(0) + "'";
                OdbcDataReader detailReader = detailCommand.ExecuteReader();
                // Read each line of the Sales Order, looking for the base Table items, ignoring kits
                if (reader.GetString(0) == "5034477")
                {
                    var test = "test";
                }
                while (detailReader.Read())
                {
                    // Import bill & quantity 
                    string billCode = detailReader.GetString(0);
                    if (!detailReader.IsDBNull(2) && detailReader.GetString(2) != "KIT")
                    {
                        if (IsTable(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = reader.GetString(0);
                            }
                            if (!reader.IsDBNull(1))
                            {
                                order.CustomerNo = reader.GetString(1);
                            }
                            if (!reader.IsDBNull(2))
                            {
                                order.ShipVia = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3))
                            {
                                order.OrderDate = reader.GetDate(3);
                            }

                            order.ItemCode = billCode;
                            order.QuantityOrdered = Convert.ToInt32(detailReader.GetValue(1));
                            tableManager.Orders.Add(order);
                        }
                        else if (IsChair(billCode))
                        {
                            // this is a table
                            Order order = new Order();
                            // scrap this order if anything is missing
                            if (!reader.IsDBNull(0))
                            {
                                order.SalesOrderNo = reader.GetString(0);
                            }
                            if (!reader.IsDBNull(1))
                            {
                                order.CustomerNo = reader.GetString(1);
                            }
                            if (!reader.IsDBNull(2))
                            {
                                order.ShipVia = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3))
                            {
                                order.OrderDate = reader.GetDate(3);
                            }

                            order.ItemCode = billCode;
                            order.QuantityOrdered = Convert.ToInt32(detailReader.GetValue(1));
                            chairManager.Orders.Add(order);
                        }

                    }
                }
                detailReader.Close();
            }
            reader.Close();
            //infoLabel.Text = "";
            return true;
        }
        private bool IsTable(string s)
        {
            return (s.Length == 9 && s.Substring(0, 2) == "MG") || (s.Length == 10 && (s.Substring(0, 3) == "38-" || s.Substring(0, 3) == "41-"));
        }
        private bool IsChair(string s)
        {
            if (s.Length == 14 && s.Substring(0, 2) == "38")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 5 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else if (s.Length == 15 && s.Substring(0, 4) == "MG11")
            {
                string[] parts = s.Split('-');
                return (parts[0].Length == 6 && parts[1].Length == 4 && parts[2].Length == 3);
            }
            else
            {
                return false;
            }

        }

        //======================
        // Tables
        //======================
        private TableManager tableManager = new TableManager();
        //======================
        // Chairs
        //======================
        private ChairManager chairManager = new ChairManager();
        //======================
        // Events
        //======================
        // Print
        private void btnPrint_Click(object sender, EventArgs e)
        {
            infoLabel.Text = "Printing Travelers...";
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //var workbook = workbooks.Open(System.IO.Path.Combine(exeDir, "traveler.xlsx"),
            //    0, false, 5, "", "", false, 2, "",
            //    true, false, 0, true, false, false);
            var workbook = workbooks.Open(System.IO.Path.Combine(exeDir, "traveler.xlsx"),
                0, false, 5, "", "", false, 2, "",
                true, false, 0, true, false, false);
            var worksheets = workbook.Worksheets;

            if (chckTables.Checked) tableManager.PrintTravelers(worksheets);
            if (chckChairs.Checked) chairManager.PrintTravelers(worksheets);
        }
        // Print summary
        private void btnPrintSummary_Click(object sender, EventArgs e)
        {
            if (summary == null)
            {
                summary = new Summary(chckTables.Checked ? tableManager.Travelers : null, chckChairs.Checked ? chairManager.Travelers : null, sortInfo + (chckTables.Checked ? " Table " : "") + (chckChairs.Checked ? " Chair " : ""));
                summary.Print(workbooks);
            }
            else
            {
                summary.Print(workbooks);
            }
        }
        // Print labels
        private void btnPrintLabels_Click(object sender, EventArgs e)
        {
            tableManager.PrintLabels();
        }
        // Create Travelers (from selected customers)
        private void btnCreateTravelers_Click(object sender, EventArgs e)
        {
            if (clearBefore.Checked) Clear();
            backgroundWorker1.RunWorkerAsync(new CreationParams(Mode.CreateSelected, ""));
        }
        // Create Travelers(from unselected customers)
        private void btnInvertCustomers_Click(object sender, EventArgs e)
        {
            if (clearBefore.Checked) Clear();
            backgroundWorker1.RunWorkerAsync(new CreationParams(Mode.CreateUnselected, ""));
        }
        // Login to MAS
        private void login_Click(object sender, EventArgs e)
        {
            // connect to MAS and initialize managers
            InitializeManagers();
        }

        struct CreationParams
        {
            public CreationParams(Mode m, string s)
            {
                mode = m;
                specificID = s;
            }
            public Mode mode;
            public string specificID;
        }
        // Add specific order or traveler
        private void btnCreateSpecificOrder_Click(object sender, EventArgs e)
        {
            backgroundWorker1.RunWorkerAsync(new CreationParams(Mode.CreateSpecific, specificOrder.Text));
        }

        // Create only previously printed travelers
        private void btnCreatedPrinted_Click(object sender, EventArgs e)
        {
            if (clearBefore.Checked) Clear();
            backgroundWorker1.RunWorkerAsync(new CreationParams(Mode.CreatePrinted, ""));
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            CreateTravelers((CreationParams)e.Argument);
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            infoLabel.Text = e.UserState.ToString();
            progressBar.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            specificOrder.Text = "";
            tableManager.DisplayTravelers();
            chairManager.DisplayTravelers();
            infoLabel.Text = "Complete";
            progressBar.Value = 0;
            btnCreateTravelers.Enabled = true;
            btnInvertCustomers.Enabled = true;
        }

        private void btnClearAll_Click(object sender, EventArgs e)
        {
            Clear();
        }
        // Been Printed check
        private bool HasBeenPrinted(string s)
        {
            string exeDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            bool foundMatch = false;
            string line;
            System.IO.StreamReader file = new System.IO.StreamReader(System.IO.Path.Combine(exeDir, "printed.json"));
            int travelerID = 0;
            try
            {
                if (specificOrder.Text.Length < 7)
                {
                    travelerID = Convert.ToInt32(specificOrder.Text);
                }
            }
            catch (Exception ex)
            {

            }
            while ((line = file.ReadLine()) != null && line != "")
            {
                Traveler printedTraveler = new Traveler(line);
                // check to see if the number matches a traveler ID
                if (travelerID == printedTraveler.ID)
                {
                    foundMatch = true;
                    break;
                }
                // check to see if these orders have been printed already
                foreach (Order printedOrder in printedTraveler.Orders)
                {
                    if (printedOrder.SalesOrderNo == specificOrder.Text)
                    {
                        foundMatch = true;
                        break;
                    }
                }
                if (foundMatch) break;
            }
            infoLabel.Text = (travelerID > 0 ? "Traveler " + travelerID.ToString("D6") : "Order " + specificOrder.Text) + (foundMatch ? " Has been printed" : " Has not been printed");
            return false;
        }
        private void button5_Click(object sender, EventArgs e)
        {
            HasBeenPrinted(specificOrder.Text);
        }

        private void btnCreateAll_Click(object sender, EventArgs e)
        {
            if (clearBefore.Checked) Clear();
            backgroundWorker1.RunWorkerAsync(new CreationParams(Mode.CreateAll, ""));
        }
    }
}
