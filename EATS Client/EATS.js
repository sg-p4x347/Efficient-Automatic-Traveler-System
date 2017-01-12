// Script: Client-side Javascript for recieving and displaying information from the Efficient Automatic Traveler System (EATS) server
// Developer: Gage Coates
// Date started: 1/12/17

var application = new Application();

// gets called once the html is loaded
function Initialize() {
	application.Initialize();
}
function Application () {
	// update and render
	this.Render = function () {
		
	};
	this.Update = function (elapsed) {
		
	};
	// initialize html and application components
	this.Initialize = function () {
		var self = this;
		
		socket = new Websocket("127.0.0.1","");
		socket;
	}
	// Classes
	function Websocket (url, protocols) {
		this.url = url;
		this.protocols = protocols;
	}
}