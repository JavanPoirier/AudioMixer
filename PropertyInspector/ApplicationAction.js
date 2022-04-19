document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    showHideSettings(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            let payload = jsonObj.payload;
            showHideSettings(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            let payload = jsonObj.payload;
            showHideSettings(payload.settings);
        }
    });
});

function setStaticApplication() {
    var payload = {};

    var { value } = document.getElementById("staticApplications");
    payload.value = value;
    payload.property_inspector = 'setStaticApplication';
    sendPayloadToPlugin(payload);
}

function refreshApplications() {
    let payload = {};
    payload.property_inspector = 'refreshApplications';
    sendPayloadToPlugin(payload);
}

function setSettings() {
    var payload = {};
    var elements = document.getElementsByClassName("sdProperty");

    Array.prototype.forEach.call(elements, function (elem) {
        var key = elem.id;
        if (elem.classList.contains("sdCheckbox")) { // Checkbox
            payload[key] = elem.checked;
        }
        else if (elem.classList.contains("sdFile")) { // File

        }
        else { // Normal value
            payload[key] = elem.value;
        }
        console.log("Save: " + key + "<=" + payload[key]);
    });
    setSettingsToPlugin(payload);
}

function setSettingsToPlugin(payload) {
    if (websocket && (websocket.readyState === 1)) {
        const json = {
            'event': 'setSettings',
            'context': uuid,
            'payload': payload
        };
        websocket.send(JSON.stringify(json));
    }
}