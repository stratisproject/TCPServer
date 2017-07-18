using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TCPServer.Tests
{
    public class Utils
    {
		public static T TryUntilMakeIt<T>(Func<T> act)
		{
			CancellationTokenSource cts = new CancellationTokenSource();
			cts.CancelAfter(60000);
			while(true)
			{
				try
				{
					return act();
				}
				catch { }
				Thread.Sleep(500);
				cts.Token.ThrowIfCancellationRequested();
			}
		}
    }
}
