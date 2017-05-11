// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 2/13/2017

var application = new Application();

// gets called once the html is loaded
function Initialize() {
	//window.oncontextmenu = function () {return false;}
	application.Initialize();
}
function Test(e,self) {
	if (e.target == self) application.popupManager.CloseAll(); 
	return false;
}
function Application () {
	this.type = "supervisor";
	// DOM
	this.queueArray;
	this.JSONviewer;
	this.popupManager;
	this.IOScheckTimeout;
	// DATA
	this.labelTypes = [];
	this.stationList = [];
	this.travelers = [];
	this.queues = {};
	this.view = {
		viewState:undefined
	}
	// MISC
	this.lastSelectedStation;
	// IO
	this.input = new Input();
	this.selection = {
		lastQueue: undefined,
		lastTraveler: undefined
	};
	// Websocket
	this.websocket;
	this.SetWindow = function () {
		// Small screens
		var fontsize = Math.max(8,Math.min(20,Math.round(window.innerWidth/24)));
		document.body.style.fontSize = fontsize + "px";
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
		
		var viewContainer = document.getElementById("viewContainer");
		var queueContainer = document.getElementById("queueContainer");
		var interfaceContainer = document.getElementById("interfaceContainer");
		
		// change the size of the queues with respect to the font size
		for (var key in application.queues) {
			application.queues[key].DOMcontainer.style.width = "auto";
		}
		
		if (window.innerHeight / window.innerWidth < (3/4)) {
			// landscape layout
		} else {
			// portrait layout

		}
		
	};
	this.LoginPopup = function (info) {
		var self = this;
		// station list
		if (self.stationList.length > 0) self.InitStations(self.stationList);
		// logout button text
		document.getElementById("logoutBtn").innerHTML = "Logout";
		// popup stuff
		self.popupManager.CloseAll();
		self.StopAutofocus();
		var loginPopup = document.getElementById("loginPopup");//.cloneNode(true);
		
		self.popupManager.AddSpecific("loginPopup");
		self.popupManager.Lock(loginPopup);
		// Extra info
		document.getElementById("loginInfo").innerHTML = (info ? info : "");
		// login submit
		document.getElementById("loginBtn").onclick = function (evt) {
			evt.preventDefault();
			if (document.getElementById("uidBox").value != "") {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("Login",
				{
					UID: document.getElementById("uidBox").value,
					PWD: document.getElementById("pwdBox").value,
				},"This");
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				self.popupManager.Close(loginPopup);
			}
			return false;
		}
	}
	this.LoginSuccess = function (name) {
		var self = this;
		self.popupManager.Unlock();
		self.popupManager.CloseAll();
		self.StartAutofocus();
		document.getElementById("logoutBtn").className = "dark button oneEM";
		// LOG OUT BUTTON
		var logoutBtn = document.getElementById("logoutBtn");
		logoutBtn.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("Logout");
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.LoginPopup();
			
		}
		self.userID = document.getElementById("uidBox").value;
		logoutBtn.innerHTML = "Logout " + name;
		
	}
	this.StartAutofocus = function () {
		window.addEventListener("keydown",this.Autofocus);
	}
	this.StopAutofocus = function () {
		window.removeEventListener("keydown",this.Autofocus);
	}
	this.Autofocus = function () {
		var self = this;
		if (searchBox != document.activeElement)  {application.FocusOnSearch();}
		clearTimeout(self.IOScheckTimeout)
		self.IOScheckTimeout = setTimeout(function () {
			if (searchBox.value.length >= 11) {
				document.getElementById("searchForm").onsubmit();
			}
		},500);
	}
	//----------------
	// Multi-select
	//----------------
	this.SelectRange = function (A,B) {
		var self = this;
		// if they are in the same queue
		if (A && B && A.stationQueue && B.stationQueue && A.stationQueue == B.stationQueue) {
			for (var i = Math.min(A.queueIndex,B.queueIndex); i < Math.max(A.queueIndex,B.queueIndex); i++) {
				var traveler = self.queues[A.stationQueue].travelers[i];
				traveler.Select(true);
			}
		}
	}
	//----------------
	// station list
	//----------------
	this.InitStations = function (stationList) {
		var self = this;
		self.stationList = stationList;
		var start;
		self.stationList.forEach(function (station) {
			var queue = new TravelerQueue(station);
			if (station.name == "Start") {
				start = queue;
			} else {
				self.queueArray.appendChild(queue.DOMcontainer);
			}
			self.queues[station.name] = queue;
		});
		// put the start queue at the beginning
		self.queueArray.insertBefore(start.DOMcontainer, self.queueArray.childNodes[0]);
		self.SetWindow();
	}
	this.QuantityAt = function (obj) {
		this.queues[obj.station].totalQtyElem.innerHTML = obj.quantity;
	}
	// updates the queues with the current travelers
	this.HandleTravelersChanged = function () {
		var self = this;
		// clear the queues
		for (var station in self.queues) {
			self.queues[station].Clear();
		}
		// add all the travelers back
		self.travelers.forEach(function (traveler) {
			traveler.stations.forEach(function (station) {
				if (Contains(traveler.items,[{prop:"state",value:self.view.viewState},{prop:"station",value:station}]) || (traveler.items.length == 0 && traveler.state == self.view.viewState)
					|| (traveler.qtyPending > 0)) {
					// QTY pending is sent based on the starting station for the traveler from the Export function on Traveler.cs
					var copy = new Traveler(JSON.parse(JSON.stringify(traveler)));
					copy.stationQueue = station;
					copy.queueIndex = self.queues[station].travelers.length;
					self.queues[station].AddTraveler(copy);
				}
			});
		});
		// update summary, if open
		if (self.popupManager.Exists("summaryPopup")) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("CreateSummary",{
				sort:"Active"
			});
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
		}
	}
	this.Info = function (message) {
		this.popupManager.Info(message);
	}
	this.InitLabelTypes = function (labelTypes) {
		this.labelTypes = labelTypes;
	}
	// Loads the traveler GUI
	this.LoadTraveler = function (traveler) {
		this.TravelerPopup(traveler);
		
		
	}
	this.LoadTravelerJSON = function (traveler) {
		//this.JSONviewer = new JSONviewer(traveler,"Traveler");
		this.popupManager.AddJSONviewer(traveler,"Traveler");
	}
	this.LoadTravelerAt = function (traveler) {
		this.TravelerPopup(traveler);
	}
	this.TravelerPopup = function (traveler) {
		var self = this;
		var promptBox = document.getElementById("travelerPopup");//.cloneNode(true);
		var closeFunction = application.popupManager.AddSpecific("travelerPopup");
		/* // clear the promptBox
		while (promptBox.hasChildNodes()) {
			promptBox.removeChild(promptBox.lastChild);
		} */
		var promptInfo = document.getElementById("promptInfo");
		document.getElementById("promptInfoStation").innerHTML = (traveler.station ? traveler.station : "");
		document.getElementById("promptInfoTravelerID").innerHTML = pad(traveler.ID,6);
		document.getElementById("promptInfoItemCode").innerHTML = traveler.itemCode;
		document.getElementById("promptInfoQuantity").innerHTML = traveler.quantity;
		document.getElementById("promptInfoPending").innerHTML = (traveler.qtyPending ? traveler.qtyPending : "-");
		document.getElementById("promptInfoCompleted").innerHTML = (traveler.qtyCompleted ? traveler.qtyCompleted : "-");
		document.getElementById("promptInfoAction").innerHTML = "Move [" + pad(traveler.ID,6) + "]'s starting location to...";
		//-----------------
		// Move starting station to...
		//-----------------
		var promptSelect = document.getElementById("promptSelect");
		ClearChildren(promptSelect);
		// add the station options
		application.stationList.forEach(function (station) {
			if (station.creates.indexOf(traveler.type) != -1 || station.name === "Start") {
				var option = document.createElement("OPTION");
				option.innerHTML = station.name;
				option.value = station.name;
				promptSelect.appendChild(option);
			}
		});
		promptSelect.value = self.lastSelectedStation;
		//-----------------
		// Move button
		//-----------------
		var promptMoveBtn = document.getElementById("promptMoveBtn");
		promptMoveBtn.onclick = function () {
			self.lastSelectedStation = promptSelect.value;
			
			
			// perform move on all selected travelers
			self.travelers.forEach(function (extraTraveler) {
				if (extraTraveler.ID == traveler.ID || extraTraveler.selected) {
					
				}
			});
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("MoveTravelerStart",
			{
				travelerIDs: application.GetSelectedIDs().concat(traveler.ID),
				station: promptSelect.value
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			
			closeFunction();
		}
		//-----------------
		// Info button
		//-----------------
		document.getElementById("promptInfoBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("LoadTravelerJSON",
			{
				travelerID: traveler.ID
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
		}
		//---------------------
		// Traveler options 
		//---------------------
		document.getElementById("travelerOptionsBtn").onclick = function () {
			var popup = application.popupManager.CreatePopup();
			// More Info --------------
			var infoBtn = application.popupManager.CreateButton("More Info");
			infoBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("LoadTravelerJSON",
				{
					travelerID: traveler.ID
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				//document.getElementById("searchBox").value = traveler.ID;
				//document.getElementById("searchForm").onsubmit();
			}
			popup.appendChild(infoBtn);
			//-------------------------
			
			// Disintegrate ----------
			var disintegrateBtn = application.popupManager.CreateButton("Disintegrate this traveler");
			AddTooltip(disintegrateBtn,"Deletes the traveler, and releases the orders to create and combine into new travelers during the next system update");
			disintegrateBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("DisintegrateTraveler",
				{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				
				closeFunction();
			}
			popup.appendChild(disintegrateBtn);
			//-------------------------
			
			// Enter Production ----------
			var enterProductionBtn = application.popupManager.CreateButton("Enter Production");
			enterProductionBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("EnterProduction",
				{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				
				closeFunction();
			}
			popup.appendChild(enterProductionBtn);
			//----------------------------
			application.popupManager.AddCustom(popup);
			
			// PRINT LABLES--------------
			var printBtn = application.popupManager.CreateButton("Print Labels");
			printBtn.onclick = function () {
				application.popupManager.Close(popup);
				application.popupManager.AddSpecific("labelPopup");
				application.PrintLabelPopup(traveler);
				/* //----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("CreateSummary",{
					sort: "Active",
					type: "Table",
					from: "",
					to: ""
				});
				self.websocket.send(JSON.stringify(message));
				//----------------------------------------------- */
			}
			popup.appendChild(printBtn);
		}
	}
	// Loads the item GUI
	this.LoadItem = function (item) {
		this.popupManager.AddJSONviewer(item,"Item");
		//this.JSONviewer = new JSONviewer(item,"Traveler Item");
	}
	this.Info = function (message) {
		this.popupManager.Info(message);
	}
	this.FocusOnSearch = function () {
		document.getElementById("searchBox").value = "";
		document.getElementById("searchBox").focus();
	}
	//----------------
	// supervisor Options (called from the server)
	//----------------
	this.UserForm = function (format) {
		var self = this;
		//self.popupManager.CloseAll();
		self.StopAutofocus();
		self.popupManager.Form(format, function (filledForm) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("NewUser",filledForm);
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.StartAutofocus();
		});
		
	}
	this.CreateSummary = function (summaryObj) {
		var self = this;
		self.popupManager.CloseAll();
		var closeFunction = self.popupManager.AddSpecific("summaryPopup");//.cloneNode(true));
		
		var summaryTable = document.getElementById("summary"); // TABLE
		
		while(summaryTable.rows.length > 0) {
			summaryTable.deleteRow(0);
		}	
		if (summaryObj.items.length > 0) {
			document.getElementById("summaryTitle").innerHTML = summaryObj.sort + " travelers";
			
			var header = Object.keys(summaryObj.items[0]);
			var headerRow = document.createElement("TR");
			header.forEach(function (key) {
				var th = document.createElement("TH");
				th.innerHTML = key;
				headerRow.appendChild(th);
			});
			summaryTable.appendChild(headerRow);
			summaryObj.items.forEach(function (item) {
				var row = document.createElement("TR");
				header.forEach(function (key) {
					var td = document.createElement("TD");
					if (item[key] != undefined) td.innerHTML = item[key];
					row.appendChild(td);
				});
				summaryTable.appendChild(row);
			});
		} else {
			self.popupManager.CloseAll();
			self.popupManager.Info("There are no items to display");
		}
	}
	// Utility
	this.GetSelectedIDs = function () {
		var self = this;
		var selectedIDs = [];
		for (var queueName in self.queues) {
			self.queues[queueName].travelers.forEach(function (traveler) {
				if (traveler.selected) {
					selectedIDs.push(traveler.ID);
				}
			});
		}
		return selectedIDs;
	}
	//----------------
	// DOM events
	//----------------
	this.ViewChanged = function (viewForm) {
		var self = this;
		
		var viewStateRadios = document.getElementsByName("viewState");
		for (var i = 0; i < viewStateRadios.length; i++) {
			if (viewStateRadios[i].checked) {
				self.view.viewState = viewStateRadios[i].value;
				break;
			}
		}
		
		//----------INTERFACE CALL-----------------------
		var message = new InterfaceCall("SetViewFilter",
		{
			viewState: self.view.viewState
		},"This");
		self.websocket.send(JSON.stringify(message));
		//-----------------------------------------------
		
		/* for (var queue in self.queues) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("QuantityAt",
			{
				station: queue,
				type: "Traveler"
			},"This");
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
		} */
	}
	this.Redirect = function(location) {
		window.location = location;
	}
	this.InterfaceOpen = function () {
		// configure the default view settings with the server
		document.getElementById("viewForm").onchange();
	}
	this.PrintLabelPopup = function (traveler) {
		var self = this;
		var labelSelect = document.getElementById("labelSelect");
		ClearChildren(labelSelect);
		self.labelTypes.forEach(function (type) {
			var option = document.createElement("OPTION");
			option.value = type;
			option.innerHTML = type;
			labelSelect.appendChild(option);
		});
		
		var itemSelect = document.getElementById("itemSelect");
		ClearChildren(itemSelect);
		traveler.items.forEach(function (item) {
			var option = document.createElement("OPTION");
			option.value = item.ID;
			option.innerHTML = item.ID;
			itemSelect.appendChild(option);
		});
		
		document.getElementById("printLabelBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("PrintLabel",{
				travelerID: traveler.ID,
				itemID: document.getElementById("itemSelect").value,
				labelType: document.getElementById("labelSelect").value,
				quantity: 1
			});
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
		}
	}
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		self.popupManager = new PopupManager(document.getElementById("blackout"));
		self.SetWindow();
		window.addEventListener("resize",self.SetWindow,false);
		//----------------
		// Input
		//----------------
		self.input.Initialize();
		//----------------
		// search
		//----------------
		var searchBox = document.getElementById("searchBox");

		//window.addEventListener("keydown",);
		document.getElementById("searchForm").onsubmit = function () {
			var searchArray = searchBox.value.split('-');
			// try to parse the search string
			var travelerID = parseInt(searchArray[0],10);
			var itemID = parseInt(searchArray[1],10);

			if (!isNaN(travelerID)) {
				if (!isNaN(itemID)) {
					// attempt to load the item
					//----------INTERFACE CALL-----------------------
					var message = new InterfaceCall("LoadItem",
					{
						travelerID: travelerID,
						itemID: itemID
					});
					self.websocket.send(JSON.stringify(message));
					//-----------------------------------------------
				} else {
					// attempt to load the traveler
					//----------INTERFACE CALL-----------------------
					var message = new InterfaceCall("LoadTraveler",
					{
						travelerID: travelerID
					});
					self.websocket.send(JSON.stringify(message));
					//-----------------------------------------------
				}
			} else {
				self.Info("Invalid traveler ID :(");
			}
			searchBox.value = "";
			return false;
		}
		
		//----------------
		// supervisor Options
		//----------------
		
		document.getElementById("superOptionsBtn").onclick = function () {
			var popup = self.popupManager.CreatePopup();
			// OPEN SUMMARY CURRENT SUMMARY VIEW--------------
			var summaryBtn = self.popupManager.CreateButton("View Summary");
			summaryBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("CreateSummary",{
					sort: "Active",
					type: "Table",
					from: "",
					to: ""
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(summaryBtn);
			
			
			
			// DOWNLOAD SUMMARY (AVAILABLE TRAVELERS)--------------
			var downloadSummaryBtn = self.popupManager.CreateButton("Download Table Summary<br>(Available in Start)");
			downloadSummaryBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("DownloadSummary",{
					sort: "Available",
					type: "Table"
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(downloadSummaryBtn);
			
			// DOWNLOAD SUMMARY (SORTED TRAVELERS)--------------
			var sortedSummary = self.popupManager.CreateButton("Download Table Summary<br>(By Station)");
			sortedSummary.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("DownloadSummary",{
					sort: "Sorted",
					type: "Table"
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(sortedSummary);
			
			// DOWNLOAD SUMMARY (Production)--------------
			var productionBtn = self.popupManager.CreateButton("Download Table Production");
			productionBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("ExportProduction",{
					sort: "All",
					type: "Table"
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(productionBtn);
			
			// DOWNLOAD SUMMARY (SORTED TRAVELERS)--------------
			var scrapBtn = self.popupManager.CreateButton("Download Table Scrap");
			scrapBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("ExportScrap",{
					sort: "All",
					type: "Table"
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(scrapBtn);
			
			// ADD NEW USER --------------
			var newUserBtn = self.popupManager.CreateButton("New User");
			newUserBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("UserForm");
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(newUserBtn);
			
			// EDIT USER --------------
			var editUserBtn = self.popupManager.CreateButton("Edit User");
			editUserBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("EditUser");
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(editUserBtn);
			
			self.popupManager.AddCustom(popup);
		}
		
		
		//----------------
		// queueArray
		//----------------
		self.queueArray = document.getElementById("queueArray");
		//----------------
		// Websocket
		//----------------
		if ("WebSocket" in window) {
			console.log("WebSocket is supported by your Browser!");
		   
			// Lets open a web socket
			self.websocket = new WebSocket("ws://" + config.server + ":" + config.port + "/");
			
			self.websocket.onopen = function() {
				console.log("Connection is open...");
				// Web Socket is connected, send data using send()
				// send the client type identification
				self.websocket.send("SupervisorClient");
				self.LoginPopup();
			};
			
			self.websocket.onmessage = function(messageEvent) {
				if (typeof messageEvent.data === "string") {
					// recieved text data
					
					// verify the integrity of the json message
					var object;
					try {
						object = JSON.parse(messageEvent.data)
					} catch (exception) {
						console.log(exception + " : " + messageEvent.data);
					}
					if (object) {					
						// valid json object recieved, time to hande the message
						if (!object.hasOwnProperty("ping")) {
							if (object.hasOwnProperty("travelers") && object.hasOwnProperty("mirror")) {
								if (object.mirror) {
									self.travelers = [];
									object.travelers.forEach(function (obj) {
										var traveler = new Traveler(obj);
										self.travelers.push(traveler);
									});
								} else {
									object.travelers.forEach(function (obj) {
										self.travelers.forEach(function (traveler, index) {
											if (traveler.ID == obj.ID) {
												self.travelers[index] = new Traveler(obj);
											}
										});
									});
								}
								self.HandleTravelersChanged();
							}
							if (object.hasOwnProperty("method")) {
								if (self.hasOwnProperty(object.method) && object.hasOwnProperty("parameters")) {
									// The server is invoking a client method
									if (object.parameters != "") {
										self[object.method](object.parameters);
									} else {
										self[object.method]();
									}
								}
							}
						}
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved binary data
				}
			};
			// websocket is closed.
			self.websocket.onclose = function() {
				self.popupManager.Error("You are not connected to the server;<br> either refresh the page, or inform Gage Coates");
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
	}
}
function TravelerQueue(station) {
	this.DOMcontainer;
	this.DOMelement;
	this.totalQtyElem;
	this.totalLaborElem;
	this.travelers;
	this.station;
	
	this.Clear = function () {
		this.travelers = [];
		this.RePaint();
	}
	this.AddTraveler = function (traveler) {
		this.travelers.push(traveler);
		this.RePaint();
	}
	this.UnshiftTraveler = function (traveler) {
		this.travelers.unshift(traveler);
		this.RePaint();
	}
	this.ShiftTraveler = function (traveler) {
		var self = this;
		// find the traveler
		var shiftedTraveler;
		for (var i = 0; i < self.travelers.length; i++) {
			if (self.travelers[i].ID === traveler.ID) {
				// swap this one with the first element
				self.travelers[i] = JSON.parse(JSON.stringify(self.travelers[0]));
				self.travelers[0] = traveler;
				// shift the first element out of existence
				shiftedTraveler = self.travelers.shift();		
			}
		}
		self.RePaint();
		return shiftedTraveler;
	}
	this.RePaint = function () {
		var self = this;
		// clear old DOM objects
		while (self.DOMelement.hasChildNodes()) {
			self.DOMelement.removeChild(self.DOMelement.lastChild);
		}
		var totalQty = 0;
		var totalLabor = 0;
		// create and add the new DOM objects
		self.travelers.forEach(function (traveler) {
			self.DOMelement.appendChild(traveler.CreateQueueItem(self.station.name));
			totalQty += traveler.quantity;
			totalLabor += traveler.totalLabor;
		});
		self.totalQtyElem.innerHTML = totalQty;
		self.totalLaborElem.innerHTML = totalLabor;
	}
	this.BalanceSliders = function(qtyMoving,qtyStaying,movingBar,stayingBar,traveler) {
		movingBar.style.width = ((parseInt(qtyMoving.value) / traveler.quantity) * 100) + "%";
		stayingBar.style.width = ((parseInt(qtyStaying.value) / traveler.quantity) * 100) + "%";
	}
	/* this.PromptAction = function (traveler) {
		var self = this;
		var promptBox = document.getElementById("travelerPopup");//.cloneNode(true);
		var closeFunction = application.popupManager.AddSpecific("travelerPopup");
		//// clear the promptBox
		//while (promptBox.hasChildNodes()) {
		//	promptBox.removeChild(promptBox.lastChild);
		//} 
		var promptInfo = document.getElementById("promptInfo");
		document.getElementById("promptInfoStation").innerHTML = self.station.name;
		document.getElementById("promptInfoTravelerID").innerHTML = pad(traveler.ID,6);
		document.getElementById("promptInfoItemCode").innerHTML = traveler.itemCode;
		document.getElementById("promptInfoQuantity").innerHTML = traveler.quantity;
		document.getElementById("promptInfoPending").innerHTML = traveler.qtyPending;
		document.getElementById("promptInfoCompleted").innerHTML = traveler.qtyCompleted;
		document.getElementById("promptInfoAction").innerHTML = "Move [" + pad(traveler.ID,6) + "]'s starting location to...";
		//-----------------
		// Move starting station to...
		//-----------------
		var promptSelect = document.getElementById("promptSelect");
		ClearChildren(promptSelect);
		// add the station options
		application.stationList.forEach(function (station) {
			if (station.creates.indexOf(traveler.type) != -1 || station.name === "Start") {
				var option = document.createElement("OPTION");
				option.innerHTML = station.name;
				option.value = station.name;
				promptSelect.appendChild(option);
			}
		});
		promptSelect.value = self.lastSelectedStation;
		//-----------------
		// Move button
		//-----------------
		var promptMoveBtn = document.getElementById("promptMoveBtn");
		promptMoveBtn.onclick = function () {
			self.lastSelectedStation = promptSelect.value;
			
			
			// perform move on all selected travelers
			self.travelers.forEach(function (extraTraveler) {
				if (extraTraveler.ID == traveler.ID || extraTraveler.selected) {
					
				}
			});
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("MoveTravelerStart",
			{
				travelerIDs: application.GetSelectedIDs().concat(traveler.ID),
				station: promptSelect.value
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			
			closeFunction();
		}
		//-----------------
		// Info button
		//-----------------
		document.getElementById("promptInfoBtn").onclick = function () {
			document.getElementById("searchBox").value = traveler.ID;
			document.getElementById("searchForm").onsubmit();
		}
		//---------------------
		// Traveler options 
		//---------------------
		document.getElementById("travelerOptionsBtn").onclick = function () {
			var popup = application.popupManager.CreatePopup();
			// More Info --------------
			var infoBtn = application.popupManager.CreateButton("More Info");
			infoBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				document.getElementById("searchBox").value = traveler.ID;
				document.getElementById("searchForm").onsubmit();
			}
			popup.appendChild(infoBtn);
			//-------------------------
			
			// Disintegrate ----------
			var disintegrateBtn = application.popupManager.CreateButton("Disintegrate this traveler");
			AddTooltip(disintegrateBtn,"Deletes the traveler, and releases the orders to create and combine into new travelers during the next system update");
			disintegrateBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("DisintegrateTraveler",
				{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				
				closeFunction();
			}
			popup.appendChild(disintegrateBtn);
			//-------------------------
			
			// Enter Production ----------
			var enterProductionBtn = application.popupManager.CreateButton("Enter Production");
			enterProductionBtn.onclick = function () {
				application.popupManager.Close(popup);
				
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("EnterProduction",
				{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				
				closeFunction();
			}
			popup.appendChild(enterProductionBtn);
			//----------------------------
			application.popupManager.AddCustom(popup);
			
			// PRINT LABLES--------------
			var printBtn = application.popupManager.CreateButton("Print Labels");
			printBtn.onclick = function () {
				application.popupManager.Close(popup);
				application.popupManager.AddSpecific("labelPopup");
				application.PrintLabelPopup(traveler);
				//----------INTERFACE CALL-----------------------
				//var message = new InterfaceCall("CreateSummary",{
				//	sort: "Active",
				//	type: "Table",
				//	from: "",
				//	to: ""
				//});
				//self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(printBtn);
		}
	} */
	this.Initialize = function (station) {
		var self = this;
		self.station = station;
		
		self.DOMcontainer = document.createElement("DIV");
		self.DOMcontainer.className = "queueContainer";
		if (station.name == "Start") {
			self.DOMcontainer.className = "queueContainer--tall";
		}
		var queueTitle = document.createElement("DIV");
		queueTitle.className = "heading";
		queueTitle.innerHTML = self.station.name;
		self.DOMcontainer.appendChild(queueTitle);
		// Total traveler quantity ---------
		var totalQtyElem = document.createElement("DIV");
		totalQtyElem.innerHTML = "Total Qty:";
		
		self.totalQtyElem = document.createElement("SPAN");
		self.totalQtyElem.className = "beige";
		totalQtyElem.appendChild(self.totalQtyElem);
		self.DOMcontainer.appendChild(totalQtyElem);
		// Total labor quantity ------------
		var totalLaborElem = document.createElement("DIV");
		totalLaborElem.innerHTML = "Total Labor:";
		
		self.totalLaborElem = document.createElement("SPAN");
		self.totalLaborElem.className = "beige";
		totalLaborElem.appendChild(self.totalLaborElem);
		totalLaborElem.appendChild(document.createTextNode(" min"));
		self.DOMcontainer.appendChild(totalLaborElem);
		//----------------------------------
		
		self.DOMelement = document.createElement("DIV");
		self.DOMelement.className = "queue";
		
		self.DOMcontainer.appendChild(self.DOMelement);
		self.travelers = [];
	}
	this.Shutdown = function () {
		this.DOMelement.parent.removeChild(this.DOMelement);
		this.travelers = [];
	}
	this.Initialize(station);
}
function TravelerView() {
	// properties
	this.traveler;
	this.destination;
	// DOM
	this.DOMcontainer;
	this.btnComplete;
	this.Clear = function () {
		var self = this;
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
	}
	this.Load = function (traveler) {
		var self = this;
		
		// initialize
		self.traveler = traveler;
		self.Clear();
		// initialize the destination list
		var destList = document.getElementById("destList");
		// remove old
		while (destList.firstChild) {
			destList.removeChild(destList.firstChild);
		}
		application.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station;
			option.className = "dark button";
			option.value = station;
			destList.appendChild(option);
		});
		
		// clear old DOM objects
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		// create the table
		var DOMtable = document.createElement("TABLE");
		DOMtable.className = "view";
		// create the complete (or uncomplete) button
		self.btnComplete = document.createElement("DIV");
		self.btnComplete.className = "button";
		// create and add new DOM objects
		document.getElementById("destList").value = self.traveler.nextStation;
		// configure complete button
		self.btnComplete.innerHTML = "Complete";
		self.btnComplete.className = "dark button fourEM";
		self.btnComplete.onclick = function () {
			document.getElementById("blackout").style.visibility = "visible";
			// reset the qtyMade number input
			var qtyMade = document.getElementById("qtyMade");
			qtyMade.min = 0;
			qtyMade.max = self.traveler.quantity;
			qtyMade.step = 1;
			qtyMade.value = self.traveler.quantity;
		}
		
		// header
		var headerRow = document.createElement("TR");
		// traveler ID
		var ID = document.createElement("TH");
		ID.className = "view__headerItem shadow";
		ID.innerHTML = traveler.ID;
		headerRow.appendChild(ID);
		// Part number
		var itemCode = document.createElement("TH");
		itemCode.className = "view__headerItem red shadow";
		itemCode.innerHTML = (traveler.itemCode ? traveler.itemCode : "");
		headerRow.appendChild(itemCode);
		// Quantity
		var quantity = document.createElement("TH");
		quantity.className = "view__headerItem shadow";
		quantity.innerHTML = traveler.quantity;
		headerRow.appendChild(quantity);
		// add the header row to the table
		DOMtable.appendChild(headerRow);
		// all other properties are in the table body
		traveler.members.forEach(function (property) {
			var row = document.createElement("TR");
			// Property name
			var propName = document.createElement("TD");
			propName.className = "view__item";
			propName.innerHTML = property.name;
			row.appendChild(propName);
			// Property value
			var propValue = document.createElement("TD");
			propValue.className = "view__item";
			propValue.innerHTML = property.value;
			row.appendChild(propValue);
			// Property quantity (if it has a quantity)
			var propQty = document.createElement("TD");
			if (property.qty != "") {
				propQty.className = "view__item center";
				propQty.innerHTML = property.qty;
			} else {
				propQty.className = "view__item--null";
			}
			row.appendChild(propQty);
			// add the row to the table
			DOMtable.appendChild(row);
		});
		// add the table
		self.DOMcontainer.appendChild(DOMtable);
		// add the complete button
		self.DOMcontainer.appendChild(self.btnComplete);
	
		// start the timer
		self.StartTimer();
	}
	this.Initialize = function () {
		var self = this;
		self.DOMcontainer = document.getElementById("viewContainer");
	}
}
// handle input events
function Input () {
	// input states
	this.keyMap = [];
	this.mouse = {
		x: 0,
		y: 0,
		left: false,
		middle: false,
		right: false
	}
	// application controls
	this.left = false;
	this.right = false;
	this.up = false;
	this.down = false;
	this.space = false;
	this.shift = false;
	this.escape = false;
	this.ctrl = false;
	// key bindings
	this.binding = {
		left: [65,37],
		right: [68,39],
		up: [87,38],
		down: [83,40],
		space: [32],
		shift: [16],
		escape: [27],
		ctrl: [17]
	}
	this.UpdateAction = function () {
		var self = this;
		for (var action in self.binding) {
			self.binding[action].some(function (keyCode) {
				if (self.keyMap[keyCode]) {
					self[action] = true;
					return true;
				} else {
					self[action] = false;
				}
			});
		}
	}
	this.Initialize = function () {
		var self = this;
		
		// set all keys to false
		for (var i = 0; i < 222; i++) {
			self.keyMap.push(false);
		}
		
		// add key listeners
		window.addEventListener('keydown', function (event) {
			//event.preventDefault();
			self.keyMap[event.keyCode] = true;
			self.UpdateAction();
		});
		window.addEventListener('keyup', function (event) {
			//event.preventDefault();
			self.keyMap[event.keyCode] = false;
			self.UpdateAction();
		});
		/* // add mouse listeners (only on the canvas)
		canvas.addEventListener('mousedown', function (event) {
			event.preventDefault();
			switch (event.which) {
				case 1: self.mouse.left = true; break;
				case 2: self.mouse.middle = true; break;
				case 3: self.mouse.right = true; break;
			}
		});
		canvas.addEventListener('mouseup', function (event) {
			event.preventDefault();
			switch (event.which) {
				case 1: self.mouse.left = false; break;
				case 2: self.mouse.middle = false; break;
				case 3: self.mouse.right = false; break;
			}
		});
		canvas.addEventListener('mousemove', function (event) {
			event.preventDefault();
			var rect = canvas.getBoundingClientRect();
			self.mouse.x = event.clientX - rect.left;
			self.mouse.y = event.clientY - rect.top;
		}); */
	}
}