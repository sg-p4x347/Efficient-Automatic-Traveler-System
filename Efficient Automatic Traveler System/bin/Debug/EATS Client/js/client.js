// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 7/26/2017

var client = new Client();

// gets called once the html is loaded
function Initialize() {
	//window.oncontextmenu = function () {return false;}
	client.Initialize();
}
// traveler object factory
function Traveler(data) {
	data.Render = function () {
		
		return HTML(data.html);
		/* var container = document.createElement("DIV");
		container.classList.add("list--horizontal");
		container.classList.add("queue__item");
		container.classList.add("align-items-center");
		container.id = data.id; */
		
	}
	return data;
}
function Client () {
	// DATA
	this.labelTypes = [];
	this.stationList = [];
	this.travelers = [];
	this.queues = {};
	this.view = {
		filterState: true,
		filterType: true,
		filterLocalState: false,
		viewState:undefined,
		viewType:undefined
	}
	// MISC
	this.lastSelectedStation;
	// IO
	this.input = new Input();
	this.selection = {
		lastQueue: undefined,
		lastTraveler: undefined
	};
	// Queue scroll positions (by element id)
	this.scrollPos = {};
	// Websocket
	this.websocket;
	this.SetWindow = function () {
		// Small screens
		var fontsize = Math.max(8,Math.min(20,Math.round(window.innerWidth/24)));
		document.body.style.fontSize = fontsize + "px";
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
		
		
		if (window.innerHeight / window.innerWidth < (3/4)) {
			// landscape layout
		} else {
			// portrait layout

		}
		
	};
	//=====================================================
	// Server/Client interface
	//=====================================================
	this.ControlPanel = function (controlPanel) {
		var self = this;
		self.popupManager.ControlPanel(controlPanel,document.getElementById(controlPanel.ID));
	}
	this.Form = function (params) {
		var self = this;
		//self.popupManager.CloseAll();
		self.StopAutofocus();
		self.popupManager.Form(params.form, function (filledForm) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall(params.callback,
			{
				form:filledForm,
				parameters:params.parameters
			});
			//-----------------------------------------------
			self.StartAutofocus();
		}, params.form.id);
	}
	this.LoginPopup = function (info) {
		var self = this;
		// station list
		//if (self.stationList.length > 0) self.InitStations(self.stationList);
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
			
			//-----------------------------------------------
			self.LoginPopup();
			
		}
		self.userID = document.getElementById("uidBox").value;
		logoutBtn.innerHTML = "Logout " + name;
		
	}
		this.CloseAll = function () {
		this.popupManager.CloseAll();
	}
	this.Info = function (message) {
		this.popupManager.Info(message);
	}
	this.Updating = function (text) {
		document.getElementById("updatingPopup").innerHTML = (text && text.text ? text.text : "");
		this.popupManager.AddSpecific("updatingPopup");
	}
	this.Redirect = function(location) {
		window.location = location;
	}
	this.Evaluate = function (javascript) {
		window.eval(javascript);
	}
	this.InterfaceOpen = function () {
		// configure the default view settings with the server
		//document.getElementById("viewForm").onchange();
	}
	this.ClearSearch = function() {
		document.getElementById("searchBox").value = "";
	}
	
	//=====================================================
	// Utility
	//=====================================================
	
	// Multi-Select
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
	// updates the queues with the current travelers
	this.HandleTravelersChanged = function (message) {
		var self = this;
		if (message.mirror) {
			/* self.travelers = [];
			message.travelers.forEach(function (obj) {
				var traveler = new Traveler(obj);
				self.travelers.push(traveler);
			}); */
		} else {
			/* message.travelers.forEach(function (obj) {
				self.travelers.forEach(function (traveler, index) {
					if (traveler.ID == obj.ID) {
						self.travelers[index] = new Traveler(obj);
					}
				});
			}); */
		}
		// clear the queues
		for (var station in self.queues) {
			self.queues[station].Clear();
		}
		for (var station in message.stations) {
			message.stations[station].travelers.forEach(function (traveler) {
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
	
	this.InitLabelTypes = function (labelTypes) {
		this.labelTypes = labelTypes;
	}

	this.FocusOnSearch = function () {
		document.getElementById("searchBox").value = "";
		document.getElementById("searchBox").focus();
	}
	this.StartAutofocus = function () {
		//window.addEventListener("keydown",this.Autofocus);
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

		document.getElementById("searchForm").onsubmit = function () {
			if (searchBox.value.length > 0) {
				new InterfaceCall("SearchSubmitted",{
				searchPhrase: searchBox.value});
			}
			return false;
		}
		
		//----------------
		// Options
		//----------------
		document.getElementById("optionsBtn").onclick = function () {
			new InterfaceCall("OptionsMenu");
			
		}
		//----------------
		// help
		//----------------
		document.getElementById("helpBtn").onclick = function () {
			new InterfaceCall("Help");
		}
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
							if (object.hasOwnProperty("method")) {
								if (self.hasOwnProperty(object.method) && object.hasOwnProperty("parameters")) {
									if (object.parameters != "") {
										self[object.method](object.parameters);
									} else {
										self[object.method]();
									}
									/* var target;
									if (object.method == "InlineCall") {
										// The server is invoking a callback
										var index = self.interfaceCalls.indexOf(parseInt(object.callID));
										target = self.interfaceCalls[index].callback;
										self.interfaceCalls.splice(index,1);
									} else {
										//target = self[object.method];
										// The server is invoking a client method */
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
				self.popupManager.Error("You are not connected to the server;<br> 1.) Refresh the page<br>2.) If problem persists, Inform an EATS administrator");
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
	}
}
function Queue() {
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