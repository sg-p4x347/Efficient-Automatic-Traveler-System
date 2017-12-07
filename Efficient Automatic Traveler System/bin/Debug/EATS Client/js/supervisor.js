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
	this.type = "supervisor";
	// DOM
	this.queueArray;
	this.JSONviewer;
	this.popupManager;
	this.IOScheckTimeout;
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
	//=====================================================
	// Server/Client interface
	//=====================================================
	this.AddHTML = function(params) {
		var parent = document.getElementById(params.id);
		if (parent) {
			parent.appendChild(HTML(params.html));
		} else {
			console.log(params.id + " could not be found");
		}
	}
	this.RemoveHTML = function(id) {
		var element = document.getElementById(id);
		if (element) {
			element.parentNode.removeChild(element);
		} else {
			console.log(id + " could not be found");
		}
	}
	this.EditHTML = function(params) {
		var element = document.getElementById(params.id);
		if (element) {
			var parent = element.parentNode;
			parent.removeChild(element);
			parent.appendChild(HTML(params.html));
		} else {
			console.log(params.id + " could not be found");
		}
	}
	this.ControlPanel = function (format) {
		this.popupManager.ControlPanel(format);
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
	
	//----------------
	// Multi-select
	//----------------
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
	//----------------
	// station list
	//----------------
	
	this.QuantityAt = function (obj) {
		this.queues[obj.station].totalQtyElem.innerHTML = obj.quantity;
	}
	this.Info = function (message) {
		this.popupManager.Info(message);
	}
	this.Updating = function (text) {
		document.getElementById("updatingPopup").innerHTML = (text && text.text ? text.text : "");
		this.popupManager.AddSpecific("updatingPopup");
	}
	this.InitLabelTypes = function (labelTypes) {
		this.labelTypes = labelTypes;
	}
	// Loads the traveler GUI
	this.LoadTraveler = function (traveler) {
		if (application.GetSelectedIDs().length > 0) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("MultiTravelerOptions",
			{
				travelerIDs: application.GetSelectedIDs()
			});
			//-----------------------------------------------
		} else {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("TravelerPopup",
			{
				travelerID: traveler.ID,
				station: (traveler.station ? traveler.station : "")
			});
			//-----------------------------------------------
		}
	}
	this.LoadTravelerJSON = function (traveler) {
		//this.JSONviewer = new JSONviewer(traveler,"Traveler");
		this.popupManager.AddJSONviewer(traveler,"Traveler");
	}
	this.LoadTravelerAt = function (traveler) {
		if (application.GetSelectedIDs().length > 0) {
			//----------INTERFACE CALL-----------------------
			new InterfaceCall("MultiTravelerOptions",
			{
				travelerIDs: application.GetSelectedIDs()
			});
			//-----------------------------------------------
		} else {
			//----------INTERFACE CALL-----------------------
			new InterfaceCall("TravelerPopup",
			{
				travelerID: traveler.ID,
				station: (traveler.station ? traveler.station : "")
			});
			//-----------------------------------------------
		}
	}
	this.TravelerPopup = function(params) {
		var self = this;
		
		var moveSelection = application.stationList.Where(function (station) {
			return station.creates.indexOf(params.object.type) != -1 || station.name === "Start";
		}).ArrayFromProperty("name");
		self.popupManager.ObjectViewer("Traveler",params.displayFields,params.object,[
			new PopupSelection("Move","Select a station", moveSelection,function (traveler,value) {
				new InterfaceCall("MoveTravelerStart",
				{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID),
					station: value
				});
			}),
			new PopupButton("More Info",function (traveler) {
				new InterfaceCall("LoadTravelerJSON",{
					travelerID: traveler.ID
				});
			}),
			new PopupButton("Disintegrate",function (traveler) {
				new InterfaceCall("DisintegrateTraveler",{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
			}),
			new PopupButton("Enter Production",function (traveler) {
				new InterfaceCall("EnterProduction",{
					travelerIDs: application.GetSelectedIDs().concat(traveler.ID)
				});
			}),
			new PopupButton("Print Labels",function (traveler) {
				
				application.PrintLabelPopup(traveler);
			})
		]);
	}
	
	// Loads the item GUI
	this.LoadItem = function (item) {
		this.popupManager.AddJSONviewer(item,"Item");
		//this.JSONviewer = new JSONviewer(item,"Traveler Item");
	}
	this.CloseAll = function () {
		this.popupManager.CloseAll();
	}
	this.FocusOnSearch = function () {
		document.getElementById("searchBox").value = "";
		document.getElementById("searchBox").focus();
	}
	//----------------
	// supervisor Options (called from the server)
	//----------------
	this.TravelerForm = function (format) {
		var self = this;
		self.StopAutofocus();
		self.popupManager.Form(format, function (filledForm) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("NewTraveler",filledForm);
			
			//-----------------------------------------------
			self.StartAutofocus();
		});
	}
	this.UserForm = function (format,method = "NewUser") {
		var self = this;
		//self.popupManager.CloseAll();
		self.StopAutofocus();
		self.popupManager.Form(format, function (filledForm) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall(method,filledForm);
			
			//-----------------------------------------------
			self.StartAutofocus();
		});
		
	}
	this.NewKanbanItemForm = function (format,method = "NewKanbanItem") {
		var self = this;
		//self.popupManager.CloseAll();
		self.StopAutofocus();
		self.popupManager.Form(format, function (filledForm) {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall(method,filledForm);
			
			//-----------------------------------------------
			self.StartAutofocus();
		});
		
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
	this.EditUserForm = function (format) {
		this.UserForm(format,"EditUser");
	}
	this.CreateSummary = function (summaryObj) {
		var self = this;
		self.popupManager.CloseAll();
		var closeFunction = self.popupManager.AddSpecific("summaryPopup");//.cloneNode(true));
		
		var summaryTable = document.getElementById("summary"); // TABLE
		
		while(summaryTable.rows.length > 0) {
			summaryTable.deleteRow(0);
		}	
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
	this.DateRangePopup = function (params) {
		var self = this;
		self.popupManager.CloseAll();
		
		var popup = self.popupManager.CreatePopup("Date Range");
		
		var rowA = self.popupManager.CreateHorizontalList();
		rowA.className = "justify-space-between";
		rowA.appendChild(self.popupManager.CreateP("From"));
		var dateA = self.popupManager.CreateDateInput();
		rowA.appendChild(dateA);
		popup.appendChild(rowA);
		
		var rowB = self.popupManager.CreateHorizontalList();
		rowB.className = "justify-space-between";
		rowB.appendChild(self.popupManager.CreateP("To"));
		var dateB = self.popupManager.CreateDateInput();
		rowB.appendChild(dateB);
		popup.appendChild(rowB);
		
		var submit = self.popupManager.CreateButton("Submit");
		popup.appendChild(submit);
		submit.onclick = function () {
			params.A = dateA.value;
			params.B = dateB.value;
			new InterfaceCall(params.innerCallback,params);
			self.StartAutofocus();
		}
		
		self.popupManager.AddCustom(popup);
	}
	this.ClearSearch = function() {
		document.getElementById("searchBox").value = "";
	}
	// Utility
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
	//----------------
	// DOM events
	//----------------


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


	this.ControlPanel = function (controlPanel) {
		var self = this;
		self.popupManager.ControlPanel(controlPanel,document.getElementById(controlPanel.ID));

	}
	/* this.EditHTML = function (params) {
		EditHTML(params);
	} */
	this.SearchPopup = function (params) {
		var self = this;
		self.StopAutofocus();
		self.popupManager.Search(params.message,function (searchPhrase) {
			new InterfaceCall(params.interfaceCall,{searchPhrase: searchPhrase});
			self.StartAutofocus();
		});
	}
	this.Redirect = function(location) {
		//window.location = location;
		var win = window.open(location,'_blank');
		win.focus();
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

		//window.addEventListener("keydown",);
		document.getElementById("searchForm").onsubmit = function () {
			if (searchBox.value.length > 0) {
				new InterfaceCall("SearchSubmitted",{
				searchPhrase: searchBox.value});
			}
			
			return false;
		}
		
		//----------------
		// supervisor Options
		//----------------
		
		document.getElementById("superOptionsBtn").onclick = function () {
			new InterfaceCall("OptionsMenu");
			
		}
		//----------------
		// help
		//----------------
		
		document.getElementById("helpBtn").onclick = function () {
			new InterfaceCall("Help");
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
function TravelerQueue(station) {
	this.DOMcontainer;
	this.DOMelement;
	this.totalQtyElem;
	this.totalLaborElem;
	this.travelers;
	this.station;
	// maintain scroll position
	this.scrollPos;
	
	this.Clear = function () {
		this.travelers = [];
		this.RePaint();
	}
	this.AddTraveler = function (traveler) {
		this.travelers.push(traveler);
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
	}
	
	this.BalanceSliders = function(qtyMoving,qtyStaying,movingBar,stayingBar,traveler) {
		movingBar.style.width = ((parseInt(qtyMoving.value) / traveler.quantity) * 100) + "%";
		stayingBar.style.width = ((parseInt(qtyStaying.value) / traveler.quantity) * 100) + "%";
	}
	this.Initialize = function (station) {
		var self = this;
		self.station = station;
		
		self.DOMcontainer = document.createElement("DIV");
		self.DOMcontainer.className = "queueContainer";
		if (station.name == "Start") {
			self.DOMcontainer.className = "queueContainer queueContainer--tall";
		}
		var queueTitle = document.createElement("DIV");
		queueTitle.className = "heading";
		queueTitle.innerHTML = self.station.name;
		self.DOMcontainer.appendChild(queueTitle);
		// Total traveler quantity ---------
		var totalQtyElem = document.createElement("DIV");
		totalQtyElem.innerHTML = "Total Qty:";
		
		self.totalQtyElem = document.createElement("SPAN");
		self.totalQtyElem.className = "beige";
		totalQtyElem.appendChild(self.totalQtyElem);
		self.DOMcontainer.appendChild(totalQtyElem);
		// Total labor quantity ------------
		var totalLaborElem = document.createElement("DIV");
		totalLaborElem.innerHTML = "Total Labor:";
		
		self.totalLaborElem = document.createElement("SPAN");
		self.totalLaborElem.className = "beige";
		totalLaborElem.appendChild(self.totalLaborElem);
		totalLaborElem.appendChild(document.createTextNode(" min"));
		self.DOMcontainer.appendChild(totalLaborElem);
		//----------------------------------
		
		self.DOMelement = document.createElement("DIV");
		self.DOMelement.className = "queue";
		self.DOMelement.onscroll = function () {
			self.scrollPos = this.scrollTop;
		}
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
		itemCode.innerHTML = (traveler.itemCode ? traveler.itemCode : "");
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