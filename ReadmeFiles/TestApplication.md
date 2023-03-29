# Test your deployment

Easiest is to navigate to `https://<your webapp url>/issue` this should allow you to sign in and show your profile data. If this is working the first part of the configuration is done correctly. If you press the button and a QR code is shown the security setup is working 100%. If you can scan the QR code and issue yourself a VC in Authenticator the VC service is working and the configuration of the webapp is complete.

When you launch the website on your desktop you should see this screen after a sign-in:

![Retrieving your credentials](Images/TestingyourappRetrievingCredentials.png)

A few seconds later this screen should show:

![Error message on screen](Images/TestingyourappErrorMessage.png)

This is expected since the webapp tries to redirect to linkedin:// which is not possible on a desktop. If you check the console in the browser developer tools, you will see this message:

![Browser tools console with error message](Images/TestingyourappBrowserToolsConsole.png)

This means the app is working correctly.