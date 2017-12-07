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
			if (property != "Name" && typeof obj[property] != "function") {
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
				self.DOMcontainer.appendChild(listHorizontal);
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