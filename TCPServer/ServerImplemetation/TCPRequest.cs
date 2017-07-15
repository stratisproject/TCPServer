using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace TCPServer.ServerImplemetation
{
	class TCPRequest : HttpRequest
	{
		private TCPRequest()
		{

		}

		public static async Task<TCPRequest> Parse(TCPStream input, bool includeHeaders)
		{
			var r = new TCPRequest();
			await r.ParseCore(input, includeHeaders).ConfigureAwait(false);
			return r;
		}

		private async Task ParseCore(TCPStream stream, bool includeHeaders)
		{
			Method = await stream.ReadStringAync().ConfigureAwait(false);
			var requestUri = await stream.ReadStringAync().ConfigureAwait(false);
			var uri = new Uri(requestUri);
			Scheme = uri.Scheme;
			IsHttps = false;
			Host = new HostString(uri.Host, uri.Port);
			PathBase = new PathString(uri.AbsolutePath);
			Path = new PathString(uri.AbsolutePath);
			QueryString = new QueryString(uri.Query);
			Query = new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query));
			Protocol = "http";

			if(includeHeaders)
			{
				var headers = new List<Tuple<string, string>>();
				var headersCount = await stream.ReadVarIntAsync().ConfigureAwait(false);
				for(int i = 0; i < (int)headersCount; i++)
				{
					var key = await stream.ReadStringAync().ConfigureAwait(false);
					var value = await stream.ReadStringAync().ConfigureAwait(false);
					headers.Add(Tuple.Create(key, value));
				}

				foreach(var h in headers.GroupBy(g => g.Item1, g => g.Item2))
				{
					Headers.Add(h.Key, new StringValues(h.ToArray()));
				}
			}

			var hasContent = (await stream.ReadVarIntAsync().ConfigureAwait(false)) == 1;
			if(hasContent)
			{
				var buffer = await stream.ReadBytesAync(TCPStream.ReadType.ManagedPool).ConfigureAwait(false);
				Body = new MemoryStream(buffer.Array);
				Body.SetLength(buffer.Count);
				ContentLength = buffer.Count;
			}
		}

		HttpContext _HttpContext;
		public void SetHttpContext(HttpContext context)
		{
			if(context == null)
				throw new ArgumentNullException(nameof(context));
			_HttpContext = context;
		}
		public override HttpContext HttpContext => _HttpContext;

		public override string Method
		{
			get;
			set;
		}
		public override string Scheme
		{
			get;
			set;
		}
		public override bool IsHttps
		{
			get;
			set;
		}
		public override HostString Host
		{
			get;
			set;
		}
		public override PathString PathBase
		{
			get;
			set;
		}
		public override PathString Path
		{
			get;
			set;
		}
		public override QueryString QueryString
		{
			get;
			set;
		}
		public override IQueryCollection Query
		{
			get;
			set;
		}
		public override string Protocol
		{
			get;
			set;
		}

		IHeaderDictionary _Headers = new HeaderDictionary();
		public override IHeaderDictionary Headers => _Headers;

		public override IRequestCookieCollection Cookies
		{
			get;
			set;
		} = new RequestCookieCollection();
		public override long? ContentLength
		{
			get
			{
				StringValues value;
				if(!Headers.TryGetValue("Content-Length", out value))
					return null;
				return long.Parse(value.FirstOrDefault(), CultureInfo.InvariantCulture);
			}
			set
			{
				Headers.Remove("Content-Length");
				if(value != null)
					Headers.Add("Content-Length", value.Value.ToString(CultureInfo.InvariantCulture));
			}
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
		public override Stream Body
		{
			get;
			set;
		}

		public override bool HasFormContentType => false;

		public override IFormCollection Form
		{
			get;
			set;
		}

		FormCollection _ReadFormAsync = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
		public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return Task.FromResult<IFormCollection>(_ReadFormAsync);
		}
	}
}
