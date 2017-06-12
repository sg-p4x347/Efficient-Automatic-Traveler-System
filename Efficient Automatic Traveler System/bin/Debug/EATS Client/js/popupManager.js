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
			var close = self.AddCloseBtn(popup);
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
		self.CloseAll();
		var popup = self.CreatePopup(format.name,true);
		var inputs = [];
		format.fields.forEach(function (field) {
			// for each field in the form
			var row = self.CreateHorizontalList();
			row.className = "list--horizontal";
			row.className = "justify-space-between";
			var fieldTitle = self.CreateP(field.title);
			row.appendChild(fieldTitle);
			//------
			var input;
			if (field.type != "select") {
				input = document.createElement("INPUT");
				input.type = field.type;
				if (field.type == "number") {
					input.min = field.min;
					input.max = field.max;
				}
			} else {
				input = document.createElement("SELECT");
				field.options.forEach(function (optionText) {
					var option = document.createElement("OPTION");
					option.innerHTML = optionText;
					option.value = optionText;
					input.appendChild(option);
				});
			}
			input.value = field.value;
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
	// displays a search box; calls a callback with the search phrase
	this.Search = function (message,callback) {
		var self = this;
		var popup = self.CreatePopup();
		var title = self.CreateP(message);
		popup.appendChild(title);
		var searchBox = document.createElement("INPUT");
		searchBox.type = "text";
		searchBox.className = "oneEM";
		popup.appendChild(searchBox);
		var submit = self.CreateButton("Search");
		submit.onclick = function () {
			self.Close(popup);
			callback(searchBox.value);
		}
		popup.appendChild(submit);
		self.Open(popup);
	}
	// displays a list view, calls a callback when an item in the list is selected
	this.ListView = function (title, list, displayField, itemSelectCallback) {
		var self = this;
		var popup = self.CreatePopup(title);
		list.forEach(function (item) {
			var itemElement = document.createElement("DIV");
			itemElement.innerHTML = item[displayField];
			itemElement.className = "blackout__popup__listView__item";
			itemElement.onclick = function () {
				itemSelectCallback(item);
			}
			popup.appendChild(itemElement);
		});
		self.Open(popup);
	}
	// displays a custom popup that contains a custom table of display fields and a custom set of controls
	this.ObjectViewer = function (title, displayFields, object, controls) {
		var self = this;
		var popup = self.CreatePopup(title,true);
		var horizontal = self.CreateHorizontalList();
		var fieldsTable = self.CreateTable(displayFields,object);
		horizontal.appendChild(fieldsTable);
		var controlDiv = document.createElement("DIV");
		controls.forEach(function (control) {
			control.Initialize(self,object);
			controlDiv.appendChild(control.element);
		});
		horizontal.appendChild(controlDiv);
		popup.appendChild(horizontal);
		self.Open(popup);
	}
	// displays a dropdown selection, calls a callback with the chosen value
	this.Selection = function (title, options, callback) {
		var self = this;
		var popup = self.CreatePopup(title,true);
		var select = document.createElement("SELECT");
		select.className = "dark oneEM";
		// add the options
		options.forEach(function (optionValue) {
			var option = document.createElement("OPTION");
			option.innerHTML = optionValue;
			option.value = optionValue;
			
			select.appendChild(option);
		});
		select.value = undefined;
		select.onchange = function () {
			callback(select.value);
			self.Close(popup);
		}
		popup.appendChild(select);
		self.Open(popup);
	}
	// displays a procedurally generated control panel from the format provided by the server
	this.ControlPanel = function (format) {
		var self = this;
		var popup = self.CreatePopup(format.title,true);
		popup.id = format.title;
		self.AddControlNode(format.body,popup,function (parameters) {
			new InterfaceCall(parameters.callback,parameters);
		},true);
		/* var horizontal = self.CreateHorizontalList();
		var fieldsTable = self.CreateTable(displayFields,object);
		horizontal.appendChild(fieldsTable); */
		/* var controlDiv = document.createElement("DIV");
		controls.forEach(function (control) {
			control.Initialize(self,object);
			controlDiv.appendChild(control.element);
		});
		horizontal.appendChild(controlDiv);
		popup.appendChild(horizontal); */
		self.Open(popup);
	}
	// helper for the control panel
	this.AddControlNode = function (node,parent,callback,highestLevel) {
		var self = this;
		
		var nodeElement;
		switch (node.type) {
			case "NodeList":
				nodeElement = document.createElement(node.DOMtype);
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,nodeElement,callback);
				});
				break;
			case "Node":
				nodeElement = document.createElement(node.DOMtype);
				break;
			case "TextNode": 
				nodeElement = document.createElement(node.DOMtype);
				nodeElement.innerHTML = node.text;
				/* if (node.color != "black") {
					nodeElement.style.textShadow = "1px 1px 1px black";
				} */
				break;
			case "Button":
				var innerParams = node.returnParam;
				innerParams.callback = node.callback;
				var button = new PopupButton(node.name,callback);
				button.Initialize(self,innerParams);
				nodeElement = button.element;
				break;
			case "Selection":
				var innerParams = node.returnParam;
				innerParams.callback = node.callback;
				var selection = new PopupSelection(node.name,node.options,node.value,callback);
				selection.Initialize(self,innerParams);
				nodeElement = selection.element;
				break;
			case "RadioButtons":
				var innerParams = node.returnParam;
				innerParams.callback = node.callback;
				var radioButtons = new PopupRadioButtons(node.name,node.options,node.value,callback);
				radioButtons.Initialize(self,innerParams);
				nodeElement = radioButtons.element;
				break;
			case "Row":
				var row = self.CreateHorizontalList();
				row.className = "blackout__popup__controlPanel__row";
				if (node.dividers) {row.className += " blackout__popup__controlPanel__row--dividers";}
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,row,callback);
				});
				nodeElement = row;
				break;
			case "Column":
				var column = document.createElement("DIV");
				column.className = "blackout__popup__controlPanel__column";
				if (node.dividers) {column.className += " blackout__popup__controlPanel__column--dividers";}
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,column,callback);
				});
				nodeElement = column;
				break;
		}
		nodeElement.className += " blackout__popup__controlPanel__node";
		node.styleClasses.forEach(function (styleClass) {
			nodeElement.className += " " + styleClass;
		});
		if (node.style) {
			for (var style in node.style) {
				nodeElement.style[style] = node.style[style];
			}
		}
		if (highestLevel) {
			nodeElement.style.overflowX = "auto";
			nodeElement.style.overflowY = "auto";
		}
		parent.appendChild(nodeElement);
	}
	
	// displays an animated loading GIF
	this.Loading = function () {
		var self = this;
		var popup = document.createElement("IMG");
		popup.id = "loading";
		popup.className = "blackout__popup__loadingGIF blackout__popup--center";
		popup.src = "./img/loading.gif";
		self.Open(popup);
	}
	//=========================================
	// CREATE MODULAR DOM OBJECTS
	
	// initializes and returns a new popup container
	this.CreatePopup = function (title,close) {
		var self = this;
		var popup = document.createElement("DIV");
		popup.className = "blackout__popup";
		// Close button
		if (close) {
			/* var close = self.CreateButton("Close");
			close.classList.remove("dark");
			close.className += " yellowBack";
			close.removeClass
			close.onclick = function () {self.Close(popup);}
			popup.insertBefore(close,popup.firstChild); */
			self.AddCloseBtn(popup);
		}
		if (title !== undefined) {
			var title = this.CreateP(title);
			title.className = "blackout__popup__title emboss";
			popup.appendChild(title);
		}
		return popup;
	}
	this.AddCloseBtn = function (popup) {
		var self = this;
		var close = document.createElement("DIV");
		close.className = "blackout__popup__closeImg";
		close.onclick = function () {self.Close(popup);}
		popup.insertBefore(close,popup.firstChild);
		return close;
	}
		
	this.CreateP = function (innerHTML) {
		var p = document.createElement("P");
		p.innerHTML = innerHTML;
		return p;
	}
	this.CreateButton = function (innerHTML) {
		var button = document.createElement("DIV");
		button.className = "dark button";
		if (innerHTML !== undefined) button.innerHTML = innerHTML;
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
	// displays a formatted table created from the fields object provided
	this.CreateTable = function (fields,object) {
		var table = document.createElement("TABLE");
		table.className = "blackout__popup__table";
		for (var fieldName in fields) {
			var row = document.createElement("TR");
			// name
			var name = document.createElement("TD");
			name.style.textAlign = "left";
			name.innerHTML = fieldName + ':';
			
			row.appendChild(name);
			if (typeof fields[fieldName] === "object") {
				// control
				
			} else {
				// value
				var value = document.createElement("TD");
				value.style.textAlign = "right";
				value.style.color = "#FFFFFF";
				value.className = "shadow";
				value.innerHTML = fields[fieldName];
				row.appendChild(value);
				
				table.appendChild(row);
			}
		}
		return table;
	}
	this.CreateDateInput = function () {
		var input = document.createElement("INPUT");
		input.type = "date";
		return input;
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
		if (self.Exists("loading")) {
			self.Close(document.getElementById("loading"));
		}
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

function PopupControl(name,callback) {
	this.name = name;
	this.element;
	this.callback = callback;
}
PopupControl.prototype.Initialize = function (popupManager, object) {}
function PopupButton(name, callback) {
	PopupControl.call(this, name, callback);
}
// calls the callback with: callback(object);
PopupButton.prototype.Initialize = function (popupManager, object) {
	var self = this;
	self.element = popupManager.CreateButton(this.name);
	self.element.onclick = function () {
		self.callback(object);
	}
}

function PopupCheckbox(name, callback) {
	PopupControl.call(this, name, callback);
}
// calls the callback with: callback(object);
PopupCheckbox.prototype.Initialize = function (popupManager, object) {
	var self = this;
	self.element = popupManager.CreateCheckItem(this.name);
	self.element.onchange = function () {
		self.callback(object);
	}
}

function PopupSelection(name, options, value, callback) {
	PopupControl.call(this, name, callback);
	this.options = options;
	this.value = value;
}
// calls the callback with: callback(object,value);
PopupSelection.prototype.Initialize = function (popupManager, object) {
	var self = this;
	self.element = document.createElement("SELECT");
	self.element.className = "dark oneEM";
	// add the options
	self.options.forEach(function (optionValue) {
		var option = document.createElement("OPTION");
		option.innerHTML = optionValue;
		option.value = optionValue;
		
		self.element.appendChild(option);
	});
	self.element.value = self.value;
	self.element.onchange = function () {
		object.value = self.element.value;
		self.callback(object);
	}
}
function PopupRadioButtons(name, options, value, callback) {
	PopupControl.call(this, name, callback);
	this.options = options;
	this.value = value;
}
// calls the callback with: callback(object,value);
PopupRadioButtons.prototype.Initialize = function (popupManager, object) {
	var self = this;
	self.element = document.createElement("DIV");
	self.element.className = "dark oneEM";
	// add the options
	self.options.forEach(function (optionValue) {
		var row = document.createElement("DIV");
		row.className = "list--horizontal";
		var radioButton = document.createElement("INPUT");
		radioButton.type = "radio";
		row.appendChild(radioButton);
		var desc = document.createElement("P");
		desc.innerHTML = optionValue;
		row.appendChild(desc);
		self.element.appendChild(row);
		radioButton.onchange = function () {
			if (!this.checked) {
				object.value = optionValue;
				self.callback(object);
			}
		}
	});
	
}