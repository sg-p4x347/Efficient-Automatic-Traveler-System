// returns procedurally generated html from the provided data
function HTML(format) {
	var body = document.createElement("DIV");

	body.id = format.title;
	AddControlNode(format.body,body,function (parameters) {
	new InterfaceCall(parameters.callback,parameters);
	},true);
}

// helper for the html generation
function AddControlNode(node,parent,callback,highestLevel) {
	var = this;
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
			selection.Initialize(;
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
			var row = CreateHorizontalList();
			row.className = "blackout__popup__controlPanel__row";
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
		}
		parent.appendChild(nodeElement);
	}
}