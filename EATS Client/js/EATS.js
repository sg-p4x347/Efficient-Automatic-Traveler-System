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
		
		if ("WebSocket" in window)
            {
               console.log("WebSocket is supported by your Browser!");
               
               // Let us open a web socket
               var ws = new WebSocket("ws://localhost:8080/");
				
               ws.onopen = function()
               {
				   console.log("Connection is open...");
					// Web Socket is connected, send data using send()
					var submit = document.getElementById("submit");
					submit.onclick = function () {
						var message = document.getElementById("textBox").value;
						ws.send(message);
					}
               };
				ws.ondata = function (src, start, end) {
					var test = "fest";
				}
				ws.onmessage = function(messageEvent) {
					if (typeof messageEvent.data === "string"){
						console.log("received text data from the server: " + messageEvent.data);
						document.getElementById("heading").innerHTML = messageEvent.data;
					} else if (messageEvent.data instanceof Blob){
						console.log("Blob data received");
					}
				};
				
				ws.onclose = function()
			    {
				  // websocket is closed.
				  console.log("Connection is closed..."); 
			    };
            }
            
            else
            {
               // The browser doesn't support WebSocket
               alert("WebSocket NOT supported by your Browser!");
            }
	}
}