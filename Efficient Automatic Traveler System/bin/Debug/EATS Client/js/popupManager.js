function PopupManager(blackout) {
	this.blackout;
	this.locked;
	this.popupCount = 0;
	this.scrollYpos = {};
	this.scrollXpos = {};
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
		if (!self.Exists(id)) {
			var popup = document.getElementById(id);
			popup.style.display = "inherit";
			self.Open(popup);
		}
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
		
		
		var popup = self.CreatePopup();
		popup.title = "error";
		// the message
		var infoP = self.CreateP(message);
		infoP.classList.add("leftAlign");
		popup.appendChild(infoP);
		
		self.Lock(popup);
		self.Open(popup);
		self.CloseAll();
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
	this.Form = function (format,submitCallback,id) {
		var self = this;
		var element = document.getElementById(id);
		if (!element) {
			self.CloseAll();
		} else {
			ClearChildren(element);
		}
		var popup = self.CreatePopup(format.name,true);
		var messageBox = self.CreateP("");
		popup.appendChild(messageBox);
		var inputs = [];
		var radios = {};
		format.fields.forEach(function (field) {
			// for each field in the form
			var row = self.CreateHorizontalList();
			row.className = "list--horizontal";
			row.className = "justify-space-between";
			if (field.type != "radio") {
				var fieldTitle = self.CreateP(field.title);
				row.appendChild(fieldTitle);
			}
			//------
			var input;
			if (field.type != "radio") {
				if (field.type == "select") {
					input = document.createElement("SELECT");
					field.options.forEach(function (optionText) {
						var option = document.createElement("OPTION");
						option.innerHTML = optionText;
						option.value = optionText;
						input.appendChild(option);
					});
				} else if (field.type == "textarea") {
					input = document.createElement("textarea");
				} else if (field.type != "addBox") {
					input = document.createElement("INPUT");
					input.type = field.type;
					if (field.type == "number") {
						input.min = field.min;
						input.max = field.max;
					} else if (field.type == "text" && field.max > 0) {
						input.maxLength = field.max;
					} else if (field.type == "file") {
					}
				} else if (field.type == "addBox") {
					input = {
						value:[],
						type:field.type
					};
					row.appendChild( self.CreateAddBox(input.value));
				}
				// value
				if (input.type != "addBox") {
					if (field.type != "checkbox") {
						input.value = field.value;
					} else {
						input.checked = field.value;
					}
					input.onclick = function (evt) {
						evt.stopPropagation();
					}
					row.appendChild(input);
				}
				inputs.push(input);
			} else {
				// Radio
				var radio = {
					type: field.type,
					value: field.value
				};
				
				
				radio.onchange = function () {}
				var radioDiv = document.createElement("DIV");
				var fieldTitle = self.CreateP(field.title);
				fieldTitle.classList.add("form__radioTitle");
				radioDiv.appendChild(fieldTitle);
				field.options.forEach(function (optionText) {
					var option = document.createElement("INPUT");
					option.classList.add("form__radio");
					option.type = "radio";
					option.name = field.name;
					option.value = optionText;
					if (option.value == radio.value) option.checked = true;
					var title = self.CreateP(optionText);
					title.style.margin = "0px";
					var radioItem = self.CreateHorizontalList();
					radioItem.appendChild(option);
					radioItem.appendChild(title);
					
					radioDiv.appendChild(radioItem);
					option.onclick = function (evt) {
						evt.stopPropagation();
						if (this.checked) {
							radio.value = this.value;
							radio.onchange();
						}
						previous = this.value;
					}
				});
				inputs.push(radio);
				if (element) {
					element.appendChild(radioDiv);
				} else {
					popup.appendChild(radioDiv);
				}
			}
			//------
			if (element) {
				element.appendChild(row);
			} else {
				popup.appendChild(row);
			}
		});
		
		
		
		if (element == undefined) {
			var submit = self.CreateButton("Submit");
			submit.onclick = function () {
				var message = self.SubmitForm(format,inputs,submitCallback); 
				messageBox.innerHTML = message;
				if (!message || message == "") self.Close(popup);
			}
			popup.appendChild(submit);
			self.Open(popup);
		} else {
			inputs.forEach(function (input,i) {
				inputs[i].onchange = function () {
					messageBox.innerHTML = self.SubmitForm(format,inputs,submitCallback);
				}
			});
			// submit form to initialize
			self.SubmitForm(format,inputs,submitCallback);
		}
	}
	this.SubmitForm = function (format, inputs, submitCallback) {
		var fileExists = false;
		var done = false;
		var self = this;
		
		var message = "";
		format.rules.forEach(function (rule) {
			format.fields.forEach(function (fieldOne,i) {
				if (format.fields[i].name == rule.fieldOne
				&& ((!rule.negateOne && inputs[i].value == rule.fieldOneValue) || (rule.negateOne && inputs[i].value != rule.fieldOneValue))) {
					format.fields.forEach(function (fieldTwo,j) {
						if (format.fields[j].name == rule.fieldTwo) {
							if ((!rule.negateTwo && inputs[j].value == rule.fieldTwoValue) || (rule.negateTwo && inputs[j].value != rule.fieldTwoValue)) {
								// good
							} else {
								// bad
								message = rule.message;
							}
						}
					});
				}
			});
		});
		done:
		if (message && message != "") {
			return message;
		}
		format.fields.forEach(function (field,i) {
			
			if (inputs[i].type != "checkbox") {
				if (inputs[i].type != "file") {
					format.fields[i].value = inputs[i].value;
				} else if (inputs[i].files.length > 0) {
					var fr = new FileReader();
					fileExists = true;
					fr.onload = function (e) {
						format.fields[i].value = e.target.result;
						if (done) {
							submitCallback(format);
						}
					}
					format.fields[i].value = fr.readAsText(inputs[i].files[0]);
				} else {
					format.fields[i].value = "";
				}
			} else {
				format.fields[i].value = inputs[i].checked;
			}
		});
		done = true;
		if (!fileExists) {		
			submitCallback(format);
		}
		return "";
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
	this.ControlPanel = function (format,DOMparent) {
		var self = this;
		
		var popup;
		if (!DOMparent) {
			popup = self.CreatePopup(format.title,true);
			popup.id = format.title;
			self.AddControlNode(format.body,popup,function (parameters) {
			new InterfaceCall(parameters.callback,parameters);
			},true);
		} else {
			popup = DOMparent;
			ClearChildren(DOMparent);
			self.AddControlNode(format.body,popup,function (parameters) {
			new InterfaceCall(parameters.callback,parameters);
			});
		}
		self.SetScrollPos(popup);
		
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
		if (!DOMparent) {
			if (format.closeAll) {
				self.CloseAll();
			}
			self.Open(popup);
		}
	}	// displays an image
	this.Image = function (filename, timeout) {
		var self = this;
		var img = document.createElement("IMG");
		img.src = "./img/" + filename;
		if (timeout) {
			setTimeout(function() {self.Close(img);},timeout * 1000);
		}
		self.Open(img);
	}
	// helper for the control panel
	this.AddControlNode = function (node,parent,callback,highestLevel) {
		var self = this;
		
		var nodeElement;
		
		switch (node.type) {
			case "Expand":
				var expand = document.createElement("DIV");
				expand.classList.add("expand");
				expand.onclick = function () {
					if (parent.classList.contains("focused")) {
						parent.classList.remove("focused");
						expand.classList.remove("collapse");
						expand.classList.add("expand");
					} else {
						parent.classList.add("focused");
						expand.classList.remove("expand");
						expand.classList.add("collapse");
					}
				}
				parent.appendChild(expand);
				break;
			case "NodeList":
				nodeElement = document.createElement(node.DOMtype);
				nodeElement.innerHTML = node.innerHTML;
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,nodeElement,callback);
				});
				break;
			case "Node":
				nodeElement = document.createElement(node.DOMtype);
				nodeElement.innerHTML = node.innerHTML;
				break;
			case "TextNode": 
				nodeElement = document.createElement(node.DOMtype);
				nodeElement.innerHTML = node.text;
				/* if (node.color != "black") {
					nodeElement.style.textShadow = "1px 1px 1px black";
				} */
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "Button":
				//var eventListener = node.EventListeners[0];
				nodeElement = new self.CreateButton(node.name);
				//button.Initialize(self,innerParams);
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "Checkbox":
				nodeElement = document.createElement("INPUT");
				nodeElement.type = "checkbox";
				nodeElement.checked = node.value;
				//button.Initialize(self,innerParams);
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "Selection":
				var selection = new PopupSelection(node.name,node.options,node.value);
				selection.Initialize(self);
				nodeElement = selection.element;
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "RadioButtons":
				var innerParams = node.returnParam;
				innerParams.callback = node.callback;
				var radioButtons = new PopupRadioButtons(node.name,node.options,node.value,callback);
				radioButtons.Initialize(self,innerParams);
				nodeElement = radioButtons.element;
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "Row":
				var row = self.CreateHorizontalList();
				row.className = "blackout__popup__controlPanel__row";
				if (node.dividers) {row.className += " blackout__popup__controlPanel__row--dividers";}
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,row,callback);
				});
				nodeElement = row;
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
			case "Column":
				var column = document.createElement("DIV");
				column.className = "blackout__popup__controlPanel__column";
				if (node.dividers) {column.className += " blackout__popup__controlPanel__column--dividers";}
				node.nodes.forEach(function (innerNode) {
					self.AddControlNode(innerNode,column,callback);
				});
				nodeElement = column;
				nodeElement.className += " blackout__popup__controlPanel__node";
				break;
		}
		if (nodeElement) {
			node.styleClasses.forEach(function (styleClass) {
				nodeElement.className += " " + styleClass;
			});
			if (node.type == "Checkbox") {
				nodeElement.onclick = function (event) {event.stopPropagation();}
			}
			node.eventListeners.forEach(function (evtListener) {
				nodeElement.addEventListener(evtListener.type,function (evt) {
					if (node.type == "Selection") {
						evtListener.returnParam.value = nodeElement.value;
					} else if (node.type == "Checkbox") {
						evtListener.returnParam.value = nodeElement.checked;
					}
					new InterfaceCall(evtListener.callback,evtListener.returnParam);
					evt.stopPropagation();
				});
			});
			if (node.style) {
				for (var style in node.style) {
					nodeElement.style[style] = node.style[style];
				}
			}
			if (node.id) {
				nodeElement.id = node.id;
			}
		
			if (highestLevel) {
				nodeElement.style.overflowX = "auto";
				nodeElement.style.overflowY = "auto";
			}
			parent.appendChild(nodeElement);
			// Script
			if (node.script) {
				// call the script function, pass the element as the parameter
				if (window[node.script]) {
					window[node.script](nodeElement);
				}
			}
		}
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
	// displays 
	
	
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
	this.CreateCheckItem = function (innerHTML,callback)  {
		var self = this;
		var list = self.CreateHorizontalList();
		var check = document.createElement("INPUT");
			
		check.onclick = function (event) {
			if (callback) {callback(check.checked);}
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
	this.CreateAddBox = function (listArray) {
		var addBox = document.createElement("FORM");
		addBox.style.display = "flex";
		addBox.className = "mediumBorder";
		var list = document.createElement("UL");
		list.style.display = "flex";
		var input = document.createElement("INPUT");
		input.type = "textbox";
		var add = document.createElement("Button");
		add.innerHTML = "Add";
		add.className = "dark button";
		
		add.onclick = function () {
			var item = document.createElement("LI");
			item.innerHTML = input.value;
			listArray.push(input.value);
			list.appendChild(item);
		}
		
		add.type = "submit";
		addBox.appendChild(list);
		addBox.appendChild(add);
		addBox.appendChild(input);
		
		
		return addBox;
	}
	//=========================================
	
	// sets scroll positions for elements with ids
	this.SetScrollPos = function (element) {
		var self = this;
		if (element.id) {
			// reload scroll position
			if (element.id in self.scrollYpos) {
				element.scrollTop = self.scrollYpos[element.id];
			}
			if (element.id in self.scrollXpos) {
				element.scrollLeft = self.scrollXpos[element.id];
			}
			// add this id to the list of scroll positions
			element.onscroll = function () {
				self.scrollYpos[element.id] = element.scrollTop;
				self.scrollXpos[element.id] = element.scrollLeft;
			}
		}
		// recursivley set scroll positions for child elements with IDs
		for (var i = 0; i < element.children.length; i++) {
			self.SetScrollPos(element.children[i]);
		}
	}
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
		if (self.Exists("updatingPopup")) {
			self.Close(document.getElementById("updatingPopup"));
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

function PopupSelection(name, options, value) {
	PopupControl.call(this, name);
	this.options = options;
	this.value = value;
}
// calls the callback with: callback(object,value);
PopupSelection.prototype.Initialize = function (popupManager) {
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