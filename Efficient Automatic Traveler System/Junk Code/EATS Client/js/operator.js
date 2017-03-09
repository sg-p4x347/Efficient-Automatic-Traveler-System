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
	// key information
	this.stationList;
	// Websocket
	this.websocket;
	// update and render
	this.Render = function () {
		
	};
	this.Update = function (elapsed) {
		
	};
	this.SetWindow = function () {
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
		// Small screens
		
		document.body.style.fontSize = Math.round(window.innerWidth/72) + "px";
	};
	
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
			option.innerHTML = station;
			option.value = station;
			select.appendChild(option);
		});
		select.onchange = function () {
			self.websocket.send('{"station":"' + this.value + '"}');
		}
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
						} else if (object.hasOwnProperty("travelers")) {
							self.travelerQueue.Clear();
							object.travelers.forEach(function (obj) {
								var traveler = new Traveler(obj);
								self.travelerQueue.AddTraveler(traveler);
							});
							// autoload the first traveler in the queue if just now visiting
							if ((document.getElementById("stationList").value != self.lastStation && self.travelerQueue.travelers[0])
								|| !self.travelerQueue.Exists(self.travelerView.traveler)) {
								self.travelerView.Load(self.travelerQueue.travelers[0]);
								self.lastStation = document.getElementById("stationList").value;
							}
							// try and load the old traveler
							self.travelerQueue.travelers.forEach(function (traveler) {
								if (traveler.ID == self.travelerView.lastTravelerID) {
									self.travelerView.Load(traveler);
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
		if (mask) {
			self.travelers.forEach(function (traveler) {
				if (traveler.ID == mask.ID) return true;
			});
		}
		return false;
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
			DOMqueueItem.innerHTML = pad(traveler.ID,6);
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
		if (!traveler)  {
			self.btnComplete.className = "hidden";
			return;
		}
		traveler.history.forEach(function (event) {
			if (event.type == "Merged") {
				application.Popup(event.message);
			}
		});
		
		self.ResetSliders();
		// TRAVELER ID
		document.getElementById("travelerID").innerHTML = pad(traveler.ID,6);
		
		// clear old DOM objects
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		
		// populate destination list
		application.stationList.forEach(function (station) {
			var option = document.createElement("OPTION");
			option.innerHTML = station;
			option.className = "dark button";
			option.value = station;
			destList.appendChild(option);
		});
		// create the table
		var DOMtable = document.createElement("TABLE");
		DOMtable.className = "view";
		// create the complete button
		self.btnComplete = document.getElementById("completeBtn");
		self.btnComplete.className = "dark button threeEM";
		// create and add new DOM objects
		document.getElementById("destList").value = self.traveler.nextStation;
		// configure complete button
		var travelerID = document.getElementById("travelerID");
		travelerID.innerHTML = pad(self.traveler.ID,6);
		self.btnComplete.onclick = function () {
			self.StopTimer();
			document.getElementById("blackout").style.visibility = "visible";
			document.getElementById("finalizeContainer").style.display = "flex";
		}
		
		// add the part row
		traveler.members.unshift({name: "Part", value: traveler.itemCode, qty: traveler.quantity});
		// add the column header
		traveler.members.unshift({name: "Property", value: "Value", qty: "Qty.",style:"view__row--header italics"});
		
		// all other properties are in the table body
		traveler.members.forEach(function (property) {
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
		traveler.members.shift();
		traveler.members.shift();
		
		// add the table
		self.DOMcontainer.appendChild(DOMtable);
	
		// start the timer
		self.StartTimer();
	}
	this.ResetSliders = function () {
		var qtyMade = document.getElementById("qtyMade");
		var qtyScrapped = document.getElementById("qtyScrapped");
		var qtyPending = document.getElementById("qtyPending");
		qtyMade.value = this.traveler.quantity;
		qtyScrapped.value = 0;
		qtyPending.value = 0;
		
		
		
		this.BalanceSliders();
	}
	this.BalanceSliders = function() {
		var self = this;
		var qtyMade = parseInt(document.getElementById("qtyMade").value);
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
		
	}
	this.Initialize = function () {
		var self = this;
		self.DOMcontainer = document.getElementById("viewContainer");
		self.btnComplete = document.getElementById("completeBtn");
		
		// Configure the finalize ui
		var qtyMade = document.getElementById("qtyMade");
		var qtyScrapped = document.getElementById("qtyScrapped");
		var qtyPending = document.getElementById("qtyPending");
		qtyMade.onchange = function () {
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
		}
		
		// Submitting a finished traveler
		document.getElementById("submit").onclick = function () {
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var completedTraveler;
			if (parseInt(qtyScrapped.value) < self.traveler.quantity && parseInt(qtyMade) < self.traveler.quantity) {
				completedTraveler = self.traveler;
			} else {
				completedTraveler = application.travelerQueue.ShiftTraveler(self.traveler);
			}
			self.lastTravelerID = completedTraveler.ID;
			var message = {
				completed: completedTraveler.ID,
				destination: document.getElementById("destList").value,
				time: self.timerTime.asMinutes(),
				qtyMade: Math.min(Math.round(document.getElementById("qtyMade").value),completedTraveler.quantity),
				qtyScrapped: Math.min(Math.round(document.getElementById("qtyScrapped").value),completedTraveler.quantity)
			};
			application.websocket.send(JSON.stringify(message));
			self.ResetSliders();
			// load the next traveler
			if (application.travelerQueue.travelers.length > 0) {
				// try and find the old traveler first
				var found = false;
				application.travelerQueue.travelers.forEach(function (traveler) {
					if (traveler.ID == completedTraveler.ID) {
						self.Load(traveler);
						found = true;
					}
				});
				// if not found, load the bottom traveler in the queue
				if (!found) self.Load(application.travelerQueue.travelers[0]);
				
			} else {
				self.Clear();
			}
/* 			if ((parseInt(qtyMade.value) > 0 && parseInt(qtyMade.value) < self.traveler.quantity) || (parseInt(qtyScrapped.value) > 0 && parseInt(qtyScrapped.value) < self.traveler.quantity)) {
				document.getElementById("submit").innerHTML = "Printing...";
			} */
			// close the window
			document.getElementById("blackout").style.visibility = "hidden";
			document.getElementById("finalizeContainer").style.display = "none";
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
function pad(num, size) {
    var s = num+"";
    while (s.length < size) s = "0" + s;
    return s;
}