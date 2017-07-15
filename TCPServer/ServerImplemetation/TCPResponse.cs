using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Internal;
using System.Collections.Concurrent;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Primitives;

namespace TCPServer.ServerImplemetation
{
	class TCPResponse : HttpResponse
	{
		class DummyResponseCookies : IResponseCookies
		{
			public void Append(string key, string value)
			{
				
			}

			public void Append(string key, string value, CookieOptions options)
			{
				
			}

			public void Delete(string key)
			{
				
			}

			public void Delete(string key, CookieOptions options)
			{
				
			}
		}
		public TCPResponse(HttpContext context)
		{
			_HttpContext = context;
		}

		HttpContext _HttpContext;
		public override HttpContext HttpContext => _HttpContext;

		public override int StatusCode
		{
			get;
			set;
		} = 200;

		HeaderDictionary _Headers = new HeaderDictionary();
		public override IHeaderDictionary Headers => _Headers;

		public override Stream Body
		{
			get;
			set;
		}
		public override long? ContentLength
		{
			get;
			set;
		}
		public override string ContentType
		{
			get
			{
				StringValues value;
				if(!Headers.TryGetValue("Content-Type", out value))
					return null;
				return value.FirstOrDefault();
			}
			set
			{
				Headers.Remove("Content-Type");
				if(value != null)
					Headers.Add("Content-Type", new StringValues(value));
			}
		}

		IResponseCookies _Cookies = new DummyResponseCookies();
		public override IResponseCookies Cookies => _Cookies;

		public override bool HasStarted => true;

		ConcurrentBag<Tuple<Func<object, Task>, object>> _OnCompleted = new ConcurrentBag<Tuple<Func<object, Task>, object>>();
		public override void OnCompleted(Func<object, Task> callback, object state)
		{
			_OnCompleted.Add(Tuple.Create(callback, state));
		}

		public void OnCompleted()
		{
			foreach(var callback in _OnCompleted)
				callback.Item1(callback.Item2);
		}

		public void OnStarting()
		{
			foreach(var callback in _OnStarting)
				callback.Item1(callback.Item2);
		}

		ConcurrentBag<Tuple<Func<object, Task>, object>> _OnStarting = new ConcurrentBag<Tuple<Func<object, Task>, object>>();
		public override void OnStarting(Func<object, Task> callback, object state)
		{
			_OnStarting.Add(Tuple.Create(callback, state));
		}

		public override void Redirect(string location, bool permanent)
		{
			
		}
	}
}
