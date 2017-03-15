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
	/* // Common properties
	this.ID;
	this.itemCode;
	this.quantity;
	this.description;
	
	this.Initialize = function (obj) {
		var self = this;
		
		// Common properties
		self.ID = obj.ID;
		self.itemCode =  obj.itemCode;
		self.quantity = obj.quantity;
		self.description = obj.description;
	}
	this.Initialize(obj); */
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