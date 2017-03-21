function InterfaceCall(methodName, parameters) {
	this.interfaceMethod = methodName;
	this.parameters = parameters;
}
function Traveler(obj) {
	obj.selected = false;
	obj.FindItem = function (itemID) {
		var item;
		obj.items.some(function (i) {
			if (i.ID == itemID) {
				item = i;
				return true;
			}
		});
		return item;
	}
	return obj;
}
function pad(num, size) {
    var s = num+"";
    while (s.length < size) s = "0" + s;
    return s;
}
// use like : Contains(someArray,[{prop:"propName",value:9},{...},...])
// ALL predicate must be true 
function Contains(list,predicateList) {
	var match = false;
	list.some(function (element) {
		var allTrue = true;
		predicateList.some(function (predicate) {
			if (element[predicate.prop] != predicate.value) {
				allTrue = false;
				return true; // break loop
			}
		});
		if (allTrue) {
			match = true;
			return true; // break loop
		}
	});
	return match;
}
function Info(message) {
	document.getElementById("blackout").style.visibility = "visible";
	document.getElementById("confirm").style.display = "flex";
	document.getElementById("confirmMessage").innerHTML = message;
}
function CloseInfo(message) {
	document.getElementById("blackout").style.visibility = "visible";
	document.getElementById("confirm").style.display = "flex";
	document.getElementById("confirmMessage").innerHTML = message;
}
function JSONviewer(object,name,quit) {
	this.stack = [];
	this.DOMcontainer;
	this.Open = function (obj) {
		var self = this;
		self.Clear();
		// quit
		if (!obj) {
			self.Quit();
			return;
		}
		// add this objet to the stack
		self.stack.push(obj);
		// add a back button
		var backBtn = document.createElement("DIV");
		backBtn.className = "JSONviewer__back";
		backBtn.innerHTML = (self.stack.length <= 1 ? "Close" : "Back");
		backBtn.onclick = function () {
			self.Close();
		}
		/* var backImg = document.createElement("IMG");
		backImg.src = "./img/back.png";
		backImg.style.height = "50%";
		backBtn.appendChild(backImg); */
		
		/*backBtn.style.background = 'url("./img/back.png"), linear-gradient(to right, transparent, #4d4d4d, transparent)';
		backBtn.style.backgroundX
		backBtn.style.backgroundRepeat = "no-repeat";
		backBtn.style.backgroundSize = "contain"; */
		
		self.DOMcontainer.appendChild(backBtn);
		// title of current scope
		var title = document.createElement("P");
		title.className = "green shadow twoEM";
		title.innerHTML = obj.Name;
		self.DOMcontainer.appendChild(title);
		
		// list the properties
		for (var property in obj) {
			if (property != "Name") {
				var value = obj[property];
				var listHorizontal = document.createElement("DIV");
				listHorizontal.className = "list--horizontal JSONviewer__field";
				
				var propName = document.createElement("P");
				propName.innerHTML = property + ": ";
				listHorizontal.appendChild(propName);
		
				if (Array.isArray(value)) {
					var scrollDiv = document.createElement("DIV");
					scrollDiv.className = "JSONviewer__scrollable";
					value.forEach(function (element,index) {
						var itemList = document.createElement("DIV");
						itemList.className = "list--horizontal";
						if (typeof(element) == "object") itemList.innerHTML = "item " + index + ":";
						self.DisplayValue(property,element,itemList);
						
						scrollDiv.appendChild(itemList);
					});
					listHorizontal.appendChild(scrollDiv);
				} else {
					self.DisplayValue(property,value,listHorizontal);
				}
				self.DOMcontainer.append(listHorizontal);
			}
		}
	}
	this.Close = function () {
		this.stack.pop();
		this.Open(this.stack.pop(),this.lastName);
	}
	this.Clear = function () {
		var self = this;
		while (self.DOMcontainer.hasChildNodes()) {
			self.DOMcontainer.removeChild(self.DOMcontainer.lastChild);
		}
	}
	this.Quit = quit;
	this.DisplayValue = function (property,value,DOMparent) {
		var self = this;
		
		
		var valueDiv = document.createElement("DIV");
		if (typeof(value) == "object") {
			valueDiv.className = "dark button";
			valueDiv.onclick = function () {
				value.Name = property;
				self.Open(value);
			}
			valueDiv.innerHTML = "Open";
		} else {
			valueDiv.className = "beige";
			if (property.toLowerCase().includes("station")) {
				valueDiv.innerHTML = application.stationList[value].name;
			} else if (property.toLowerCase().includes("time")) {
				valueDiv.innerHTML = value + " min";
			} else {
				valueDiv.innerHTML = value;
			}
		}
		DOMparent.appendChild(valueDiv);
					
	}
	this.Initialize = function (object,name) {
		var self = this;
		
		if (object) {
			self.DOMcontainer = document.createElement("DIV");
			self.DOMcontainer.className = "JSONviewer";
			object.Name = name;
			self.Open(object);
		}
	}
	this.Initialize(object,name);
}
function PopupManager(blackout) {
	this.blackout;
	// adds a custom popup, where the close function is returned for the popup creator to call
	this.AddCustom = function (popup) {
		var self = this;
		self.Open(popup);
		// Close button
		var close = self.CreateButton("Close");
		close.classList.remove("dark");
		close.className += " yellowBack";
		close.removeClass
		close.onclick = function () {self.Close(popup);}
		popup.insertBefore(close,popup.firstChild);
		return close.onclick;
	}
	// displays a message with an "OK" button
	this.Info = function (message) {
		var self = this;

		var popup = self.CreatePopup();
		// the message
		var infoP = self.CreateP(message);
		popup.appendChild(infoP);
		// OK button
		var button = self.CreateButton("OK");
		button.onclick = function () {self.Close(popup);}
		popup.appendChild(button);
		
		self.Open(popup);
	}
	// displays a json viewer from the given object
	this.AddJSONviewer = function (obj, name) {
		var self = this;
		var jsonViewer = new JSONviewer(obj,name,function () {
			self.Close(jsonViewer.DOMcontainer);
		});
		
		self.Open(jsonViewer.DOMcontainer);
	}
	// displays an error message with no ability to close
	this.Error = function (message) {
		var self = this;

		var popup = self.CreatePopup();
		// the message
		var infoP = self.CreateP(message);
		popup.appendChild(infoP);
		
		self.Open(popup);
	}
	//=========================================
	// CREATE MODULAR DOM OBJECTS
	
	// initializes and returns a new popup container
	this.CreatePopup = function () {
		var popup = document.createElement("DIV");
		popup.className = "blackout__popup";
		this.blackout.appendChild(popup);
		return popup;
	}
	this.CreateP = function (innerHTML) {
		var p = document.createElement("P");
		p.innerHTML = innerHTML;
		return p;
	}
	this.CreateButton = function (innerHTML) {
		var button = document.createElement("DIV");
		button.className = "dark button";
		button.innerHTML = innerHTML;
		return button;
	}
	//=========================================
	
	// clears everything and closes the blackout
	this.Close = function (popup) {
		var self = this;
		// close the specified popup, if it exists
		if (self.blackout.contains(popup)) {
			self.blackout.removeChild(popup);
		}
		// there are no open popups
		if (self.blackout.children.length <= 0) {
			self.blackout.className = "blackout hidden";
		}
		
	}
	this.CloseAll = function () {
		var self = this;
		while (self.blackout.lastChild) {
			self.blackout.removeChild(self.blackout.lastChild);
		}
		self.blackout.className = "blackout hidden";
	}
	this.Open = function (popup) {
		var self = this;
		self.blackout.appendChild(popup);
		self.blackout.className = "blackout";
	}
	// initializes the blackout container
	this.Initialize = function(blackout) {
		var self = this;
		self.blackout = blackout;
	}
	
	this.Initialize(blackout);
}