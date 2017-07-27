// Purpose: Stores context-dependent scripts that are bound to html objects by the server
// Developer: Gage Coates
// Date: 7/27/2017


// handles the selection of queue items
function Script_QueueItem(queueItem) {
	var checkbox = queueItem.childNodes[0];
	if (checkbox) {
		checkbox.onchange = function (evt) {
			if (this.checked) {
				queueItem.classList.add("selected");
			} else {
				queueItem.classList.remove("selected");
			}
			new InterfaceCall("SelectChanged",{travelerID:parseInt(queueItem.id),value:this.checked});
			evt.stopPropagation();
		}
	}
}