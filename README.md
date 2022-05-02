# Stream Deck Audio Mixer

A dynamic volume mixer with process icons that reflects the windows volume mixer. Control each independent process' volume as applications open and close. No need to statically assign a button to
a single application, the application placeholder actions will update accordingly.

**IMPORTANT:** To have the plugin work best and provide as many process icons as possible, please ensure you are running your Stream Deck app as an administrator. See "Troubleshooting" for more details.

**Tutorial Video:** Click to watch
<a href="https://www.youtube.com/watch?v=26_o4-roURs" title="Link Title"><img src="https://github.com/JavanPoirier/AudioMixer/blob/master/Previews/1-preview.png" alt="Click to watch" /></a>

---

## Features
### Inline Controls
Have a small stream deck? Instead of dedicating buttons to volume controls, only show controls when you select an application you want to control.

### Static Applications
Want an application to stay on the same key regardless if it's open or not? Make it static.
		
### Application Blacklisting
Have an application you know you will never want to control? Add it to your blacklist to prevent it from ever being displayed on your Stream Deck.
		
---

## Controls
### Blacklist An App 
No need to head into Stream Deck and manually blacklist it. Hold down the key for 1.5 seconds and it will be
auto-magically added to your blacklist.

---

## Known Limitations
### Only supports a single page
At the moment for V1, processes do not move on to consequative pages once a page is full. This behaviour is something I would like to add, however it would
be a user setting as this can also be seen as unintended behaviour.

###  You can only have one of the same applications as static
You can have multiple static applications, just not the same application. This is a limitation of how the code was written. 
This shouldn't be an issue for most, let me know if it is.

### A single process with multiple audio steams are conglomerated.
This is due to the fact there is no way to discriminate between them during the applications lifecycle.

---

## Known Issues
**Icons flicker and jump around.** <br/>

This occurs due to each action needing to check the state of others, along with the lack of tracking which would be resolved by forcing left to right. 
This should not occur frequently and lasts for only a second.


**Application's not showing/disapearing when launched/closed.** <br/>

While testing I noticed this occuring with some apps, notably Spotify. Some apps will only notify the OS
of their audio intentions once they actually start playing, not on open. So keep this in mind. You can see this for yourself, if you open up the Windows Volume Mixer.
A possible solution to this would require quite the re-write, of watching applications rather than just audio sessions.

---

## Troubleshooting

**Some applications show as default app icons**

Either the audio session actually does not have an associated icon, or more likely, you are not running your Stream Deck as an administrator.
<img src="https://github.com/JavanPoirier/AudioMixer/blob/master/Images/StreamDeckRunAsAdmin.png" alt="StreamDeckRunAsAdmin" />

---

## TODO
- Changing default output device.
- Add Application Whitelist (Application priority)
- Local volume step setting for static actions
- Alight applications left to right. (**Note to Self**: Currently not occuring as the application actions array index is not reflective of the key position)

---

## Getting Started

To use:
1. Right click the project and choose "Manage Nuget Packages"
2. Choose the restore option in the Nuget screen (or just install the latest StreamDeck-Tools from Nuget)
3. Update the manifest.json file with the correct details about your plugin
4. Modify PluginAction.cs as needed (it holds the logic for your plugin)
5. Modify the PropertyInspector\PluginActionPI.html and PropertyInspector\PluginActionPI.js as needed to show field in the Property Inspector
6. Before releasing, change the Assembly Information (Right click the project -> Properties -> Application -> Assembly Information...)

For help with StreamDeck-Tools:
	Discord Server: http://discord.barraider.com

Resources:
* StreamDeck-Tools samples and tutorial: https://github.com/BarRaider/streamdeck-tools
* EasyPI library (for working with Property Inspector): https://github.com/BarRaider/streamdeck-easypi

