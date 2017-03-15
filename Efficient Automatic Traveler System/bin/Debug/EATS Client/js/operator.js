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
	// client information
	this.lastStation;
	this.station;
	// key information
	this.stationList;
	// Websocket
	this.websocket;
	// timeouts
	this.AutoFocusTimeout;
	// barcode scanner
	this.IDbuffer = "";
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
			viewContainer.style.height = "50%";
			
			
			queueContainer.style.width = "20%";
			queueContainer.style.maxWidth = "none";
			queueContainer.style.height = "50%";
			
			
			interfaceContainer.style.width = "80%";
			interfaceContainer.style.height = "50%";
		}
		// Small screens
		document.body.style.fontSize = Math.min(10,Math.round(window.innerWidth/72)) + "px";
	};
	this.FocusOnSearch = function () {
		document.getElementById("travelerSearchBox").focus();
	}
	//----------------
	// station list
	//----------------
	this.PopulateStations = function () {
		var self = this;
		// remove old
		var select = document.getElementById("stationList")
		while (select.firstChild) {
			select.removeChild(select.firstChild);
		}
		self.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station.name;
			option.value = station.ID;
			select.appendChild(option);
		});
		select.onchange = function () {
			var stationID = parseInt(this.value);
			self.websocket.send('{"station":' + stationID + '}');
			self.stationList.some(function (station) {
				if (station.ID == stationID) {
					self.station = JSON.parse(JSON.stringify(station));
					return true;
				}
			});
		}
		select.onchange();
	}
	// Executes when the connection closes
	this.ConnectionClosed = function () {
		var self = this;
		var blackout = document.getElementById("blackout");
		blackout.style.visibility = "visible";
		while (blackout.firstChild) {
			blackout.removeChild(blackout.firstChild);
		}
		blackout.style.fontSize = "3em";
		blackout.style.color = "black";
		blackout.style.backgroundColor = "rgba(255,255,255,0.8)";
		blackout.style.textShadow = "0px 0px 8px yellow";
		blackout.innerHTML = "You are not connected to the server;<br> either refresh the page, or inform your supervisor";
	}
	this.Popup = function (message) {
		document.getElementById("blackout").style.visibility = "visible";
		document.getElementById("confirm").style.display = "flex";
		document.getElementById("confirmMessage").innerHTML = message;
	}
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		self.SetWindow();
		window.addEventListener("resize",self.SetWindow,false);
		
		window.onmousedown = function() {
			clearTimeout(application.AutoFocusTimeout);
			application.AutoFocusTimeout = setTimeout(application.FocusOnSearch,5000);
		}
		window.addEventListener("keydown",function (evt) {
			application.FocusOnSearch();
		});
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
						if (object.hasOwnProperty("stationList")) {
							self.stationList = object.stationList;
							self.PopulateStations();
						} else if (object.hasOwnProperty("travelers") && object.hasOwnProperty("mirror")) {
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
									self.Popup("A new traveler has been loaded automatically");
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
						} else if (object.hasOwnProperty("confirmation")) {
							self.Popup(object.confirmation);
						}
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved blob data
				}
			};
			// websocket is closed.
			self.websocket.onclose = function() {
				self.ConnectionClosed();
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
		// Confirm box "OK" button
		document.getElementById("confirmBtn").onclick = function () {
			document.getElementById("confirm").style.display = "none";
			document.getElementById("blackout").style.visibility = "hidden";
		}
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
			
			DOMqueueItem.innerHTML = pad(traveler.ID,6);
			var itemCode = document.createElement("SPAN");
			itemCode.className = "queue_item__desc";
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
	// Timer
	this.timerStart;
	this.timerStop;
	this.timerTime;
	this.timerInterval;
	
	this.StartTimer = function () {
		var self = this;
		self.StopTimer();
		self.timerTime = new moment.duration("00:00");
		document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		self.timerInterval = setInterval(function () {
			self.timerTime.add(1,'s');
			document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		},1000);
	}
	this.StopTimer = function () {
		var self = this;
		clearInterval(self.timerInterval);
	}
	this.ResumeTimer = function () {
		var self = this;
		//---------------------
		document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		self.timerInterval = setInterval(function () {
			self.timerTime.add(1,'s');
			document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		},1000);
	}
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
		// hide the item area
		document.getElementById("itemQueue").style.display = "none";
		document.getElementById("completeItemBtn").innerHTML = "Complete item";
		document.getElementById("scrapItemBtn").innerHTML = "Scrap item";
		self.ResetSliders();
	}
	this.DisableUI = function () {
		document.getElementById("completeItemBtn").className = "dark button twoEM disabled";
		document.getElementById("scrapItemBtn").className = "dark button twoEM disabled";
		//document.getElementById("submitTravelerBtn").className = "dark button twoEM disabled";
	}
	this.EnableUI = function () {
		document.getElementById("completeItemBtn").className = "dark button twoEM";
		document.getElementById("scrapItemBtn").className = "dark button twoEM";
		//document.getElementById("submitTravelerBtn").className = "dark button twoEM";
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
		self.StartTimer();
	}
	this.LoadItem = function (traveler, item) {
		var self = this;
		self.traveler = traveler;
		self.item = item;
		// enable the buttons
		self.EnableUI();
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
		
		if (!self.traveler)  {
			return;
		}
		// store the last state (this current state)
		self.lastTravelerID = self.traveler.ID;
		if (application.station) {
			application.lastStation = JSON.parse(JSON.stringify(application.station));
		}
		
		
		if (application.station.canCreateItems) {
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
				if (item.station == application.station.ID && !Contains(item.history,[{prop:"type",value:0},{prop:"station",value:item.station}])) {
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
			application.FocusOnSearch();
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
			application.FocusOnSearch();
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
			application.FocusOnSearch();
		}
		// cancel submission
		document.getElementById("cancel").onclick = function () {
			document.getElementById("blackout").style.visibility = "hidden";
			self.ResumeTimer();
		}
		//----------------
		// timer ui
		//----------------
		self.timerStart = document.getElementById("startTimer");
		self.timerStart.onmousedown = function () {
			self.StartTimer();
		}
		/* self.timerStop = document.getElementById("stopTimer");
		
		self.timerStop.onmousedown = function () {
			self.StopTimer();
		} */
		// Traveler Search
		document.getElementById("travelerSearch").onsubmit = function () {
			var search = document.getElementById("travelerSearchBox").value;
			// try to parse the search string
			var travelerID;
			var itemID;
			// as traveler + item
			var success = false;
			var array = search.split('-');

			travelerID = parseInt(array[0],10);
			itemID = parseInt(array[1],10);
			if (!isNaN(travelerID)) {
				var traveler = application.travelerQueue.FindTraveler(travelerID);
				if (traveler) {
					application.travelerQueue.SelectTraveler(traveler);
					var item = traveler.FindItem(itemID);
					if (item && item.station == application.station.ID) {
						self.LoadItem(traveler,application.travelerQueue.FindItem(travelerID,itemID));
					} else if (!isNaN(itemID)) {
						application.Popup("Item [" + itemID + "] is not at your station");
					}
				} else {
					application.Popup("Traveler [" + pad(travelerID,6) + "] isn't at your station :(");
				}
			} else {
				application.Popup("Invalid traveler ID :(");
			}
			document.getElementById("travelerSearchBox").value = "";
			return false;
		};
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
