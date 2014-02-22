using System;
using System.Threading;
using System.Collections.Generic;

namespace Xamarin.Socket.IO
{
	public class MessageBroker
	{

		Timer HeartbeatTimer { get; set; }
		int HeartbeatInterval { get; set; }
		int TimeoutInterval { get; set; }

		public List<string> RequestQueue { get; set; }

		const float percentage = 0.75f;

		public MessageBroker (int heartbeatInterval, int timeoutInterval)
		{
			HeartbeatInterval = heartbeatInterval;
			TimeoutInterval = timeoutInterval;
		}
	}
}

