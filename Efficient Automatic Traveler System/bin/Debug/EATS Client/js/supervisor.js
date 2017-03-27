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
	// DOM
	this.queueArray;
	this.JSONviewer;
	this.popupManager;
	this.IOScheckTimeout;
	// DATA
	this.stationList = [];
	this.travelers = [];
	this.queues = {};
	// MISC
	this.lastSelectedStation;
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
	
	//----------------
	// station list
	//----------------
	this.PopulateQueues = function () {
		var self = this;
		self.stationList.forEach(function (station) {
			var queue = new TravelerQueue(station);
			self.queueArray.appendChild(queue.DOMcontainer);
			
			self.queues[station.ID] = queue;
		});
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
				
				self.queues[station].AddTraveler(traveler);
				
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
	// Loads the traveler GUI
	this.LoadTraveler = function (traveler) {
		this.popupManager.AddJSONviewer(traveler,"Traveler");
		//this.JSONviewer = new JSONviewer(traveler,"Traveler");
	}
	this.LoadTravelerAt = function (traveler) {
		this.queues[traveler.station].PromptAction(traveler);
	}
	this.TravelerPopup = function (traveler) {
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
	// supervisor Options
	//----------------
	this.CreateSummary = function (summaryObj) {
		var self = this;
		self.popupManager.CloseAll();
		self.popupManager.AddCustom(document.getElementById("summaryPopup").cloneNode(true));
		
		var summaryTable = document.getElementById("summary"); // TABLE
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
	
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		self.popupManager = new PopupManager(document.getElementById("blackout"));
		self.SetWindow();
		window.addEventListener("resize",self.SetWindow,false);
		//----------------
		// search
		//----------------
		var searchBox = document.getElementById("searchBox");

		window.addEventListener("keydown",function () {
			if (searchBox != document.activeElement)  {application.FocusOnSearch();}
			clearTimeout(self.IOScheckTimeout)
			self.IOScheckTimeout = setTimeout(function () {
				if (searchBox.value.length >= 11) {
					document.getElementById("searchForm").onsubmit();
				}
			},500);
		});
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
			// OPEN SUMMARY --------------
			var summaryBtn = self.popupManager.CreateButton("View Summary");
			summaryBtn.onclick = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("CreateSummary",{
					sort: "Active"
				});
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			popup.appendChild(summaryBtn);
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
							if (object.hasOwnProperty("stationList")) {
								self.stationList = object.stationList;
								self.PopulateQueues();
								self.SetWindow();
							}
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
									self[object.method](object.parameters);
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
		// create and add the new DOM objects
		self.travelers.forEach(function (traveler) {
			var DOMqueueItem = document.createElement("DIV");
			DOMqueueItem.className = "button queue__item blueBack twoEM";
			DOMqueueItem.innerHTML = pad(traveler.ID,6) + "<br>";
			var itemCode = document.createElement("SPAN");
			itemCode.className = "queue__item__desc beige";
			itemCode.innerHTML = traveler.itemCode;
			DOMqueueItem.appendChild(itemCode);
			DOMqueueItem.onmousedown = function () {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("LoadTravelerAt",
				{
					travelerID: traveler.ID,
					station: self.station.ID
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				//self.PromptAction(traveler);
			}
			self.DOMelement.appendChild(DOMqueueItem);
		});
	}
	this.BalanceSliders = function(qtyMoving,qtyStaying,movingBar,stayingBar,traveler) {
		movingBar.style.width = ((parseInt(qtyMoving.value) / traveler.quantity) * 100) + "%";
		stayingBar.style.width = ((parseInt(qtyStaying.value) / traveler.quantity) * 100) + "%";
	}
	this.PromptAction = function (traveler) {
		var self = this;
		var promptBox = document.getElementById("travelerPopup").cloneNode(true);
		var closeFunction = application.popupManager.AddCustom(promptBox);
		/* // clear the promptBox
		while (promptBox.hasChildNodes()) {
			promptBox.removeChild(promptBox.lastChild);
		} */
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
		// add the station options
		application.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station.name;
			option.value = station.ID;
			promptSelect.appendChild(option);
		});
		promptSelect.value = self.lastSelectedStation;
		//-----------------
		// Move button
		//-----------------
		var promptMoveBtn = document.getElementById("promptMoveBtn");
		promptMoveBtn.onclick = function () {
			self.lastSelectedStation = promptSelect.value;
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var movedTraveler = self.ShiftTraveler(traveler); 
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("MoveTravelerStart",
			{
				travelerID: movedTraveler.ID,
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
					travelerID: traveler.ID
				});
				application.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				
				closeFunction();
			}
			popup.appendChild(disintegrateBtn);
			//-------------------------
			application.popupManager.AddCustom(popup);
		}
	}
	this.Initialize = function (station) {
		var self = this;
		self.station = station;
		
		self.DOMcontainer = document.createElement("DIV");
		self.DOMcontainer.className = "queueContainer";
		self.DOMcontainer.innerHTML = self.station.name;
		
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
		itemCode.innerHTML = traveler.itemCode;
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
