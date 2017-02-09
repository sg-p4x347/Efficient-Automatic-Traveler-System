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
	
	// key information
	this.stationList;
	// Websocket
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
	
	//----------------
	// station list
	//----------------
	this.PopulateStations = function () {
		var self = this;
		PopulateStations(self.stationList,document.getElementById("stationList"), function ()  {
			self.websocket.send('{"station":"' + this.innerHTML + '"}');
			var dropdown = document.getElementById("stationName");
			dropdown.innerHTML = this.innerHTML;
		});
	}
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
							self.PopulateStations(object.stationList);
						} else if (object.hasOwnProperty("travelers")) {
							
							self.travelerQueue.Clear();
							object.travelers.forEach(function (obj) {
								var traveler = new Traveler(obj);
								self.travelerQueue.AddTraveler(traveler);
							});
							if (self.travelerQueue.travelers[0]) self.travelerView.Load(self.travelerQueue.travelers[0]);
							
						}
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
		
		/* if (traveler.completed) {
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
		} else { */
		// configure complete button
		self.btnComplete.innerHTML = "Complete";
		self.btnComplete.className = "dark button fourEM";
		self.btnComplete.onclick = function () {
			document.getElementById("blackout").style.visibility = "visible";
			PopulateStations(application.stationList,document.getElementById("destList"),function () {
				self.destination = this.innerHTML;
				var destName = document.getElementById("destName");
				destName.innerHTML = this.innerHTML;
			});
			//application.completedList.UnshiftTraveler(complete);
		}
		//}
		
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
			var completedTraveler = self.traveler; //application.travelerQueue.ShiftTraveler(self.traveler);
			var message = {
				completed: completedTraveler.ID,
				destination: self.destination,
				time: self.timerTime.asMinutes()
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
	obj.completed = false;
	return obj;
	/* // Common properties
	this.ID;
	this.itemCode;
	this.quantity;
	this.description;
	
	this.Initialize = function (obj) {
		var self = this;
		
		// Common properties
		self.ID = obj.ID;
		self.itemCode =  obj.itemCode;
		self.quantity = obj.quantity;
		self.description = obj.description;
	}
	this.Initialize(obj); */
}
function PopulateStations (stations,DOMparent,callback) {
	var self = this;
	stations.forEach(function (station) {
		var li = document.createElement("DIV");
		li.innerHTML = station;
		li.className = "dropdown__item";
		li.onmousedown = callback;
		DOMparent.appendChild(li);
	});
}
function pad(num, size) {
    var s = num+"";
    while (s.length < size) s = "0" + s;
    return s;
}