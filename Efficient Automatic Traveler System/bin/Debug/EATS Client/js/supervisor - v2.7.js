// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 7/26/2017

var supervisor = new Supervisor();

// gets called once the html is loaded
function Initialize() {
	//window.oncontextmenu = function () {return false;}
	supervisor.Initialize();
}
function Supervisor () {
	Client.call(this);
	this.queues = [];
	// updates the queues with the current travelers
	this.HandleTravelersChanged = function (stations) {
		var self = this;
	
		
		for (var station in stations) {
			stations[station].travelers.forEach(function (traveler) {
				var copy = new Traveler(JSON.parse(JSON.stringify(traveler)));
				copy.stationQueue = station;
				copy.queueIndex = self.queues[station].travelers.length;
				self.queues[station].AddTraveler(copy);
			});
			self.queues[station].RePaint();
		};
		// add all the travelers back
		/* self.travelers.forEach(function (traveler) {
			for (var station in traveler.stations) {
				if (self.view.viewState == "InProcess" && (traveler.stations[station].qtyPending > 0 && station != "Finished" && station != "Start" && station != "Scrapped") || (traveler.state == "PreProcess" && self.view.viewState == "PreProcess") || (self.view.viewState == "PostProcess" && (station == "Finished" || station == "Scrapped"))) {
					// QTY pending is sent based on the starting station for the traveler from the Export function on Traveler.cs
					var copy = new Traveler(JSON.parse(JSON.stringify(traveler)));
					copy.stationQueue = station;
					copy.queueIndex = self.queues[station].travelers.length;
					self.queues[station].AddTraveler(copy);
				}
				self.queues[station].RePaint();
			}
		}); */
		// update summary, if open
		if (self.popupManager.Exists("summaryPopup")) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("CreateSummary",{
				sort:"Active"
			});
			
			//-----------------------------------------------
		}
	}
	
}
function TravelerQueue() {
	
	this.queue;
	this.travelers;
	// maintain scroll position
	this.scrollPos;
	
	this.Clear = function () {
		this.travelers = [];
		ClearChildren(this.queue);
	}
	this.AddTraveler = function (traveler) {
		this.travelers.push(traveler);
		this.queue.appendChild(traveler.Render());
	}
	this.RemoveTraveler = function (ID) {
		var self = this;
		for (var i = 0; i < self.travelers.length; i++) {
			if (self.travelers[i].id == ID) {
				self.travelers.splice(i,1);
				self.queue.remove(document.getElementById(ID));
			}
		}
	}
	
	/* this.RePaint = function () {
		var self = this;
		if (self.travelers.length > 0) {
			self.DOMcontainer.style.display = "flex";
			// clear old DOM objects
			while (self.DOMelement.hasChildNodes()) {
				self.DOMelement.removeChild(self.DOMelement.lastChild);
			}
			var totalQty = 0;
			var totalLabor = 0;
			// create and add the new DOM objects
			self.travelers.forEach(function (traveler) {
				var DOMqueueItem = traveler.CreateQueueItem(self.station.name);
				DOMqueueItem.onclick = function () {
					//----------INTERFACE CALL-----------------------
					var message = new InterfaceCall("LoadTravelerAt",
					{
						travelerID: traveler.ID,
						station: self.station.name
					});
					//-----------------------------------------------
				}
				DOMqueueItem.ontouchmove = function (event) {
					application.BeginDrag(traveler,self,event);
				}
				
				//self.PromptAction(traveler);
				self.DOMelement.appendChild(DOMqueueItem);
				totalQty += traveler.quantity;
				totalLabor += traveler.totalLabor;
			});
			self.totalQtyElem.innerHTML = totalQty;
			self.totalLaborElem.innerHTML = totalLabor.toFixed(1);
			if (self.scrollPos) {
				self.DOMelement.scrollTop = self.scrollPos;
			}
		} else {
			self.DOMcontainer.style.display = "none";
		}
	} */
	this.Initialize = function () {
		var self = this;
	
		
		self.queue = document.createElement("DIV");
		self.queue.className = "queue";
		self.queue.onscroll = function () {
			self.scrollPos = this.scrollTop;
		}
		self.DOMcontainer.appendChild(self.queue);
		self.travelers = [];
	}
	this.Shutdown = function () {
		this.DOMelement.parent.removeChild(this.DOMelement);
		this.travelers = [];
	}
	this.Initialize(station);
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
	}
}