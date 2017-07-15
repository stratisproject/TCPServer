using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Internal;

namespace TCPServer.ServerImplemetation
{
	class TCPRequest : HttpRequest
	{
		private TCPRequest()
		{

		}

		public static async Task<TCPRequest> Parse(TCPStream input)
		{
			var r = new TCPRequest();
			await r.ParseCore(input).ConfigureAwait(false);
			return r;
		}

		private async Task ParseCore(TCPStream stream)
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
			ContentType = await stream.ReadStringAync().ConfigureAwait(false);
			if(ContentType != string.Empty)
			{
				var buffer = await stream.ReadBytesAync(TCPStream.ReadType.ManagedPool).ConfigureAwait(false);
				Body = new MemoryStream(buffer.Array);
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
			get;
			set;
		}
		public override string ContentType
		{
			get;
			set;
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
