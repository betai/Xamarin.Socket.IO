using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using Xamarin.Socket.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Socket.IO.Test
{
	public partial class Socket_IO_TestViewController : UIViewController
	{
		static bool UserInterfaceIdiomIsPhone {
			get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
		}

		public Socket_IO_TestViewController (IntPtr handle) : base (handle)
		{
		}

		public override void DidReceiveMemoryWarning ()
		{
			// Releases the view if it doesn't have a superview.
			base.DidReceiveMemoryWarning ();
			
			// Release any cached data, images, etc that aren't in use.
		}

		#region View lifecycle

		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();
		}

		SocketIO Socket;

		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);

			Socket = new SocketIO (host : "127.0.0.1", port : 3000);

			Socket.On ("news_response", (data) => {
				Debug.WriteLine (data ["hello"]);
			});

			var connectButton = new UIButton () {
				Frame = new RectangleF (0, 0, View.Bounds.Width / 4, 44),
				BackgroundColor =  UIColor.Gray
			};

			connectButton.SetTitle ("connect", UIControlState.Normal);
			connectButton.TouchUpInside += async (object sender, EventArgs evtArgs) => {
				await Socket.ConnectAsync ();
			};

			var sendButton = new UIButton () {
				Frame = new RectangleF (0, 20, View.Bounds.Width / 4, 44),
				BackgroundColor = UIColor.Gray
			};

			sendButton.SetTitle ("message", UIControlState.Normal);
			sendButton.TouchUpInside += (object sender, EventArgs evtArgs) => {
				var list = new object [] { 1, "randomString", 3.4f, new Foo () { Bar = "baz"} };
				Socket.Emit ("news", list);
				Socket.Send ("regular old message");
				Socket.SendJson (new Foo () {
					Bar = "baz"
				});
				Socket.SendAcknowledgement (2, new string [] { "A", "B" });
			};

			var disconnectButton = new UIButton () {
				Frame = new RectangleF (View.Bounds.Width * 0.75f, 20, View.Bounds.Width / 4, 44),
				BackgroundColor = UIColor.Gray
			};

			disconnectButton.SetTitle ("disconnect", UIControlState.Normal);
			disconnectButton.TouchUpInside += (object sender, EventArgs evtArgs) => {
				Socket.Disconnect ();
			};

			connectButton.Center = View.Center;
			View.AddSubviews (connectButton, sendButton, disconnectButton);
		}


		[JsonObject(MemberSerialization.OptIn)]
		public class Foo
		{
			[JsonProperty]
			public string Bar { get; set; }
		}

		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);
		}

		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
		}

		public override void ViewDidDisappear (bool animated)
		{
			base.ViewDidDisappear (animated);
		}

		#endregion

	}
}

