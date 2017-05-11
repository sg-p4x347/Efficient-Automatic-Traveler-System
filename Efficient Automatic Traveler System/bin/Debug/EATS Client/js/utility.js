function InterfaceCall(methodName, parameters, target) {
	this.interfaceMethod = methodName;
	this.parameters = (parameters === undefined ? "" : parameters);
	this.interfaceTarget = (target === undefined ? "This" : target);
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
	obj.CreateQueueItem = function (station) {
		var self = this;
		var DOMqueueItem = document.createElement("DIV");
		self.colorClass = "blueBack";
		switch (application.view.viewState) {
			case "PreProcess": 
			if (self.quantity > 0) {
				self.colorClass = "blueBack";
			} else {
				self.colorClass = "ghostBack";
			}
			break;
			case "InProcess": self.colorClass = "redBack"; break;
			case "PostProcess": self.colorClass = "greenBack"; break;
		}
		DOMqueueItem.className = "button queue__item twoEM " + self.colorClass;
		if (self.selected) {
			DOMqueueItem.className += " selected";
		}
		DOMqueueItem.innerHTML = pad(self.type == "TableBox" ? self.parentTravelers[0] : self.ID,6) + "<br>";
		// QTY -------------------------------------------
		var qty = document.createElement("DIV");
		qty.className = "queue__item__qty blue";
		qty.innerHTML = self.quantity;
		DOMqueueItem.appendChild(qty);
		// CHECKBOX-------------------------------------------
		if (self.quantity > 0 && application.type == "supervisor") {
			self.checkBox = document.createElement("INPUT");
			self.checkBox.type = "checkbox";
			self.checkBox.checked = self.selected;
			self.checkBox.onchange = function () {
				self.Select(this.checked);
			}
			self.checkBox.onclick = function(event) {
				event.stopPropagation();
				if (self.checkBox.checked && application.input.shift) {
					application.SelectRange(application.selection.lastTraveler,self);
				}
				application.selection.lastQueue = self.stationQueue;
				application.selection.lastTraveler = self;
			}
			DOMqueueItem.appendChild(self.checkBox);
		}
		// ITEM CODE-------------------------------------------
		var itemCode = document.createElement("SPAN");
			itemCode.className = "queue__item__desc beige";
		if (self.type == "TableBox") {
			itemCode.innerHTML = "";
		} else {
			itemCode.innerHTML = self.itemCode;
		}
		
		DOMqueueItem.appendChild(itemCode);
		
		
		
		DOMqueueItem.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("LoadTravelerAt",
			{
				travelerID: self.ID,
				station: station
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			//self.PromptAction(traveler);
		}
		self.DOMqueueItem = DOMqueueItem;
		return DOMqueueItem;
	}
	obj.Select = function(state) {
		obj.selected = state;
		obj.checkBox.checked = state;
		obj.DOMqueueItem.className = (state ? "button queue__item twoEM selected " + obj.colorClass
			: "button queue__item twoEM " + obj.colorClass);
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
						if (typeof(element) == "object") itemList.innerHTML = '[' + index + "]:";
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
			if (property.toLowerCase().includes("time")) {
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
function JSONeditor(object,name) {
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
						if (typeof(element) == "object") itemList.innerHTML = '[' + index + "]:";
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
			if (property.toLowerCase().includes("time")) {
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
	this.locked;
	this.popupCount = 0;
	// adds a custom popup, where the close function is returned for the popup creator to call
	this.AddCustom = function (popup,noClose) {
		var self = this;
		self.Open(popup);
		// Close button
		if (!noClose) {
			var close = self.CreateButton("Close");
			close.classList.remove("dark");
			close.className += " yellowBack";
			close.removeClass
			close.onclick = function () {self.Close(popup);}
			popup.insertBefore(close,popup.firstChild);
			return close.onclick;
		}
	}
	// opens a pre-formatted popup from an id
	this.AddSpecific = function (id) {
		var self = this;
		var popup = document.getElementById(id);
		popup.style.display = "inherit";
		self.Open(popup);
		return function() {self.Close(popup);} // return the close function
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
		button.className += " twoEM";
		button.onclick = function () {self.Close(popup);}
		popup.appendChild(button);
		
		self.Open(popup);
	}
	// displays a yes or no question and calls a callback for the YES option
	this.Confirm = function (question, YEScallback,NOcallback) {
		var self = this;

		var popup = self.CreatePopup();
		// the message
		var infoP = self.CreateP(question);
		popup.appendChild(infoP);
		var list = document.createElement("DIV");
		list.className = "list--horizontal";
		// NO button
		var no = self.CreateButton("NO");
		no.className += " twoEM";
		no.onclick = function () {
			self.Close(popup);
			NOcallback();
		}
		list.appendChild(no);
		
		// NO button
		var yes = self.CreateButton("YES");
		yes.className += " twoEM";
		yes.onclick = function () {
			self.Close(popup);
			YEScallback();
		}
		list.appendChild(yes);
		popup.appendChild(list);
		
		self.Lock(popup);
		self.Open(popup);
	}
	// displays a json viewer from the given object
	this.AddJSONviewer = function (obj, name) {
		var self = this;
		var jsonViewer = new JSONviewer(obj,name,function () {
			self.Close(jsonViewer.DOMcontainer);
		});
		
		self.Open(jsonViewer.DOMcontainer);
		return jsonViewer;
	}
	// displays an error message with no ability to close
	this.Error = function (message) {
		var self = this;
		self.CloseAll();
		
		var popup = self.CreatePopup();
		popup.title = "error";
		// the message
		var infoP = self.CreateP(message);
		popup.appendChild(infoP);
		
		self.Lock(popup);
		self.Open(popup);
	}
	// test if a specific popup is open
	this.Exists = function(DOMid) {
		var self = this;
		for (i = 0; i < self.blackout.childNodes.length; i++) {
			if (self.blackout.childNodes[i].id == DOMid && self.blackout.childNodes[i].style.display != "none") {
				return true;
			}
		}
		return false;
	}
	// displays a form in the format provided by the formData object
	this.Form = function (format,submitCallback) {
		var self = this;
		var popup = self.CreatePopup();
		var title = self.CreateP(format.name);
		
		var inputs = [];
		format.fields.forEach(function (field) {
			// for each field in the form
			var row = self.CreateHorizontalList();
			var fieldTitle = self.CreateP(field.title);
			row.appendChild(fieldTitle);
			//------
			var input;
			if (field.type != "select") {
				input = document.createElement("INPUT");
				input.type = field.type;
			} else {
				input = document.createElement("SELECT");
				field.options.forEach(function (optionText) {
					var option = document.createElement("OPTION");
					option.innerHTML = optionText;
					option.value = optionText;
					input.appendChild(option);
				});
			}
			row.appendChild(input);
			inputs.push(input);
			//------
			popup.appendChild(row);
		});
		var submit = self.CreateButton("Submit");
		submit.onclick = function () {
			format.fields.forEach(function (field,i) {
				self.Close(popup);
				format.fields[i].value = inputs[i].value;
			});
			submitCallback(format);
		}
		popup.appendChild(submit);
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
	this.CreateHorizontalList = function () {
		var list = document.createElement("DIV");
		list.className = "list--horizontal";
		return list;
	}
	this.CreateCheckItem = function (innerHTML)  {
		var self = this;
		var list = self.CreateHorizontalList();
		var check = document.createElement("INPUT");
		check.onclick = function (event) {
			var test = "";
			event.stopPropagation();
		}
		check.name = "checklist";
		check.type = "checkbox";
		var text = self.CreateP(innerHTML);
		text.className = "stdMargin";
		list.appendChild(check);
		list.appendChild(text);
		return list;
	}
	//=========================================
	
	// clears everything and closes the blackout
	this.Close = function (popup) {
		var self = this;
		if (self.popupCount > 0) {
			// close the specified popup, if it exists
			if (self.blackout.contains(popup) && popup.style.display != "none") {
				// only close unonomouse popups
				if (!popup.hasAttribute("id")) {
					self.blackout.removeChild(popup);
				} else {
					popup.style.display = "none";
				}
				self.popupCount--;
			}
			// there are no open popups
			if (self.popupCount <= 0) {
				self.blackout.className = "blackout hidden";
			}
		}
	}
	this.CloseAll = function (closeLocked = false) {
		var self = this;
		var children = [];
		for (var i = 0; i < self.blackout.children.length; i++) {
			if ((!self.locked || closeLocked) || self.blackout.children[i] != self.locked) {
				children.push(self.blackout.children[i]);
			}
		}
		children.forEach(function (elem) {
			self.Close(elem);
		});
		/* for (var i = 0; i < children.length; i++) {
			if (!self.locked || children[i] != self.locked) {
				self.Close(children[i]);
			}
		} */
	}
	this.Open = function (popup) {
		var self = this;
		self.popupCount++;
		self.blackout.appendChild(popup);
		self.blackout.className = "blackout";
	}
	this.Lock = function (popup) {
		var self = this;
		self.locked = popup;
	}
	this.Unlock = function () {
		var self = this;
		self.locked = undefined;
	}
	// initializes the blackout container
	this.Initialize = function(blackout) {
		var self = this;
		self.blackout = blackout;
		self.blackout.onclick = function (event) {
			if (event.target == this) self.CloseAll(); 
			return false;
		}
	}
	
	this.Initialize(blackout);
}
function AddTooltip(element,tip) {
	element.title = tip;
}

function Timer(DOMelement) {
		// Timer
	this.timerStart;
	this.timerStop;
	this.timerTime;
	this.timerInterval;
	this.timerDirection = 1;
	this.DOMelement = DOMelement;
	
	this.Clear = function () {
		this.Stop();
		this.DOMelement.className = "silver";
		this.DOMelement.innerHTML = "--:--:--";
	}
	this.Start = function (time) {
		var self = this;
		self.timerDirection = 1;
		self.Stop();
		self.timerTime = (time ? time : new moment.duration("00:00:00"));
		self.DOMelement.innerHTML = pad(self.timerTime.hours(),2) + ":" + pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		self.timerInterval = setInterval(function () {
			self.timerTime.add(1,'s');
			self.DOMelement.innerHTML = pad(self.timerTime.hours(),2) + ":" + pad(self.timerTime.minutes(),2) + ":" + pad(self.timerTime.seconds(),2);
		},1000);
	}
	this.CountDown = function (minutes) {
		var self = this;
		self.timerDirection = -1;
		self.Stop();
		self.timerTime = moment.duration(minutes*60*1000, 'milliseconds');
		//self.DOMelement.innerHTML = pad(duration.hours(),2) + ":" + pad(duration.minutes(),2) + ":" + pad(duration.seconds(),2);
		self.timerInterval = setInterval(function () {
			self.timerTime = moment.duration(self.timerTime - 1000, 'milliseconds');
			if (self.timerTime.hours() < 0 || self.timerTime.minutes() < 0 || self.timerTime.seconds() < 0 ? "-" : "") {
				self.DOMelement.innerHTML = "-";
				self.DOMelement.className = "red shadow";
			} else {
				self.DOMelement.innerHTML = "";
				if (self.timerTime.hours() == 0 && self.timerTime.minutes() == 0 && self.timerTime.seconds() < 30) {
					self.DOMelement.className = "yellow";
				} else {
					self.DOMelement.className = "white";
				}
			}
			self.DOMelement.innerHTML += pad(Math.abs(self.timerTime.hours()),2) + ":" + pad(Math.abs(self.timerTime.minutes()),2) + ":" + pad(Math.abs(self.timerTime.seconds()),2);
		},1000);
	}
	this.Stop = function () {
		var self = this;
		clearInterval(self.timerInterval);
	}
	this.Resume = function () {
		if (this.timerDirection === 1) {
			this.Start(this.timerTime);
		} else if (this.timerDirection === -1) {
			this.CountDown(this.timerTime.minutes());
		}
	}
	this.Clear();
}
function ClearChildren(domElement) {
	while (domElement.lastChild) {
		domElement.remove(domElement.lastChild);
	}
}