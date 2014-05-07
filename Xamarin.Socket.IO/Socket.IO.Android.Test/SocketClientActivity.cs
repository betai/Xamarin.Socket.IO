using System;

using Android.App;
using Android.Widget;
using Android.OS;
using Xamarin.Socket.IO;
using Newtonsoft.Json;

namespace Socket.IO.Android.Test
{
	[JsonObject(MemberSerialization.OptIn)]
	public class Foo
	{
		[JsonProperty]
		public string Bar { get; set; }
	}

	[Activity(Label = "Socket.IO Client", MainLauncher = true)]
	public class SocketClientActivity : Activity
	{
		Button _connectButton;
		Button _disconnectButton;
		Button _messageButton;

		SocketIO Socket;

		protected override void OnCreate(Bundle bundle)
		{
			base.OnCreate(bundle);

			// Set our view from the "main" layout resource
			SetContentView(Resource.Layout.Main);

			_connectButton = FindViewById<Button>(Resource.Id.connectButton);
			_disconnectButton = FindViewById<Button>(Resource.Id.disconnectButton);
			_messageButton = FindViewById<Button>(Resource.Id.messageButton);
			
			_connectButton.Click += ConnectClick;
			_disconnectButton.Click += DisconnectClick;
			_messageButton.Click += MessageClick;
		}

		protected override void OnStart()
		{
			base.OnStart();

			// TODO: For testing Android, the host is the IP address of my emulator
			//	Some have suggested using 10.0.2.2 - the special alias for loopback to localhost
			Socket = new SocketIO (host : "192.168.56.1", port : 3000);

			Socket.On ("news_response", (data) => Console.WriteLine(data["hello"]));
		}

		void MessageClick (object sender, EventArgs e)
		{
			var list = new object [] { 1, "randomString", 3.4f, new Foo () { Bar = "baz"} };

			Socket.Emit ("news", list);
			Socket.Send ("regular old message");
			Socket.SendJson (new Foo () {
				Bar = "baz"
			});

			Socket.SendAcknowledgement (2, new string [] { "A", "B" });
		}

		void DisconnectClick (object sender, EventArgs e)
		{
			Socket.Disconnect();
		}

		async void ConnectClick (object sender, EventArgs e)
		{
			await Socket.ConnectAsync();
		}
	}
}