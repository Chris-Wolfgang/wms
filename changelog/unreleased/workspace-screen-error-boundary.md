type: fix

An exception in a console screen no longer ends the Blazor circuit: the workspace layout contains it, shows a localized message with a Retry that renders the screen again, and a scan whose handler fails is reported in the scan feedback while the scanner and the chrome keep working.
