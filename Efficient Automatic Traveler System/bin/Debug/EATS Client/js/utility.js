function InterfaceCall(methodName, parameters, target) {
	this.interfaceMethod = methodName;
	this.parameters = (parameters === undefined ? "" : parameters);
	this.interfaceTarget = (target === undefined ? "This" : target);
	this.Call = function () {
		application.websocket.send(JSON.stringify(this));
	}
	this.Call();
}

/* function InlineCall(methodName, parameters, callback,callID) {
	this.interfaceMethod = methodName;
	this.parameters = (parameters === undefined ? "" : parameters);
	this.callID = callID;
	this.callback = callback;
	
} */
function TravelerItem(obj) {
	obj.queueType = "travelerItem";
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
				if (self.forInventory) {
					self.colorClass = "cyanBack";
				} else {
					self.colorClass = "blueBack";
				}
			} else {
				self.colorClass = "ghostBack";
			}
			break;
			case "InProcess": self.colorClass = "redBack"; break;
			case "PostProcess": self.colorClass = "greenBack"; break;
		}
		DOMqueueItem.className = "queue__item twoEM ";
		if (self.selected) {
			DOMqueueItem.className += " selected";
		}
		DOMqueueItem.className += " " + self.colorClass;
		DOMqueueItem.innerHTML = (self.type == "TableBox" ? pad(self.parentTravelers[0],6) : self.sequenceID) + "<br>";
		// Icon -------------------------------------------
		var path = "./img/";
		if (self.type == "Table") {
			path += self.shape + ".png";
		} else if (self.type == "TableBox") {
			path += "box.png";
		}
		DOMqueueItem.style.backgroundImage = 'url("' + path + '")';
		// ITEM CODE-------------------------------------------
		var itemCode = document.createElement("SPAN");
			itemCode.className = "queue__item__desc beige";
		if (self.type == "TableBox") {
			itemCode.innerHTML = "Table Box";
		} else {
			itemCode.innerHTML = self.itemCode;
		}
		
		DOMqueueItem.appendChild(itemCode);
		
		
		
		/* DOMqueueItem.onclick = function () {
			//----------INTERFACE CALL-----------------------
			var message = new InterfaceCall("LoadTravelerAt",
			{
				travelerID: self.ID,
				station: station
			});
			application.websocket.send(JSON.stringify(message));
			//-----------------------------------------------
			//self.PromptAction(traveler);
		} */
		self.DOMqueueItem = DOMqueueItem;
		return DOMqueueItem;
	}
	obj.Select = function(state) {
		obj.selected = state;
		if (obj.checkBox) obj.checkBox.checked = state;
		obj.DOMqueueItem.className = (state ? "queue__item twoEM selected " + obj.colorClass
			: "queue__item twoEM " + obj.colorClass);
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
		this.DOMelement.className = "bold white shadow";
		this.DOMelement.innerHTML = "--:--:--";
	}
	this.Start = function (time) {
		var self = this;
		self.timerDirection = 1;
		self.Stop();
		self.timerTime = (time ? new moment.duration(time,'minutes') : new moment.duration("00:00:00"));
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
				self.DOMelement.className = "bold red shadow";
			} else {
				self.DOMelement.innerHTML = "";
				if (self.timerTime.hours() == 0 && self.timerTime.minutes() == 0 && self.timerTime.seconds() < 30) {
					self.DOMelement.className = "bold yellow shadow";
				} else {
					self.DOMelement.className = "bold white shadow";
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
			this.Start(this.timerTime.asMinutes());
		} else if (this.timerDirection === -1) {
			this.CountDown(this.timerTime.asMinutes());
		}
	}
	this.Clear();
}
function Breakify(string) {
	var newString = "";
	for (var i = 0; i < string.length; i++) {
		if (string.charAt(i) == '\n') {
			newString += "</br>";
		} else {
			newString += string.charAt(i);
		}
	}
	return newString;
}
function ClearChildren(domElement) {
	while (domElement.hasChildNodes()) {
		domElement.removeChild(domElement.lastChild);
	}
}
Array.prototype.ArrayFromProperty = function (property) {
	var subArray = [];
	this.forEach(function (item) {
		if (item.hasOwnProperty(property)) {
			subArray.push(item[property]);
		}
	});
	return subArray;
}
Array.prototype.Where = function (callbackTest,property) {
	var subArray = [];
	this.forEach(function (item) {
		if (property === undefined) {
			if (callbackTest(item)) subArray.push(item);
		} else if (item.hasOwnProperty(property)) {
			if (callbackTest(item[property])) subArray.push(item);
		}
	});
	return subArray;
}
function Selection(array,callback) {
	var selection = document.createElement("SELECTION");
	array.forEach(function (elem) {
		var option = document.createElement("OPTION");
		option.innerHTML = elem;
		option.value = elem;
		selection.appendChild(option);
	});
	selection.value = "";
	selection.onchange = function () {
		callback(selection.value);
	}
	return selection;
}	

function EditHTML(params) {
	var element = document.getElementById(params.id);
	if (element) {
		if (element[params.method] != undefined) {
			element[params.method](params.params);
		} else {
			console.log(element.id + " does not have a property or method: " + params.method);
		}
	} else {
		console.log("No element by id: " + params.id);
	}
}
function AddStyle(params) {
	var element = document.getElementById(params.id);
	if (element) {
		element.classList.add(params.style);
	} else {
		console.log("No element by id: " + params.id);
	}
}
function RemoveStyle(params) {
	var element = document.getElementById(params.id);
	if (element) {
		element.classList.remove(params.style);
	} else {
		console.log("No element by id: " + params.id);
	}
}