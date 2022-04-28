document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);
        var payload = jsonObj.payload;

        if (jsonObj.event === 'sendToPropertyInspector') {
            showHideSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            showHideSettings(payload.settings);
        }
    });
});

function refreshApplications() {
    var payload = {};
    payload.property_inspector = 'refreshApplications';
    sendPayloadToPlugin(payload);
}