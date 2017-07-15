using Microsoft.AspNetCore.Hosting;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using TCPServer;
using TCPServer.Client;
using Xunit;

namespace TCPServer.Tests
{
	public class UnitTest1
	{
		IPEndPoint serverBind = new System.Net.IPEndPoint(IPAddress.Parse("0.0.0.0"), 29472);
		IPEndPoint serverIp = new System.Net.IPEndPoint(IPAddress.Parse("127.0.0.1"), 29472);

		[Fact]
		public void CanSetupServer()
		{
			IWebHost host = CreateHost();
			var client = new HttpClient(new TCPHttpMessageHandler());
			var nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico").Result.Content.ReadAsStringAsync().Result;
			Assert.Equal("\"nico\"", nico);
			nico = client.GetAsync("http://127.0.0.1:29472/v1/hello/nico?test=toto").Result.Content.ReadAsStringAsync().Result;
			Assert.Equal("\"nicototo\"", nico);

			var error = Assert.Throws<HttpRequestException>(() => client.GetAsync("http://127.0.0.1:29472/v1/badrequest/nico").Result.EnsureSuccessStatusCode());
			Assert.Contains("400", error.Message);
		}

		private IWebHost CreateHost()
		{
			var host = new WebHostBuilder()
				.UseTCPServer(new ServerOptions(serverBind))
				.UseStartup<Startup>()
				.Build();
			host.Start();
			return host;
		}
	}
}
