## Streamdeck Audio Mixer

A dynamic volume mixer that reflects the windows volume mixer with process icons. Control each independant process' volume as they open and close. No need to statically assign a button to
a single application. The Stream Deck application placeholders will update accordingly. You can set static applications if you would like.

### Features
	- Inline Controls
		Have a small stream deck? Instead of dedicating buttons to volume controls, only show controls when you select an application you want to control.

	- Static Applications
		Want a application to stay on the same key regardless if it's open or not? Make it static.
		
	- Application Blacklisting
		Have an application you know you will never want to control? Add it to your blacklist to prevent it from ever being displayed on your Stream Deck.
		
## Controls
	- Blacklist An App
		No need to head into Stream Deck and manually blacklist it. Hold down the key for 1.5 seconds and it will be
		auto-magically added to your blacklist.

### Known Limitations
	- Only supports a single page
		At the moment for V1, processes do not move on to consequative pages once a page is full. This behaviour is something I would like to add, however it would
		be a user setting as this can also be seen as unintended behaviour.

	- You can only have one of the same applications as static
		You can have multiple static applications, just not the same application. This is a limitation of how the code was written. 
		This shouldn't be an issue for most, let me know if it is.

	- A single process with multiple audio steams are conglomerated.
		This is due to the fact there is no way to discriminate between them during the applications lifecycle.

### Known Issues
	- Application's not showing/disapearing when launched/closed.
		While testing I noticed this occuring with some apps, notably Spotify. Some apps will only notify the OS
		of their audio intentions once they actually start playing, not on open. So keep this in mind. You can see this for yourself, if you open up the Windows Volume Mixer.
		A possible solution to this would require quite the re-write, of watching applications rather than just audio sessions.

### TODO
	- Support changing of default output device.