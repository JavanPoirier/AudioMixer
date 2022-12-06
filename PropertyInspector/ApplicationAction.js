document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
});

function websocketOnMessage(evt) {
    // Received message from Stream Deck
    var jsonObj = JSON.parse(evt.data);
    var payload = jsonObj.payload;

    if (jsonObj.event === 'sendToPropertyInspector') {
        console.log('sendToPropertyInspector', payload);

        populateBlacklist(payload);
    }
    else if (jsonObj.event === 'didReceiveSettings') {
        console.log('didReceiveSettings', payload)

        loadConfiguration(payload.settings);
        populateBlacklist(payload.settings);
    }
    else {
        console.log("Ignored websocketOnMessage: " + jsonObj.event);
    }
}

function initPropertyInspector() {
    // Place to add functions
    prepareDOMElements(document);
    populateBlacklist(actionInfo.payload.settings);
}

function populateBlacklist(payload) {
    console.log('populateBlacklist', payload)
    const { blacklistedApplications } = payload;

    document.getElementById("blacklist").innerHTML = '';
    blacklistedApplications?.forEach(app => {
        document.getElementById("blacklist").innerHTML += `
            <li style="display:flex;justify-content:center;align-items:center;padding:2.5px;">
                <img width="15" height="15" src="data:image/png;base64,${app.processIcon}" style="margin-right:10px;"/>
                ${app.processName}
            </li>`
    })
}

function setStaticApp(clear) {
    var payload = {};

    var { value } = document.getElementById("staticApplicationSelector");
    payload.value = clear ? null : value;
    payload.property_inspector = 'setStaticApp';
    sendPayloadToPlugin(payload);
}

function toggleBlacklistApp() {
    let payload = {}
    var { value } = document.getElementById("blacklistApplicationSelector");

    payload.value = value;
    payload.property_inspector = 'toggleBlacklistApp';
    sendPayloadToPlugin(payload);
}

function refreshApplications() {
    let payload = {};

    payload.property_inspector = 'refreshApplications';
    sendPayloadToPlugin(payload);
}

function resetSettings() {
    let payload = {};

    payload.property_inspector = 'resetSettings';
    sendPayloadToPlugin(payload);
}

//function setSettings() {
//    var payload = {};
//    var elements = document.getElementsByClassName("sdProperty");

//    Array.prototype.forEach.call(elements, function (elem) {
//        var key = elem.id;
//        if (elem.classList.contains("sdCheckbox")) { // Checkbox
//            payload[key] = elem.checked;
//        }
//        else if (elem.classList.contains("sdFile")) { // File

//        }
//        else { // Normal value
//            payload[key] = elem.value;
//        }
//        console.log("Save: " + key + "<=" + payload[key]);
//    });
//    setSettingsToPlugin(payload);
//}

//function setSettingsToPlugin(payload) {
//    if (websocket && (websocket.readyState === 1)) {
//        const json = {
//            'event': 'setSettings',
//            'context': uuid,
//            'payload': payload
//        };
//        websocket.send(JSON.stringify(json));
//    }
//}