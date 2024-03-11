﻿document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");

    refreshApplications();
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
        populateStaticApp(payload.settings);
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

function populateStaticApp(payload) {
    console.log('populateStaticApp', payload);
    const { staticApplication } = payload;

    if (!staticApplication?.processName) return;
    document.getElementById("staticApplicationsSelector").value = staticApplication.processName;
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

function populateWhitelist(payload) {
    console.log('populateWhitelist', payload)
    const { whitelistedApplications } = payload;

    document.getElementById("whitelist").innerHTML = '';
    whitelistedApplications?.forEach(app => {
        document.getElementById("whitelist").innerHTML += `
            <li style="display:flex;justify-content:center;align-items:center;padding:2.5px;">
                <img width="15" height="15" src="data:image/png;base64,${app.processIcon}" style="margin-right:10px;"/>
                ${app.processName}
            </li>`
    })
}

function setStaticApp(clear) {
    var payload = { action: "application" };
    var { value } = document.getElementById("staticApplicationsSelector");

    payload.value = clear ? null : value;
    payload.property_inspector = 'setStaticApp';
    sendPayloadToPlugin(payload);
}

function toggleBlacklistedApp() {
    var payload = { action: "application" };
    var { value } = document.getElementById("blacklistedApplicationsSelector");

    payload.value = value;
    payload.property_inspector = 'toggleBlacklistedApp';
    sendPayloadToPlugin(payload);
}

function toggleWhitelistedApp() {
    var payload = { action: "application" };
    var { value } = document.getElementById("whitelistedApplicationsSelector");

    payload.value = value;
    payload.property_inspector = 'toggleWhitelistedApp';
    sendPayloadToPlugin(payload);
}

function refreshApplications() {
    var payload = { action: "application" };

    payload.property_inspector = 'refreshApplications';
    sendPayloadToPlugin(payload);
}

function resetGlobalSettings() {
    var payload = { action: "application" };

    payload.property_inspector = 'resetGlobalSettings';
    sendPayloadToPlugin(payload);
}

function resetSettings() {
    var payload = { action: "application" };

    payload.property_inspector = 'resetSettings';
    sendPayloadToPlugin(payload);
}