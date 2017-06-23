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
	this.type = "operator";
	// DOM
	this.travelerQueue;
	this.inProcessQueue;
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
	// Queue scroll positions (by element id)
	this.scrollPos = {};

	// update and render
	this.view = {
		viewState:"InProcess"
	}
	this.SetWindow = function () {
		// fit the body to the screen resolution
		document.body.style.height = window.innerHeight + "px";
		
		var viewContainer = document.getElementById("viewContainer");
		var preProcessQueueContainer = document.getElementById("preProcessQueueContainer");
		var inProcessQueueContainer = document.getElementById("inProcessQueueContainer");
		var interfaceContainer = document.getElementById("interfaceContainer");
		
		if (window.innerHeight / window.innerWidth < (3/4)) {
			// landscape layout
			viewContainer.style.width = "45%";
			viewContainer.style.height = "100%";
			
			
			preProcessQueueContainer.style.width = "15%";
			preProcessQueueContainer.style.height = "100%";
			
			inProcessQueueContainer.style.width = "10%";
			inProcessQueueContainer.style.height = "100%";
			
			interfaceContainer.style.width = "30%";
			interfaceContainer.style.height = "100%";
		} else {
			// portrait layout
			
			viewContainer.style.width = "80%";
			viewContainer.style.height = "40%";
			
			
			preProcessQueueContainer.style.width = "20%";
			preProcessQueueContainer.style.maxWidth = "none";
			preProcessQueueContainer.style.height = "40%";
			
			inProcessQueueContainer.style.width = "15%";
			inProcessQueueContainer.style.maxWidth = "none";
			inProcessQueueContainer.style.height = "60%";
			
			interfaceContainer.style.width = "85%";
			interfaceContainer.style.height = "60%";
		}
		// Small screens
		var fontsize = Math.max(8,Math.min(10,Math.round(window.innerWidth/24)));
		document.body.style.fontSize = fontsize + "px";
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
		var loginPopup = document.getElementById("loginPopup");//.cloneNode(true);
		
		self.popupManager.AddSpecific("loginPopup");
		self.popupManager.Lock(loginPopup);
		// Extra info
		document.getElementById("loginInfo").innerHTML = (info ? info : "");
		// login submit
		document.getElementById("loginBtn").onclick = function (evt) {
			evt.preventDefault();
			if (document.getElementById("uidBox").value != "") {
				self.SetStation(document.getElementById("stationList").value);
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("Login",
				{
					UID: document.getElementById("uidBox").value,
					PWD: document.getElementById("pwdBox").value,
					station: document.getElementById("stationList").value,
				},"This");
				
				//-----------------------------------------------
				self.popupManager.Close(loginPopup);
			}
			return false;
		}
	}
	this.LoginSuccess = function (data) {
		var self = this;
		self.popupManager.Unlock();
		self.popupManager.CloseAll();
		self.StartAutofocus();
		document.getElementById("logoutBtn").className = "dark button twoEM";
		// LOG OUT BUTTON
		var logoutBtn = document.getElementById("logoutBtn");
		logoutBtn.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("Logout",{},"This");
			
			//-----------------------------------------------
			
			self.LoginPopup();
			
		}
		self.userID = document.getElementById("uidBox").value;
		logoutBtn.innerHTML = "Logout " + data.user;
		
		// set the station
		self.SetStation(data.station);
		// window title
		document.getElementById("windowTitle").innerHTML = self.station.name;
		// start the station timer
		//self.stationTimer.Start();
		
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
	this.SetStation = function (stationName) {
		var self = this;
		/* //----------INTERFACE CALL-----------------------
		var message = new InterfaceCall("SetStation",
		{
			station: stationName
		},"This");
		
		//----------------------------------------------- */
		document.getElementById("stationName").innerHTML = stationName;
		self.stationList.some(function (station) {
			if (station.name == stationName) {
				self.station = JSON.parse(JSON.stringify(station));
				return true;
			}
		});
	}
	this.ControlPanel = function (controlPanel) {
		var self = this;
		self.popupManager.ControlPanel(controlPanel,document.getElementById(controlPanel.ID));
		if (controlPanel.ID in self.scrollPos) {
			// reload scroll position
			document.getElementById(controlPanel.ID).scrollTop = self.scrollPos[controlPanel.ID];
		} else {
			// add this id to the list of scroll positions
			document.getElementById(controlPanel.ID).onscroll = function () {
				self.scrollPos[controlPanel.ID] = this.scrollTop;
			}
		}
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
	this.Redirect = function(location) {
		//window.location = location;
		var win = window.open(location,'_blank');
		win.focus();
	}
	this.PrintLabelPopup = function (params) {
		var self = this;
		self.popupManager.AddSpecific("labelPopup");
		var labelSelect = document.getElementById("labelSelect");
		ClearChildren(labelSelect);
		params.labelTypes.forEach(function (type) {
			var option = document.createElement("OPTION");
			option.value = type;
			option.innerHTML = type;
			labelSelect.appendChild(option);
		});
		
		var itemSelect = document.getElementById("itemSelection");
		ClearChildren(itemSelect);
		params.traveler.items.forEach(function (item) {
			var option = document.createElement("OPTION");
			option.value = item.ID;
			option.innerHTML = item.ID;
			itemSelect.appendChild(option);
		});
		
		document.getElementById("printLabelBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("PrintLabel",{
				travelerID: params.traveler.ID,
				itemID: document.getElementById("itemSelection").value,
				labelType: document.getElementById("labelSelect").value,
				quantity: 1
			});
			
			//-----------------------------------------------
		}
	}
	// Loads the traveler GUI
	this.LoadTravelerJSON = function (traveler) {
		var popup = this.popupManager.AddJSONviewer(traveler,"Traveler")
		popup.DOMcontainer.className += " twoEM";
		//this.JSONviewer = new JSONviewer(traveler,"Traveler");
	}
	this.LoadTraveler = function (traveler) {
		//this.travelerView.Load(traveler);
		//this.travelerQueue.SelectTraveler(traveler);
	}
	this.LoadItem = function (params) {
		//this.travelerView.LoadItem(params.traveler,params.item,params.sequenceID);
		//this.travelerQueue.SelectTraveler(params.traveler);
	}
	this.StartAutofocus = function () {
		window.addEventListener("keydown",this.Autofocus);
	}
	this.StopAutofocus = function () {
		window.removeEventListener("keydown",this.Autofocus);
	}
	this.CloseAll = function () {
		this.popupManager.CloseAll();
	}
	// displays a station checklist
	this.checklistSubmits = 0;
	this.DisplayChecklist = function (list) {
		var self = this;
		self.partTimer.Stop();
		self.popupManager.CloseAll(true);
		var popup = self.popupManager.CreatePopup();
		popup.style.position = "fixed";
		popup.style.right = "16px";
		popup.style.bottom = "16px";
		var info = self.popupManager.CreateP("");
		info.className = "red";
		popup.appendChild(info);
		list.forEach(function (item) {
			var check = self.popupManager.CreateCheckItem(item);
			popup.appendChild(check);
		});
		var submit = self.popupManager.CreateButton("Submit");
		var submitMessages = [
			"Please verify all items",
			"PLEASE verify all items",
			"PLEASE verify ALL of the items",
			"You really should verify them",
			"This isn't difficult",
			"Don't make this difficult",
			"Ok, now you are just doing this for fun",
			"One more time and your supervisors will be notified",
			"You don't believe me?",
			"STOP",
			"Im serious, get back to work"
		];
		submit.onclick = function () {
			self.checklistSubmits++;
			var nodes = popup.getElementsByTagName("INPUT");
			var allSelected = true;
			for (var i=0; i<nodes.length; i++) {
				if (!nodes[i].checked) {
					allSelected = false;
					break;
				}
			}
			if (allSelected) {
				self.popupManager.Close(popup);
				//self.partTimer.Resume();
				//----------INTERFACE CALL-----------------------
				var message = new InterfaceCall("ChecklistSubmit");
				
				//-----------------------------------------------
			} else {
				info.innerHTML = submitMessages[Math.min(submitMessages.length,self.checklistSubmits)-1];
			}
		}
		popup.appendChild(submit);
		self.popupManager.Lock(popup);
		self.popupManager.AddCustom(popup,true);
	}
	this.DisplayScrapReport = function(reportConfig) {
		var self = this;
		var closeFunction = self.popupManager.AddSpecific("scrapReport");
		var scrapReport = document.getElementById("scrapReport");
		var vendorRadio = document.getElementById("vendorRadio");
		var scrapReasons = document.getElementById("scrapReasons");
		vendorRadio.onclick = function () {
			ClearChildren(scrapReasons);
			reportConfig.vendor.forEach(function (reason) {
				var option = document.createElement("OPTION");
				option.innerHTML = reason;
				option.value = reason;
				scrapReasons.appendChild(option);
			});
		};
		
		
		var productionRadio = document.getElementById("productionRadio");
		productionRadio.onclick =  function () {
			ClearChildren(scrapReasons);
			reportConfig.production.forEach(function (reason) {
				var option = document.createElement("OPTION");
				option.innerHTML = reason;
				option.value = reason;
				scrapReasons.appendChild(option);
			});
		};
		
		productionRadio.onclick();
		productionRadio.checked = true;
		self.popupManager.Lock(scrapReport);
		
		document.getElementById("submitScrap").onclick = function (event) {
			closeFunction();
			
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("ScrapEvent",
			{
				/* travelerID: self.travelerView.traveler.ID,
				eventType: "Scrapped",
				time: application.partTimer.timerTime.asMinutes(),
				itemID: (self.travelerView.item ? self.travelerView.item.ID : "undefined"), */
				
				source: vendorRadio.checked ? "Vendor" : "Marco Group",
				reason: scrapReasons.value,
				startedWork: document.getElementById("startedWork").checked
			});
			
			//-----------------------------------------------
			
			return false;
		}
	}
	this.HandleTravelersChanged = function (message) {
		var self = this;
		if (message.mirror) {
			// The only travelers in the queue are explicitly the ones in the message
			//self.travelerQueue.Clear();
			//self.inProcessQueue.Clear();
			//self.travelerView.Clear();
			/* message.preProcess.forEach(function (obj) {
				self.travelerQueue.AddTraveler(Traveler(obj));
			}); */
			/* message.inProcess.forEach(function (obj) {
				self.inProcessQueue.AddTraveler(TravelerItem(obj));
			}); */
		} else {
			// Only update existing travelers in the queue
			message.travelers.forEach(function (obj) {
				self.travelerQueue.UpdateTraveler(new Traveler(obj));
			});
		}
		/* // autoload the first traveler in the queue if just now visiting
		if ((!self.lastStation || self.station.ID != self.lastStation.ID) && self.travelerQueue.travelers[0]) {
			self.travelerQueue.SelectTraveler(self.travelerQueue.FindTraveler(self.travelerQueue.travelers[0].ID)); // this ensures that the item is selected
		} else if (!self.travelerQueue.Exists(self.travelerView.traveler)) {
			if (self.travelerQueue.travelers[0]) {
				self.travelerQueue.SelectTraveler(self.travelerQueue.FindTraveler(self.travelerQueue.travelers[0].ID)); // this ensures that the item is selected
				self.Info("A new traveler has been loaded automatically");
			} else {
				self.travelerView.Clear();
			}
		} */
		// try and load the old traveler
		/* self.travelerQueue.travelers.forEach(function (traveler) {
			if (traveler.ID == self.travelerView.lastTravelerID) {
				application.LoadTraveler(self.travelerQueue.FindTraveler(traveler.ID));
				//self.travelerQueue.SelectTraveler();
				//self.travelerView.Load(traveler);
			}
		}); */
		return "{}";
	}
	// Direct UI control vvvvvvvvvvvvvvvvvvvvvvvvvv
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
		});
	}
	this.DisableUI = function () {
		document.getElementById("completeItemBtn").classList.add("disabled");
		document.getElementById("scrapItemBtn").classList.add("disabled");
	}
	this.DisableSubmitBtn = function () {
		document.getElementById("submitTravelerBtn").classList.add("disabled");
	}
	this.HideSubmitBtn = function () {
		document.getElementById("submitTravelerBtn").classList.add("hidden");
		document.getElementById("qtyCompletedRow").style.display = "none";
	}
	this.DisableMoreInfoBtn = function () {
		document.getElementById("moreInfoBtn").classList.add("disabled");
	}
	this.DisableDrawingBtn = function () {
		document.getElementById("viewDrawingBtn").classList.add("disabled");
	}
	this.DisableCommentBtn = function () {
		document.getElementById("addCommentBtn").classList.add("disabled");
	}
	this.EnableUI = function () {
		document.getElementById("completeItemBtn").classList.remove("disabled");
		document.getElementById("scrapItemBtn").classList.remove("disabled");
	}
	this.EnableSubmitBtn = function () {
		document.getElementById("submitTravelerBtn").classList.remove("disabled");
	}
	this.ShowSubmitBtn = function () {
		document.getElementById("submitTravelerBtn").classList.remove("hidden");
		document.getElementById("qtyCompletedRow").style.display = "inherit";
	}
	this.EnableMoreInfoBtn = function () {
		document.getElementById("moreInfoBtn").classList.remove("disabled");
	}
	this.EnableDrawingBtn = function () {
		document.getElementById("viewDrawingBtn").classList.remove("disabled");
	}
	this.EnableCommentBtn = function () {
		document.getElementById("addCommentBtn").classList.remove("disabled");
	}
	this.SetQtyPending = function (qty) {
		var qtyPending = document.getElementById("qtyPending");
		if (qty > 0) {
			qtyPending.innerHTML = qty;
		} else {
			qtyPending.innerHTML = "-";
		}
	}
	this.SetQtyCompleted = function (qty) {
		var qtyCompleted = document.getElementById("qtyCompleted");
		if (qty > 0) {
			qtyCompleted.innerHTML = qty;
		} else {
			qtyCompleted.innerHTML = "-";
		}
	}
	this.LoadTravelerView = function (params) {
		this.travelerView.LoadTable(params.ID,params.members,params.itemMembers);
	}
	this.ClearTravelerView = function () {
		this.travelerView.Clear();
	}
	this.StartPartTimer = function (minutes) {
		this.partTimer.Start(minutes);
	}
	this.StopPartTimer = function () {
		this.partTimer.Stop();
	}
	this.StartStationTimer = function () {
		this.stationTimer.Start(0.0);
	}
	this.ClearStationTimer = function () {
		this.stationTimer.Clear();
	}
	this.CountdownPartTimer = function (minutes) {
		this.partTimer.CountDown(minutes);
	}
	this.ResumePartTimer = function () {
		this.partTimer.Resume();
	}
	this.ClearPartTimer = function () {
		this.partTimer.Clear();
	}
	//^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
	
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
		self.travelerView.Clear();
		// message
		var p = document.createElement("P");
		p.className = "fourEM";
		p.innerHTML = "Select a traveler to work on"
		self.travelerView.DOMcontainer.appendChild(p);
		//----------------
		// traveler queue
		//----------------
		self.travelerQueue = new TravelerQueue();
		self.travelerQueue.Initialize("preProcessQueueContainer");
		
		self.inProcessQueue = new TravelerQueue();
		self.inProcessQueue.Initialize("inProcessQueueContainer");
		//----------------
		// completed travelers
		//----------------
		self.completedList = new TravelerQueue();
		self.completedList.Initialize("completedList");
		//----------------
		// Options
		//----------------
		// OPEN DRAWING PDF--------------
		var drawingBtn = document.getElementById("viewDrawingBtn");
		drawingBtn.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("OpenDrawing");
			//-----------------------------------------------
			//self.popupManager.Close(popup);
		}
		
		// MORE INFO--------------
		var infoBtn = document.getElementById("moreInfoBtn");
		infoBtn.onclick = function () {
			//self.popupManager.Close(popup);
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("LoadTravelerJSON");
			//-----------------------------------------------
		}
		document.getElementById("optionsBtn").onclick = function () {
			new InterfaceCall("OptionsMenu");
		}
		document.getElementById("addCommentBtn").onclick = function () {
			new InterfaceCall("AddComment");
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
						if (object.hasOwnProperty("method")) {
							if (self.hasOwnProperty(object.method) && object.hasOwnProperty("parameters")) {
								// The server is invoking a client method
								if (object.callback == "") {
									// Inovkes a client function, with the parameters passed
									self[object.method](object.parameters);
								} else {
									// Invokes a callback function on the server, passing the object that was returned
									// by the client function as the parameter
									new InterfaceCall(object.callback,self[object.method](object.parameters));
								}
							}
						}
					}
				} else if (messageEvent.data instanceof Blob) {
					// recieved blob data
				}
			};
			// websocket is closed.
			self.websocket.onclose = function() {
				var message = "You are not connected to the server;";
				message += "<br>1.) Refresh the page";
				message += "<br>2.) If problem persists, Inform your supervisor";
				/* message += "<br>3.) Do not harm this computer";
				message += "<br>4.) Keep calm"; */
				self.popupManager.Error(message);
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
			/* var DOMqueueItem = document.createElement("DIV");
			
			if (traveler.selected) {
				DOMqueueItem.className = "button blueBack queue__item selected";
			} else {
				DOMqueueItem.className = "button blueBack queue__item";
			}
			
			DOMqueueItem.innerHTML = pad(traveler.ID,6) + "<br>";
			var itemCode = document.createElement("SPAN");
			itemCode.className = "queue__item__desc beige";
			var itemCodeString = "";
			if (traveler.itemCode) {
				itemCodeString = traveler.itemCode;
			} else if (traveler.type == "TableBox") {
				itemCodeString = "For: " + traveler.parentTravelers[0];
			}
			itemCode.innerHTML = itemCodeString;
			DOMqueueItem.appendChild(itemCode);
			
			DOMqueueItem.onmousedown = function () {
				self.SelectTraveler(traveler);
			}
			self.DOMelement.appendChild(DOMqueueItem); */
			
			var DOMqueueItem = traveler.CreateQueueItem(application.station.name);
			DOMqueueItem.onclick = function () {
				if (traveler.queueType = "traveler") {
					//----------INTERFACE CALL-----------------------
					new InterfaceCall("LoadTravelerAt",
					{
						travelerID: traveler.ID,
						station: application.station.name
					});
					//-----------------------------------------------
				} else if (traveler.queueType ==  "travelerItem") {
					//----------INTERFACE CALL-----------------------
					new InterfaceCall("LoadItem",
					{
						travelerID: traveler.travelerID,
						itemID: traveler.ID
					});
					//-----------------------------------------------
				}
			}
			DOMqueueItem.style.fontSize = "1em";
			/* DOMqueueItem.onmousedown = function () {
				new InterfaceCall("LoadTraveler",{travelerID: traveler.ID});
			} */
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
		ClearChildren(self.DOMcontainer);
		/* var self = this;
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
		//document.getElementById("itemQueue").style.display = "none";
		document.getElementById("completeItemBtn").innerHTML = "Complete item";
		document.getElementById("scrapItemBtn").innerHTML = "Scrap item";
		self.ResetSliders();
		// clear the timer
		application.partTimer.Clear();
		// stop the station timer
		application.stationTimer.Stop(); */
	}
	
	this.UpdateSubmitBtn = function () {
		if (this.traveler && this.traveler.queueItem.qtyCompleted > 0) {
			document.getElementById("submitTravelerBtn").classList.remove("disabled");
		} else {
			document.getElementById("submitTravelerBtn").classList.add("disabled");
		}
	}
	
	this.LoadTable = function (ID,members,itemMembers) {
		var self = this;
		ClearChildren(self.DOMcontainer);
		// create the view header
		var viewHeader = document.createElement("DIV");
		viewHeader.className = "view__header";
		viewHeader.innerHTML = ID;

		
		self.DOMcontainer.appendChild(viewHeader);
		// create the table
		var DOMtable = document.createElement("TABLE");
		DOMtable.className = "view";
		
		// add the part row
		//self.traveler.members.unshift({name: "Part", value: self.traveler.itemCode, qty: self.traveler.quantity});
		// add the column header
		members.unshift({name: "Property", value: "Value", qty: "Qty.",style:"view__row--header italics"});
		
		self.TravelerTableRows(members,DOMtable);
		
		// remove the column header
		members.shift();
		
		// add the table
		self.DOMcontainer.appendChild(DOMtable);
		
		// Item members
		if (itemMembers && itemMembers.length > 0) {
			// create the table title
			var itemTitle = document.createElement("DIV");
			itemTitle.className = "view__header";
			itemTitle.innerHTML = "This item";
			self.DOMcontainer.appendChild(itemTitle);
			
			var itemTable = document.createElement("TABLE");
			itemTable.className = "view";
			self.TravelerTableRows(itemMembers,itemTable);
			self.DOMcontainer.appendChild(itemTable);
		}
		// start the timer
		//application.partTimer.CountDown(self.traveler.laborRate);
		//application.stationTimer.Resume();
	}
	this.TravelerTableRows = function (members,DOMtable) {
		// all other properties are in the table body
		members.forEach(function (property) {
			var row = document.createElement("TR");
			if (property.hasOwnProperty("style")) row.className = property.style;
			// Property name
			var propName = document.createElement("TD");
			propName.className = "view__item";
			propName.innerHTML = Breakify(property.name);
			row.appendChild(propName);
			// Property value
			var propValue = document.createElement("TD");
			propValue.className = "view__item" + (property.name == "Part" ? " " + "twoEM center red shadow" : "");
			propValue.innerHTML = Breakify(property.value);
			row.appendChild(propValue);
			// Property quantity (if it has a quantity)
			var propQty = document.createElement("TD");
			propQty.className = "view__item center lime bold shadow";
			if (property.qty != "" || property.name == "Part") {
				
				propQty.innerHTML = property.qty;
			} else {
				//propQty.className = "view__item--null";
			}
			row.appendChild(propQty);
			// add the row to the table
			DOMtable.appendChild(row);
		});
	}
	this.AutomaticReload = function (oldT,newT) {
		if ((oldT && newT) && oldT.ID != newT.ID) {
			application.Info("A new traveler has been loaded automatically");
		}
	}
	this.EnableTravelerBtns = function () {
		document.getElementById("moreInfoBtn").className = "dark button twoEM";
		document.getElementById("viewDrawingBtn").className = "dark button twoEM";
	}
	this.DisableTravelerBtns = function () {
		document.getElementById("moreInfoBtn").className = "disabled";
		document.getElementById("viewDrawingBtn").className = "disabled";
	}
	this.LoadItem = function (traveler, item, sequenceID) {
		var self = this;
		self.Load(traveler);
		//----------INTERFACE CALL-----------------------
		/* var message = new InterfaceCall("LoadItem",
		{
			travelerID: traveler.ID,
			itemID: item.ID
		}); */
		//-----------------------------------------------
		self.traveler = traveler;
		self.item = item;
		// enable the buttons
		self.EnableUI();
		self.UpdateSubmitBtn();
		// clear old DOM objects
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
		//document.getElementById("completeItemBtn").innerHTML = "Complete item #" + self.item.ID;
		//document.getElementById("scrapItemBtn").innerHTML = "Scrap item #" + self.item.ID;
		document.getElementById("completeItemBtn").innerHTML = "Complete item"
		document.getElementById("scrapItemBtn").innerHTML = "Scrap item"
		//self.LoadTable(sequenceID);
	}
	this.Load = function (traveler) {
		var self = this;
		//----------INTERFACE CALL-----------------------
		/* var message = new InterfaceCall("LoadTraveler",
		{
			travelerID: traveler.ID
		}); */
		
		//-----------------------------------------------
		// initialize
		self.Clear();
		self.traveler = traveler;
		self.ResetSliders();
		self.UpdateSubmitBtn();
		if (!self.traveler)  {
			self.DisableTravelerBtns();
			return;
		} else {
			self.EnableTravelerBtns();
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
			//document.getElementById("itemQueue").style.display = "none";
			
			
			
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
			//document.getElementById("itemQueue").style.display = "block";
			// create the selection for traveler items
			/* var select = document.getElementById("itemSelect");
			// clear old options
			while (select.hasChildNodes()) {
				select.removeChild(select.lastChild);
			}
			select.className = "dark twoEM";
			self.traveler.items.forEach(function (item) {
				// only add item if it is at this station and uncomplete
				if (item.station == application.station.name && !Contains(item.history,[{prop:"process",value:"Completed"},{prop:"station",value:item.station}])) {
					var option = document.createElement("OPTION");
					option.value = item.ID;
					option.innerHTML = item.ID;
					select.appendChild(option);
				}
			});
			select.onchange = function () {
				new InterfaceCall("LoadItem",{
					travelerID:self.traveler.ID,
					itemID:select.value
				});
				//self.LoadItem(self.traveler,self.traveler.FindItem(select.value));
			}
			select.value = 0; */
		}
		
	}
	this.ResetSliders = function () {
		var qtyMade = document.getElementById("qtyCompleted");
		//var qtyScrapped = document.getElementById("qtyScrapped");
		var qtyPending = document.getElementById("qtyPending");
		qtyMade.innerHTML = this.traveler ? this.traveler.queueItem.qtyCompleted : '-';
		//qtyScrapped.innerHTML = this.traveler ? this.traveler.stations[application.station.name].qtyScrapped : '-';
		qtyPending.innerHTML = this.traveler ? this.traveler.queueItem.qtyPending : '-';
		
		
		
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
		//var qtyScrapped = document.getElementById("qtyScrapped");
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
			new InterfaceCall("CompleteItem");
			
			//-----------------------------------------------
			//if (application.station.mode == "Serial") document.getElementById("submitTravelerBtn").onclick();
			//self.UpdateSubmitBtn();
			// Restart part timer 
			//application.partTimer.CountDown(self.traveler.laborRate);
		}
		// scrapping a traveler item
		document.getElementById("scrapItemBtn").onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("DisplayScrapReport");
			//-----------------------------------------------
			
			//self.UpdateSubmitBtn();
		}
		// Submitting a finished traveler
		document.getElementById("submitTravelerBtn").onclick = function () {
			/* this is just for responsiveness, 
			the server will soon confirm traveler positions in an update*/
			var completedTraveler = self.traveler;
			// if (parseInt(qtyScrapped.value) < self.traveler.quantity && parseInt(qtyMade) < self.traveler.quantity) {
				// completedTraveler = self.traveler;
			// } else {
				// completedTraveler = self.traveler;
			// }
			//self.lastTravelerID = completedTraveler.ID;
			//----------INTERFACE CALL-----------------------
			new InterfaceCall("SubmitTraveler");
			
			//-----------------------------------------------
			//self.UpdateSubmitBtn();
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
		
		new InterfaceCall("SearchSubmitted",{travelerID:travelerID,itemID,itemID});
		/* if (!isNaN(travelerID)) {
			var traveler = application.travelerQueue.FindTraveler(travelerID);
			if (traveler) {
				self.AutomaticReload(self.traveler,traveler);
				
				var item = traveler.FindItem(itemID);
				if (item && item.station == application.station.name) {
					if (Contains(item.history,[{prop:"station",value:item.station},{prop:"type",value:0}])) {
						application.Info("Item [" + pad(travelerID,6) + "-" + itemID + "] has already been completed at this station :)");
					} else {
						self.LoadItem(traveler,application.travelerQueue.FindItem(travelerID,itemID));
						// SEND SUBMIT EVENT TO SERVER SEPARATELY
						new InterfaceCall("SearchSubmitted",{
							travelerID: travelerID,
							itemID: itemID
						});
					}
				} else if (!isNaN(itemID)) {
					application.Info("Item [" + pad(travelerID,6) + "-" + itemID + "] is not at your station;<br>It is at: " + item.station);
				} else {
					application.travelerQueue.SelectTraveler(traveler);
				}
			} else {
				application.Info("Traveler [" + pad(travelerID,6) + "] isn't at your station :(");
			}
		} else {
			application.Info("Invalid traveler ID :(");
		}
		*/
		} catch (exception) {
			application.Info(exception.message);
		} 
		document.getElementById("travelerSearchBox").value = "";
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
