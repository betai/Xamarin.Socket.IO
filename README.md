Xamarin.Socket.IO (beta)
=================

C# interface for socket.io on mobile platforms.

This library uses WebSocket4Net for the websocket connections, and Newtonsoft.Json to serialize the objects passed through the socket.

##Running the Test app (OSX/Android)

#####Set up the local server

  * Install node: you can install this via [brew](http://brew.sh/) (see bottom of the page for install script,
    then run ```brew install node```)
  * Navigate to the server directory and run ```npm install```
  * run ```node app.js```
  (If you run into a module.js exception in socket.io, see the following [fix](http://stackoverflow.com/questions/11266608/socket-io-error))

#####Restore the NuGet packages
  
  * Right-click on references and select "Restore NuGet Packages"

#####Run the app

  * Set the Socket.IO.Test project to be the startup project

##API

The basics

```C#
var socket = new SocketIO (host: "www.example.com", port: 80);

var connectionStatus = await socket.ConnectAsync ();

if (connectionStatus == ConnectionStatus.Connected) {
  socket.Emit ("MessageName", new Foo [] { new Foo () }); //emit message named "MessageName" with args a list of Foos
  socket.On ("MessageReceived", (data) => {               //call this lambda when a message named "MessageReceived"
    Console.WriteLine (data ["jsonFieldName"]);     //is emitted from the server
  });
} else {
  Console.WriteLine ("Websocket failed to connect to the server");
}

socket.Disconnect ();
```
\*Note that ```data``` is a JToken.

