using System;
using System.Collections.Generic;
using System.Text;

namespace TCPServer
{
	public class CompositeDisposable : IDisposable
	{
		public CompositeDisposable()
		{

		}
		public List<IDisposable> Children
		{
			get; set;
		} = new List<IDisposable>();
		public void Dispose()
		{
			foreach(var c in Children)
				c.Dispose();
		}
	}
}
