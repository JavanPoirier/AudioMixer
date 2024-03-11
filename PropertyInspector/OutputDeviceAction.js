document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");

     refreshDevices()
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
        populateStaticDevice(payload.settings);
        populateBlacklist(payload.settings);
        populateWhitelist(payload.settings);
    }
    else {
        console.log("Ignored websocketOnMessage: " + jsonObj.event);
    }
}

function initPropertyInspector() {
    console.log("initPropertyInspector")

    // Place to add functions
    prepareDOMElements(document);
}

function populateStaticDevice(payload) {
    console.log('populateStaticDevice', payload);
    const { staticDevice } = payload;

    if (!staticDevice?.processName) return;
    document.getElementById("staticOutputDevicesSelector").value = staticDevice.displayName;
}

function populateBlacklist(payload) {
    console.log('populateBlacklist', payload)
    const { blacklistedOutputDevices } = payload;

    document.getElementById("blacklist").innerHTML = '';
    blacklistedOutputDevices?.forEach(device => {
        document.getElementById("blacklist").innerHTML += `
            <li style="display:flex;justify-content:center;align-items:center;padding:2.5px;">
                 ${device.displayName}
            </li>`
    })
}

function populateWhitelist(payload) {
    console.log('populateWhitelist', payload)
    const { whitelistedOutputDevices } = payload;

    document.getElementById("whitelist").innerHTML = '';
    whitelistedOutputDevices?.forEach(device => {
        document.getElementById("whitelist").innerHTML += `
            <li style="display:flex;justify-content:center;align-items:center;padding:2.5px;">
                 ${device.displayName}
            </li>`
    })
}

function setStaticDevice(clear) {
    var payload = { action: "outputDevice" };
    var { value } = document.getElementById("staticOutputDevicesSelector");

    payload.value = clear ? null : value;
    payload.property_inspector = 'setStaticDevice';
    sendPayloadToPlugin(payload);
}

function toggleBlacklistedDevice() {
    var payload = { action: "outputDevice" };
    var { value } = document.getElementById("blacklistedOutputDevicesSelector");

    payload.value = value;
    payload.property_inspector = 'toggleBlacklistedDevice';
    sendPayloadToPlugin(payload);
}

function toggleWhitelistedDevice() {
    var payload = { action: "outputDevice" };
    var { value } = document.getElementById("whitelistedOutputDevicesSelector");

    payload.value = value;
    payload.property_inspector = 'toggleWhitelistedDevice';
    sendPayloadToPlugin(payload);
}

function refreshDevices() {
    var payload = { action: "outputDevice" };

    payload.property_inspector = 'refreshDevices';
    sendPayloadToPlugin(payload);
}

function resetGlobalSettings() {
    var payload = { action: "outputDevice" };

    payload.property_inspector = 'resetGlobalSettings';
    sendPayloadToPlugin(payload);
}

function resetSettings() {
    var payload = { action: "outputDevice" };

    payload.property_inspector = 'resetSettings';
    sendPayloadToPlugin(payload);
}