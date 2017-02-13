// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 2/13/2017

var application = new Application();

// gets called once the html is loaded
function Initialize() {
	//window.oncontextmenu = function () {return false;}
	application.Initialize();
}
function Application () {
	// DOM
	this.queueArray;
	// DATA
	this.stationList = [];
	this.travelers = [];
	this.queues = {};
	// Websocket
	this.websocket;
	this.SetWindowHeight = function () {
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
	};
	
	//----------------
	// station list
	//----------------
	this.PopulateQueues = function () {
		var self = this;
		self.stationList.forEach(function (station) {
			var queue = new TravelerQueue();
			queue.DOMcontainer.innerHTML = station;
			queue.DOMcontainer.appendChild(queue.DOMelement);
			self.queueArray.appendChild(queue.DOMcontainer);
			
			self.queues[station] = queue;
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
			self.queues[traveler.station].AddTraveler(traveler);
		});
	}
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		self.SetWindowHeight();
		window.addEventListener("resize",self.SetWindowHeight,false);
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
			self.websocket = new WebSocket("ws://localhost:8080/");
			
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
						if (object.hasOwnProperty("stationList")) {
							self.stationList = object.stationList;
							self.PopulateQueues();
						}
						if (object.hasOwnProperty("travelers")) {
							self.travelers = [];
							object.travelers.forEach(function (obj) {
								var traveler = new Traveler(obj);
								self.travelers.push(traveler);
							});
							self.HandleTravelersChanged();
						}
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved binary data
				}
			};
			// websocket is closed.
			self.websocket.onclose = function() {
			 
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
	}
}
function TravelerQueue() {
	this.DOMcontainer;
	this.DOMelement;
	this.travelers;
	
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
			DOMqueueItem.className = "queue__item";
			DOMqueueItem.innerHTML = traveler.itemCode;
			DOMqueueItem.onmousedown = function () {
				self.PromptAction(traveler);
			}
			self.DOMelement.appendChild(DOMqueueItem);
		});
	}
	this.PromptAction = function (traveler) {
		var self = this;
		var blackout = document.getElementById("blackout");
		blackout.style.visibility = "visible";
		var promptBox = document.getElementById("promptBox");
		// clear the promptBox
		while (promptBox.hasChildNodes()) {
			promptBox.removeChild(promptBox.lastChild);
		}
		//-----------------
		// Send to...
		//-----------------
		promptBox.innerHTML = "Send to...";
		var sendTo = document.createElement("SELECT");
		
		sendTo.className = "dark stdMargin halfEM";
		// add the station options
		application.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station;
			option.value = station;
			sendTo.appendChild(option);
		});
		// exectues when user selects an option
		sendTo.onchange =  function () {
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var completedTraveler = self.ShiftTraveler(traveler); 
			var message = {
				completed: completedTraveler.ID,
				destination: sendTo.value,
				time: 0,
				quantity: completedTraveler.quantity
			};
			application.websocket.send(JSON.stringify(message));
			
			blackout.style.visibility = "hidden";
		}
		sendTo.selectedIndex = -1;
		promptBox.appendChild(sendTo);
		//-----------------
		// Cancel button
		//-----------------
		var cancel = document.createElement("DIV");
		cancel.className = "dark button";
		cancel.innerHTML = "Cancel";
		cancel.onclick = function () {
			blackout.style.visibility = "hidden";
		}
		promptBox.appendChild(cancel);
		
	}
	this.Initialize = function () {
		var self = this;
		self.DOMcontainer = document.createElement("DIV");
		self.DOMcontainer.className = "queueContainer";
		self.DOMelement = document.createElement("DIV");
		self.DOMelement.className = "queue";
		
		self.DOMcontainer.appendChild(self.DOMelement);
		self.travelers = [];
	}
	this.Shutdown = function () {
		this.DOMelement.parent.removeChild(this.DOMelement);
		this.travelers = [];
	}
	this.Initialize();
}
function TravelerView() {
	// properties
	this.traveler;
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
		// hide complete button
		if (self.btnComplete != undefined) self.btnComplete.style.visibility = "hidden";
		//---------------------
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
		// show complete button
		if (self.btnComplete != undefined) self.btnComplete.style.visibility = "visible";
		//---------------------
	}
	this.ResumeTimer = function () {
		var self = this;
		// hide complete button
		if (self.btnComplete != undefined) self.btnComplete.style.visibility = "hidden";
		//---------------------
		document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		self.timerInterval = setInterval(function () {
			self.timerTime.add(1,'s');
			document.getElementById("timerTime").innerHTML = pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		},1000);
	}
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
		
		// Submitting a finished traveler
		document.getElementById("submit").onclick = function () {
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var completedTraveler = application.travelerQueue.ShiftTraveler(self.traveler); 
			var message = {
				completed: completedTraveler.ID,
				destination: document.getElementById("destList").value,
				time: self.timerTime.asMinutes(),
				quantity: Math.min(Math.round(document.getElementById("qtyMade").value),completedTraveler.quantity)
			};
			application.websocket.send(JSON.stringify(message));
			// load the next traveler
			if (application.travelerQueue.travelers.length > 0) {
				self.Load(application.travelerQueue.travelers[0]);
			} else {
				self.Clear();
			}
			// close the window
			document.getElementById("blackout").style.visibility = "hidden";
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
		self.timerStop = document.getElementById("stopTimer");
		self.timerStop.onmousedown = function () {
			self.StopTimer();
		}
	}
}
function Traveler(obj) {
	return obj;
}
function pad(num, size) {
    var s = num+"";
    while (s.length < size) s = "0" + s;
    return s;
}