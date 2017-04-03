// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 1/12/17

var application = new Application();

// gets called once the html is loaded
function Initialize() {
	//window.oncontextmenu = function () {return false;}
	application.Initialize();
}
function Application () {
	// DOM
	this.travelerQueue;
	this.travelerView;
	this.completedList;
	this.popupManager;
	// client information
	this.lastStation;
	this.station;
	this.userID;
	// key information
	this.stationList = [];
	// Websocket
	this.websocket;
	// timeouts
	this.IOScheckTimeout;
	// barcode scanner
	this.IDbuffer = "";
	// timers
	this.partTimer
	this.stationTimer
	// update and render
	this.Render = function () {
		
	};
	this.Update = function (elapsed) {
		
	};
	this.SetWindow = function () {
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
		
		var viewContainer = document.getElementById("viewContainer");
		var queueContainer = document.getElementById("queueContainer");
		var interfaceContainer = document.getElementById("interfaceContainer");
		
		if (window.innerHeight / window.innerWidth < (3/4)) {
			// landscape layout
			viewContainer.style.width = "50%";
			viewContainer.style.height = "100%";
			
			
			queueContainer.style.width = "10%";
			queueContainer.style.height = "100%";
			
			
			interfaceContainer.style.width = "40%";
			interfaceContainer.style.height = "100%";
		} else {
			// portrait layout
			
			viewContainer.style.width = "100%";
			viewContainer.style.height = "40%";
			
			
			queueContainer.style.width = "25%";
			queueContainer.style.maxWidth = "none";
			queueContainer.style.height = "60%";
			
			
			interfaceContainer.style.width = "75%";
			interfaceContainer.style.height = "60%";
		}
		// Small screens
		document.body.style.fontSize = Math.min(10,Math.round(window.innerWidth/72)) + "px";
	};
	this.FocusOnSearch = function () {
		document.getElementById("travelerSearchBox").value = "";
		document.getElementById("travelerSearchBox").focus();
	}
	//----------------
	// station list
	//----------------
	this.InitStations = function (stationList) {
		var self = this;
		self.stationList = stationList
		// remove old
		var select = document.getElementById("stationList")
		while (select.firstChild) {
			select.removeChild(select.firstChild);
		}
		self.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station.name;
			option.value = station.name;
			select.appendChild(option);
		});
		select.onchange = function () {
			var stationName = this.value;
			//self.websocket.send('{"station":' + stationID + '}');
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("SetStation",
			{
				station: stationName
			},"This");
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.stationList.some(function (station) {
				if (station.name == stationName) {
					self.station = JSON.parse(JSON.stringify(station));
					return true;
				}
			});
		}
		select.onchange();
	}
	this.Info = function (message) {
		this.popupManager.Info(message);
	}
	this.LoginPopup = function (info) {
		var self = this;
		// station list
		if (self.stationList.length > 0) self.InitStations(self.stationList);
		// logout button text
		document.getElementById("logoutBtn").innerHTML = "Logout";
		// popup stuff
		self.popupManager.CloseAll();
		self.StopAutofocus();
		var loginPopup = document.getElementById("loginPopup").cloneNode(true);
		
		self.popupManager.AddCustom(loginPopup,true);
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
					UID: document.getElementById("uidBox").value
				},"This");
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
				self.popupManager.Close(loginPopup);
			}
			return false;
		}
	}
	this.LoginSuccess = function (userName) {
		var self = this;
		self.popupManager.Unlock();
		self.popupManager.CloseAll();
		self.StartAutofocus();
		
		// LOG OUT BUTTON
		var logoutBtn = document.getElementById("logoutBtn");
		logoutBtn.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("Logout",
			{
				UID: document.getElementById("uidBox").value
			},"This");
			self.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.LoginPopup();
			
		}
		self.userID = document.getElementById("uidBox").value;
		logoutBtn.innerHTML = "Logout " + userName;
		// start the station timer
		self.stationTimer.Start();
		
	}
	/* this.AddUID = function (question) {
		var self = this;
		self.popupManager.Confirm(question,function () {
			if (document.getElementById("uidBox").value != "") {
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("AddUID",
				{
					UID: document.getElementById("uidBox").value
				},"This");
				self.websocket.send(JSON.stringify(message));
				//-----------------------------------------------
			}
			self.LoginPopup();
		},function () {
			self.LoginPopup();
		});
	} */
	this.StartAutofocus = function () {
		window.addEventListener("keydown",this.Autofocus);
	}
	this.Autofocus = function (evt) {
		if (document.getElementById("travelerSearchBox") != document.activeElement)  {
			application.FocusOnSearch();
		}
		clearTimeout(application.IOScheckTimeout);
		application.IOScheckTimeout = setTimeout(function () {
			if (document.getElementById("travelerSearchBox").value.length >= 11) {
				document.getElementById("travelerSearch").onsubmit();
			}
		},500);
	}
		
	this.StopAutofocus = function () {
		window.removeEventListener("keydown",this.Autofocus);
	}
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		self.popupManager = new PopupManager(document.getElementById("blackout"));
		self.SetWindow();
		window.addEventListener("resize",self.SetWindow,false);
		
		
		/* window.addEventListener("keyup",function () {
			if (document.getElementById("travelerSearchBox").value.length == 11) {
				document.getElementById("travelerSearch").onsubmit();
			}
		}); */
		// timers
		self.partTimer = new Timer(document.getElementById("partTime"));
		self.stationTimer = new Timer(document.getElementById("stationTime"));
		//----------------
		// traveler view
		//----------------
		self.travelerView = new TravelerView();
		self.travelerView.Initialize();
		//----------------
		// traveler queue
		//----------------
		self.travelerQueue = new TravelerQueue();
		self.travelerQueue.Initialize("travelerQueue");
		//----------------
		// completed travelers
		//----------------
		self.completedList = new TravelerQueue();
		self.completedList.Initialize("completedList");
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
				self.websocket.send("OperatorClient");
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
						if (object.hasOwnProperty("travelers") && object.hasOwnProperty("mirror")) {
							if (object.mirror) {
								// The only travelers in the queue are explicitly the ones in the message
								self.travelerQueue.Clear();
								object.travelers.forEach(function (obj) {
									self.travelerQueue.AddTraveler(new Traveler(obj));
								});
							} else {
								// Only update existing travelers in the queue
								object.travelers.forEach(function (obj) {
									self.travelerQueue.UpdateTraveler(new Traveler(obj));
								});
							}
							// autoload the first traveler in the queue if just now visiting
							if ((!self.lastStation || self.station.ID != self.lastStation.ID) && self.travelerQueue.travelers[0]) {
								self.travelerQueue.SelectTraveler(self.travelerQueue.FindTraveler(self.travelerQueue.travelers[0].ID)); // this ensures that the item is selected
							} else if (!self.travelerQueue.Exists(self.travelerView.traveler)) {
								if (self.travelerQueue.travelers[0]) {
									self.travelerQueue.SelectTraveler(self.travelerQueue.FindTraveler(self.travelerQueue.travelers[0].ID)); // this ensures that the item is selected
									self.Info("A new traveler has been loaded automatically");
								} else {
									self.travelerView.Clear();
								}
							}
							// try and load the old traveler
							self.travelerQueue.travelers.forEach(function (traveler) {
								if (traveler.ID == self.travelerView.lastTravelerID) {
									self.travelerQueue.SelectTraveler(self.travelerQueue.FindTraveler(traveler.ID));
									//self.travelerView.Load(traveler);
								}
							});
						} else if (object.hasOwnProperty("method")) {
							if (self.hasOwnProperty(object.method) && object.hasOwnProperty("parameters")) {
								// The server is invoking a client method
								self[object.method](object.parameters);
							}
						}
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved blob data
				}
			};
			// websocket is closed.
			self.websocket.onclose = function() {
				self.popupManager.Error("You are not connected to the server;<br> either refresh the page, or inform your supervisor");
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
		/* // Confirm box "OK" button
		document.getElementById("confirmBtn").onclick = function () {
			document.getElementById("confirm").style.display = "none";
			document.getElementById("blackout").style.visibility = "hidden";
		} */
	}
}
function TravelerQueue() {
	this.DOMelement;
	this.travelers;
	
	this.Clear = function () {
		this.travelers = [];
		this.RePaint();
	}
	this.Exists = function (mask) {
		var self = this;
		var exists = false;
		if (mask) {
			self.travelers.some(function (traveler) {
				if (traveler.ID == mask.ID) {
					exists = true;
					return true;
				}
			});
		}
		return exists;
	}
	this.FindTraveler = function (id) {
		var t;
		this.travelers.some(function (traveler) {
			if (traveler.ID == id) {
				t = traveler;
				return true;
			}
		});
		return t;
	}
	this.FindItem = function (travelerID, itemID) {
		var self = this;
		var traveler = self.FindTraveler(travelerID);
		var itm;
		if (traveler) {
			traveler.items.some(function (item) {
				if (item.ID == itemID) {
					itm = item;
					return true;
				}
			});
		}
		return itm;
	}
	this.AddTraveler = function (traveler) {
		var self = this;
		this.travelers.push(traveler);
		this.RePaint();
		
	}
	this.UpdateTraveler = function (updated) {
		var self = this;
		var found = false;
		for (var i = 0; i < self.travelers.length; i++) {
			if (self.travelers[i].ID == updated.ID) {
				self.travelers[i] = updated;
				found = true;
				if (application.travelerView.traveler && updated.ID == application.travelerView.traveler.ID) {
					application.travelerView.Load(updated);
				}
				break;
			}
		};
		if (!found) {
			this.AddTraveler(updated);
		}
	}
	this.SelectTraveler = function (traveler) {
		var self = this;
		self.travelers.forEach(function (trav) {
			trav.selected = trav.ID == traveler.ID;
		});
		application.travelerView.Load(traveler)
		self.RePaint();
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
			
			if (traveler.selected) {
				DOMqueueItem.className = "button blueBack queue__item selected";
			} else {
				DOMqueueItem.className = "button blueBack queue__item";
			}
			
			DOMqueueItem.innerHTML = pad(traveler.ID,6) + "<br>";
			var itemCode = document.createElement("SPAN");
			itemCode.className = "queue_item__desc beige";
			itemCode.innerHTML = traveler.itemCode;
			DOMqueueItem.appendChild(itemCode);
			
			DOMqueueItem.onmousedown = function () {
				self.SelectTraveler(traveler);
			}
			self.DOMelement.appendChild(DOMqueueItem);
		});
	}
	this.Initialize = function (DOMid) {
		var self = this;
		self.DOMelement = document.getElementById(DOMid);
		self.travelers = [];
		
	}
}
function TravelerView() {
	// properties
	this.traveler;
	this.item;
	this.lastTravelerID;
	this.destination;
	// DOM
	this.DOMcontainer;
	this.btnComplete;
	// Part timer
	this.timer;
	this.Clear = function () {
		var self = this;
		delete self.traveler;
		self.traveler;
		delete self.item;
		self.item;
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		// disable the buttons (temporarily)
		self.DisableUI();
		self.ResetSliders();
		self.UpdateSubmitBtn();
		// hide the item area
		document.getElementById("itemQueue").style.display = "none";
		document.getElementById("completeItemBtn").innerHTML = "Complete item";
		document.getElementById("scrapItemBtn").innerHTML = "Scrap item";
		self.ResetSliders();
	}
	this.DisableUI = function () {
		document.getElementById("completeItemBtn").className = "dark button twoEM disabled";
		document.getElementById("scrapItemBtn").className = "dark button twoEM disabled";
	}
	this.EnableUI = function () {
		document.getElementById("completeItemBtn").className = "dark button twoEM";
		document.getElementById("scrapItemBtn").className = "dark button twoEM";
	}
	this.UpdateSubmitBtn = function () {
		if (this.traveler && this.traveler.qtyCompleted > 0) {
			document.getElementById("submitTravelerBtn").className = "dark button twoEM";
		} else {
			document.getElementById("submitTravelerBtn").className = "dark button twoEM disabled";
		}
	}
	
	this.LoadTable = function () {
		var self = this;
		// create the view header
		var viewHeader = document.createElement("DIV");
		viewHeader.className = "view__header";
		viewHeader.innerHTML = "Traveler: " + pad(self.traveler.ID,6);
		if (self.item != undefined) {
			viewHeader.innerHTML += " &#8213 " + self.item.ID;
		}
		self.DOMcontainer.appendChild(viewHeader);
		// create the table
		var DOMtable = document.createElement("TABLE");
		DOMtable.className = "view";
		
		// add the part row
		self.traveler.members.unshift({name: "Part", value: self.traveler.itemCode, qty: self.traveler.quantity});
		// add the column header
		self.traveler.members.unshift({name: "Property", value: "Value", qty: "Qty.",style:"view__row--header italics"});
		
		// all other properties are in the table body
		self.traveler.members.forEach(function (property) {
			var row = document.createElement("TR");
			if (property.hasOwnProperty("style")) row.className = property.style;
			// Property name
			var propName = document.createElement("TD");
			propName.className = "view__item";
			propName.innerHTML = property.name;
			row.appendChild(propName);
			// Property value
			var propValue = document.createElement("TD");
			propValue.className = "view__item" + (property.name == "Part" ? " " + "twoEM center red shadow" : "");
			propValue.innerHTML = property.value;
			row.appendChild(propValue);
			// Property quantity (if it has a quantity)
			var propQty = document.createElement("TD");
			if (property.qty != "" || property.name == "Part") {
				propQty.className = "view__item center green shadow";
				propQty.innerHTML = property.qty;
			} else {
				propQty.className = "view__item--null";
			}
			row.appendChild(propQty);
			// add the row to the table
			DOMtable.appendChild(row);
		});
		// remove the column header
		self.traveler.members.shift();
		self.traveler.members.shift();
		
		// add the table
		self.DOMcontainer.appendChild(DOMtable);
		// start the timer
		
		application.partTimer.CountDown(2.5);
	}
	this.AutomaticReload = function (oldT,newT) {
		if ((oldT && newT) && oldT.ID != newT.ID) {
			application.Info("A new traveler has been loaded automatically");
		}
	}
	this.LoadItem = function (traveler, item) {
		var self = this;
		self.traveler = traveler;
		self.item = item;
		// enable the buttons
		self.EnableUI();
		self.UpdateSubmitBtn();
		// clear old DOM objects
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		document.getElementById("completeItemBtn").innerHTML = "Complete item #" + self.item.ID;
		document.getElementById("scrapItemBtn").innerHTML = "Scrap item #" + self.item.ID;
		self.LoadTable();
	}
	this.Load = function (traveler) {
		var self = this;
		// initialize
		self.Clear();
		self.traveler = traveler;
		self.ResetSliders();
		self.UpdateSubmitBtn();
		if (!self.traveler)  {
			return;
		}
		// store the last state (this current state)
		self.lastTravelerID = self.traveler.ID;
		if (application.station) {
			application.lastStation = JSON.parse(JSON.stringify(application.station));
		}
		
		
		if (application.station.creates.length > 0) {
			//=================================
			// CLIENTS THAT CAN CREATE ITEMS
			//=================================
			self.item = undefined;
			self.LoadTable();
			// enable the buttons
			self.EnableUI();
			// hide the item area
			document.getElementById("itemQueue").style.display = "none";
			
			
			
		} else {
			//=================================
			// CLIENTS THAT CAN'T CREATE ITEMS
			//=================================
			
			// message
			var p = document.createElement("P");
			p.className = "fourEM";
			p.innerHTML = "Select an item to work on"
			self.DOMcontainer.appendChild(p);
			// disable the buttons (temporarily)
			self.DisableUI();
			// show the item area
			document.getElementById("itemQueue").style.display = "block";
			// create the selection for traveler items
			var select = document.getElementById("itemSelect");
			// clear old options
			while (select.hasChildNodes()) {
				select.removeChild(select.lastChild);
			}
			select.className = "dark twoEM";
			self.traveler.items.forEach(function (item) {
				// only add item if it is at this station and uncomplete
				if (item.station == application.station.name && !Contains(item.history,[{prop:"type",value:0},{prop:"station",value:item.station}])) {
					var option = document.createElement("OPTION");
					option.value = item.ID;
					option.innerHTML = item.ID;
					select.appendChild(option);
				}
			});
			select.onchange = function () {
				self.LoadItem(self.traveler,self.traveler.FindItem(select.value));
			}
			select.value = 0;
		}
		
	}
	this.ResetSliders = function () {
		var qtyMade = document.getElementById("qtyCompleted");
		var qtyScrapped = document.getElementById("qtyScrapped");
		var qtyPending = document.getElementById("qtyPending");
		qtyMade.innerHTML = this.traveler ? this.traveler.qtyCompleted : '-';
		qtyScrapped.innerHTML = this.traveler ? this.traveler.qtyScrapped : '-';
		qtyPending.innerHTML = this.traveler ? this.traveler.qtyPending : '-';
		
		
		
		this.BalanceSliders();
	}
	this.BalanceSliders = function() {
		var self = this;
		/* var qtyMade = parseInt(document.getElementById("qtyMade").value);
		var qtyScrapped = parseInt(document.getElementById("qtyScrapped").value);
		var qtyPending = parseInt(document.getElementById("qtyPending").value);
		if ((qtyMade > 0 && qtyMade < self.traveler.quantity) || (qtyScrapped > 0 && qtyScrapped < self.traveler.quantity)) {
			document.getElementById("submit").innerHTML = "Submit & Print";
		} else {
			document.getElementById("submit").innerHTML = "Submit";
		}
		document.getElementById("qtyMadePercent").style.width = ((qtyMade / self.traveler.quantity) * 100) + "%";
		document.getElementById("qtyScrappedPercent").style.width = ((qtyScrapped / self.traveler.quantity) * 100) + "%";
		document.getElementById("qtyPendingPercent").style.width = ((qtyPending / self.traveler.quantity) * 100) + "%";
		 */
	}
	this.Initialize = function () {
		var self = this;
		self.DOMcontainer = document.getElementById("viewContainer");
		self.btnComplete = document.getElementById("completeBtn");
		
		// Configure the finalize ui
		var qtyMade = document.getElementById("qtyMade");
		var qtyScrapped = document.getElementById("qtyScrapped");
		var qtyPending = document.getElementById("qtyPending");
		/* qtyMade.onchange = function () {
			this.value = Math.min(self.traveler.quantity-parseInt(qtyScrapped.value), this.value);
			this.max = self.traveler.quantity-parseInt(qtyScrapped.value);
			qtyPending.value = self.traveler.quantity - (parseInt(qtyScrapped.value) + parseInt(this.value));
			self.BalanceSliders();
		}
		qtyScrapped.onchange = function () {
			if (parseInt(qtyPending.value) == 0) {
				this.value = Math.min(self.traveler.quantity-parseInt(qtyPending.value), this.value);
				this.max = self.traveler.quantity-parseInt(qtyPending.value);
				qtyMade.value = self.traveler.quantity - (parseInt(qtyPending.value) + parseInt(this.value));
			} else {
				this.value = Math.min(self.traveler.quantity-parseInt(qtyMade.value), this.value);
				this.max = self.traveler.quantity-parseInt(qtyMade.value);
				qtyPending.value = self.traveler.quantity - (parseInt(qtyMade.value) + parseInt(this.value));
			}
			self.BalanceSliders();
		}
		qtyPending.onchange = function () {
			this.value = Math.min(self.traveler.quantity-parseInt(qtyScrapped.value), this.value);
			this.max = self.traveler.quantity-parseInt(qtyScrapped.value);
			qtyMade.value = self.traveler.quantity - (parseInt(qtyScrapped.value) + parseInt(this.value));
			self.BalanceSliders();
		} */
		// completing a finished traveler item
		document.getElementById("completeItemBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("AddTravelerEvent",
			{
				travelerID: self.traveler.ID,
				eventType: "Completed",
				time: self.timerTime.asMinutes(),
				station: document.getElementById("stationList").value,
				itemID: (self.item ? self.item.ID : "undefined")
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			if (application.station.mode == "Serial") document.getElementById("submitTravelerBtn").onclick();
			self.UpdateSubmitBtn();
		}
		// scrapping a traveler item
		document.getElementById("scrapItemBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("AddTravelerEvent",
			{
				travelerID: self.traveler.ID,
				eventType: "Scrapped",
				time: self.timerTime.asMinutes(),
				station: document.getElementById("stationList").value,
				itemID: (self.item ? self.item.ID : "undefined")
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.UpdateSubmitBtn();
		}
		// Submitting a finished traveler
		document.getElementById("submitTravelerBtn").onclick = function () {
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var completedTraveler;
			if (parseInt(qtyScrapped.value) < self.traveler.quantity && parseInt(qtyMade) < self.traveler.quantity) {
				completedTraveler = self.traveler;
			} else {
				completedTraveler = application.travelerQueue.ShiftTraveler(self.traveler);
			}
			self.lastTravelerID = completedTraveler.ID;
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("SubmitTraveler",
			{
				travelerID: completedTraveler.ID,
				station: document.getElementById("stationList").value
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			self.UpdateSubmitBtn();
		}
		/* self.timerStop = document.getElementById("stopTimer");
		
		self.timerStop.onmousedown = function () {
			self.StopTimer();
		} */
		// Traveler Search
		document.getElementById("travelerSearch").onsubmit = function () {
			self.SubmitSearch();
			return false;
		}
	}
	this.SubmitSearch = function() {
		var self = this;
		try {
		application.popupManager.CloseAll();
		var search = document.getElementById("travelerSearchBox").value;
		// try to parse the search string
		var travelerID;
		var itemID;
		// as traveler + item
		var array = search.split('-');

		travelerID = parseInt(array[0],10);
		itemID = parseInt(array[1],10);
		if (!isNaN(travelerID)) {
			var traveler = application.travelerQueue.FindTraveler(travelerID);
			if (traveler) {
				self.AutomaticReload(self.traveler,traveler);
				application.travelerQueue.SelectTraveler(traveler);
				var item = traveler.FindItem(itemID);
				if (item && item.station == application.station.name) {
					if (Contains(item.history,[{prop:"station",value:item.station},{prop:"type",value:0}])) {
						application.Info("Item [" + pad(travelerID,6) + "-" + itemID + "] has already been completed at this station :)");
					} else {
						self.LoadItem(traveler,application.travelerQueue.FindItem(travelerID,itemID));
					}
				} else if (!isNaN(itemID)) {
					application.Info("Item [" + pad(travelerID,6) + "-" + itemID + "] is not at your station;<br>It is at: " + item.station);
				}
			} else {
				application.Info("Traveler [" + pad(travelerID,6) + "] isn't at your station :(");
			}
		} else {
			application.Info("Invalid traveler ID :(");
		}
		document.getElementById("travelerSearchBox").value = "";
		} catch (exception) {
			application.Info(exception.message);
		}
	}
}


function PopulateStations (stations,DOMparent,callback) {
	var self = this
	// remove old
	while (DOMparent.firstChild) {
		DOMparent.removeChild(DOMparent.firstChild);
	}
	// add
	stations.forEach(function (station) {
		var li = document.createElement("DIV");
		li.innerHTML = station;
		li.className = "dropdown__item";
		li.onmousedown = callback;
		DOMparent.appendChild(li);
	});
}
