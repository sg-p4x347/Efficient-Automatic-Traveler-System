// returns procedurally generated html from the provided data
function HTML(format) {

	return AddControlNode(format,null,function (parameters) {
	new InterfaceCall(parameters.callback,parameters);
	},true);
}

// helper for the html generation
function AddControlNode(node,parent,callback,highestLevel) {
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
				AddControlNode(innerNode,nodeElement,callback);
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
			nodeElement = new CreateButton(node.name);
			//button.Initialize(innerParams);
			nodeElement.className += " blackout__popup__controlPanel__node";
			break;
		case "Checkbox":
			nodeElement = document.createElement("INPUT");
			nodeElement.type = "checkbox";
			nodeElement.checked = node.value;
			//button.Initialize(innerParams);
			nodeElement.className += " blackout__popup__controlPanel__node";
			break;
		case "Selection":
			var selection = new PopupSelection(node.name,node.options,node.value);
			selection.Initialize(innerParams);
			nodeElement = selection.element;
			nodeElement.className += " blackout__popup__controlPanel__node";
			break;
		case "RadioButtons":
			var innerParams = node.returnParam;
			innerParams.callback = node.callback;
			var radioButtons = new PopupRadioButtons(node.name,node.options,node.value,callback);
			radioButtons.Initialize(innerParams);
			nodeElement = radioButtons.element;
			nodeElement.className += " blackout__popup__controlPanel__node";
			break;
		case "Row":
			var row = document.createElement("DIV");
			row.className = "list--horizontal blackout__popup__controlPanel__row";
			if (node.dividers) {row.className += " blackout__popup__controlPanel__row--dividers";}
			node.nodes.forEach(function (innerNode) {
				AddControlNode(innerNode,row,callback);
			});
			nodeElement = row;
			nodeElement.className += " blackout__popup__controlPanel__node";
			break;
		case "Column":
			var column = document.createElement("DIV");
			column.className = "blackout__popup__controlPanel__column";
			if (node.dividers) {column.className += " blackout__popup__controlPanel__column--dividers";}
			node.nodes.forEach(function (innerNode) {
				AddControlNode(innerNode,column,callback);
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
			return nodeElement;
		} else {
			parent.appendChild(nodeElement);
		}
		
	}
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