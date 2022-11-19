document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
});

function websocketOnMessage(evt) {
    // Received message from Stream Deck
    var jsonObj = JSON.parse(evt.data);
    var payload = jsonObj.payload;

    if (jsonObj.event === 'sendToPropertyInspector') {
        console.log('sendToPropertyInspector', payload);
    }
    else if (jsonObj.event === 'didReceiveSettings') {
        console.log('didReceiveSettings', payload)

        loadConfiguration(payload.settings);
    }
    else {
        console.log("Ignored websocketOnMessage: " + jsonObj.event);
    }
}

function initPropertyInspector() {
    // Place to add functions
    prepareDOMElements(document);
}