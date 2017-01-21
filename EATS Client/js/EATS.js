// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 1/12/17

var application = new Application();

// gets called once the html is loaded
function Initialize() {
	application.Initialize();
}
function Application () {
	this.travelerQueue;
	this.travelerView;
	this.completedList;
	this.websocket;
	// update and render
	this.Render = function () {
		
	};
	this.Update = function (elapsed) {
		
	};
	this.SetWindowHeight = function () {
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
	};
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		
		self.SetWindowHeight();
		window.addEventListener("resize",self.SetWindowHeight,false);
		
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
			self.websocket = new WebSocket("ws://localhost:8080/");
			
			self.websocket.onopen = function() {
				console.log("Connection is open...");
				// Web Socket is connected, send data using send()

			};
			
			self.websocket.onmessage = function(messageEvent) {
				if (typeof messageEvent.data === "string") {
					// recieved text data
					
					// verify the integrity of the json message
					try {
						var traveler = new Traveler(JSON.parse(messageEvent.data));
						self.travelerQueue.AddTraveler(traveler);
						self.travelerView.Load(traveler);
					} catch (exception) {
						console.log("invalid JSON (" + exception + "): " + messageEvent.data);
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved blob data
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
	this.DOMelement;
	this.travelers;
	
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
		if (self.travelers.length > 0) {
			application.travelerView.Load(self.travelers[0]);
		} else {
			application.travelerView.Clear();
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
			DOMqueueItem.innerHTML = traveler.partNo;
			DOMqueueItem.onclick = function () {
				application.travelerView.Load(traveler);
				self.RePaint();
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
	// DOM
	this.DOMcontainer;
	
	this.Clear = function () {
		var self = this;
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
	}
	this.Load = function (traveler) {
		var self = this;
		self.traveler = traveler;
		self.Clear();
		// clear old DOM objects
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		// create the table
		var DOMtable = document.createElement("TABLE");
		DOMtable.className = "view";
		// create the complete (or uncomplete) button
		var btnComplete = document.createElement("DIV");
		btnComplete.className = "button";
		// create and add new DOM objects
		
		if (traveler.completed) {
			var completedRow = document.createElement("TR");
			completedRow.appendChild(document.createElement("TH"));
			var completedCell = document.createElement("TH");
			completedCell.className = "view__item--complete";
			completedCell.innerHTML = "Completed";
			completedCell.colspan = "3";
			completedRow.appendChild(completedCell);
			
			completedRow.appendChild(document.createElement("TH"));
			DOMtable.appendChild(completedRow);
			// configure uncomplete button
			btnComplete.innerHTML = "Un-Complete";
			btnComplete.onclick = function () {
				var unComplete = application.completedList.ShiftTraveler(traveler);
				unComplete.completed = false;
				application.travelerQueue.UnshiftTraveler(unComplete);
			}
		} else {
			// configure complete button
			btnComplete.innerHTML = "Complete";
			btnComplete.onclick = function () {
				var complete = application.travelerQueue.ShiftTraveler(traveler);
				complete.completed = true;
				application.completedList.UnshiftTraveler(complete);
			}
		}
		
		// header
		var headerRow = document.createElement("TR");
		// traveler ID
		var ID = document.createElement("TH");
		ID.className = "view__headerItem shadow";
		ID.innerHTML = traveler.ID;
		headerRow.appendChild(ID);
		// Part number
		var partNo = document.createElement("TH");
		partNo.className = "view__headerItem red shadow";
		partNo.innerHTML = traveler.partNo;
		headerRow.appendChild(partNo);
		// Quantity
		var quantity = document.createElement("TH");
		quantity.className = "view__headerItem shadow";
		quantity.innerHTML = traveler.quantity;
		headerRow.appendChild(quantity);
		// add the header row to the table
		DOMtable.appendChild(headerRow);
		// all other properties are in the table body
		var properties = Object.keys(traveler);
		properties.forEach(function (property) {
			if (property.substr(-3,3) != "Qty" && ["ID","partNo","quantity"].indexOf(property) == -1) {
				var row = document.createElement("TR");
				// Property name
				var propName = document.createElement("TD");
				propName.className = "view__item";
				propName.innerHTML = property;
				row.appendChild(propName);
				// Property value
				var propValue = document.createElement("TD");
				propValue.className = "view__item";
				propValue.innerHTML = traveler[property];
				row.appendChild(propValue);
				// Property quantity (if it has a quantity)
				var propQty = document.createElement("TD");
				if (traveler.hasOwnProperty(property + "Qty")) {
					propQty.className = "view__item center";
					propQty.innerHTML = traveler[property + "Qty"];
				} else {
					propQty.className = "view__item--null";
				}
				row.appendChild(propQty);
				// add the row to the table
				DOMtable.appendChild(row);
			}
		});
		// add the table
		self.DOMcontainer.appendChild(DOMtable);
		// add the complete button
		self.DOMcontainer.appendChild(btnComplete);
	}
	this.Initialize = function () {
		var self = this;
		self.DOMcontainer = document.getElementById("viewContainer");
	}
}
function Traveler(obj) {
	obj.completed = false;
	return obj;
	/* // Common properties
	this.ID;
	this.partNo;
	this.quantity;
	this.description;
	
	this.Initialize = function (obj) {
		var self = this;
		
		// Common properties
		self.ID = obj.ID;
		self.partNo =  obj.partNo;
		self.quantity = obj.quantity;
		self.description = obj.description;
	}
	this.Initialize(obj); */
}