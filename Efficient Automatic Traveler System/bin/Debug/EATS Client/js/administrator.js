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
	this.type = "administrator";
	this.popupManager;
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
	this.LoginPopup = function (info) {
		var self = this;
		// popup stuff
		self.popupManager.CloseAll();
		//self.StopAutofocus();
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
					PWD: document.getElementById("pwdBox").value
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
		//self.StartAutofocus();
		//document.getElementById("logoutBtn").className = "dark button twoEM";
		// LOG OUT BUTTON
		//var logoutBtn = document.getElementById("logoutBtn");
		/* logoutBtn.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("Logout",{},"This");
			
			//-----------------------------------------------
			self.travelerView.Clear();
			self.LoginPopup();
			
		}
		self.userID = document.getElementById("uidBox").value;
		logoutBtn.innerHTML = "Logout " + data.user.name;
		
		// set the station
		self.SetStation(data.station);
		// window title
		document.getElementById("windowTitle").innerHTML = self.station.name;
		// start the station timer
		self.stationTimer.Start(); */
		
	}
	this.ConfigBtnClick = function () {
		//----------INTERFACE CALL-----------------------
		var message = new InterfaceCall("LoadConfig");
		//-----------------------------------------------
	}
	this.UsersBtnClick = function () {
		//----------INTERFACE CALL-----------------------
		var message = new InterfaceCall("LoadUsers");
		//-----------------------------------------------
	}
	this.LoadJSON = function (obj,name) {
		var self = this;
		var jsonViewer = self.popupManager.AddJSONviewer(obj,name);
		jsonViewer.DOMcontainer.style.width = "auto";
		jsonViewer.DOMcontainer.style.minWidth = "200px";
		jsonViewer.DOMcontainer.style.height = "90%";
	}
	//==========================================
	//=========Calls from the server ===========
	//==========================================
	
	this.LoadUsers = function (users) {
		this.LoadJSON(users,"Users");
	}
	this.LoadConfig = function (config) {
		this.LoadJSON(config, "System Configuration");
	}
	
	
	
	//==========================================
	// initialize
	this.Initialize = function () {
		var self = this;
		self.popupManager = new PopupManager(document.getElementById("blackout"));
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
				self.websocket.send("AdministratorClient");
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
									// The server is invoking a client method
									if (object.parameters != "") {
										self[object.method](object.parameters);
									} else {
										self[object.method]();
									}
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
				self.popupManager.Error("You are not connected to the server;<br> either refresh the page, or inform Gage Coates");
				console.log("Connection is closed..."); 
			};
		} else {
			alert("WebSocket NOT supported by your Browser!");
        }
		
		//-------------------
		// MAIN BUTTONS
		//-------------------
		
		document.getElementById("configBtn").onclick = function () {self.ConfigBtnClick()};
		document.getElementById("usersBtn").onclick = function () {self.UsersBtnClick()};
		
	}
}