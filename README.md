# WHEAService
WHEAService, suppressor for WHEA errors

The purpose of this application is to allow AMD Ryzen processor to run at FCLK above 1900 MHz without performance penalties.

Without the hundreds of WHEA errors logged every second the system will not be clogged and run smoothly.

Of course you will loose the error reporting.

Please ensure the system is stable. 

Run OCCT for more than 1 hour and multiple y-cruncher stress test cycles (better all tests selected).

On top check if you can pass CoreCycler SSE with Huge dataset.


## **USE AT YOUR OWN RISK**


## Installation

There's a nice Wix installer that will do everything for you.

For those who hates installers, there's also a "portable" version.

The batch files for installation are using InstallUtil from .NET Framework 4.0 therefore you need it installed to use them.

Please use the installer if you don't know exactly what I mean.

If .NET is not available you can use the command *sc* to install and remove the service and configure it manually.


## Usage

WHEAService is a Windows Service which runs at system startup.

There's nothing that you have to do after the installation, just reboot the system and set a higher FCLK.

The Service should be configured to run under Local System account and Automatic startup.


It's not meant to go into Running mode; it stops itself after a few seconds.

You can also run it from Computer Management under Services; but there's no real need to run it more than once.

There are events logged in the Applications Custom logs under WHEAServiceLog.

Since it's a Service it'll stop WHEA even before you Login or without Login at all.

No memory leaks, no usage of resources in background.

## What is doing exactly?

**It's not fixing WHEA errors or any performance degradation or stability issues that comes from running at high FCLK.**

It will only stop the reporting which is bogging down the system when the WHEA errors are coming in at high rate.

There's a pre-defined list of WHEA error sources in the code for which an attempt will be done to issue a stop request.

Some of them can't be stopped. The query will fail. That's not important; just issuing a stop request will silence them.

You can see in the Event Logs under the WHEAServiceLog custom provider the list of the sources targeted for the stop request, a list with all your WHEA sources and their status at the beginning and at then end of the process.


