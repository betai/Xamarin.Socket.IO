using System;
using System.Threading;

namespace Xamarin.Socket.IO
{
	public class PCLTimer
	{

		public event Action action;

		public PCLTimer () : this (100.0)
		{
		}

		public PCLTimer (double interval)
		{
		}
	}
}

