using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TCPServer.Tests.Controllers
{
	public class Request
	{
		public string Name
		{
			get; set;
		}
	}
	public class NoResult : IActionResult
	{
		public Task ExecuteResultAsync(ActionContext context)
		{
			context.HttpContext.Response.StatusCode = 200;
			return Task.CompletedTask;
		}
	}
    
	public class MainController : Controller
	{
		[HttpGet]
		[Route("v1/hello/{hello}")]
		public IActionResult Hello(string hello, string test)
		{
			return Json(hello + test);
		}

		[HttpPost]
		[Route("v1/hellojson")]
		public IActionResult HelloJson([FromBody]Request req)
		{
			return Json(req.Name);
		}

		[HttpPost]
		[Route("v1/nothing")]
		public IActionResult Nothing([FromBody]Request req)
		{
			return new NoResult();
		}

		[HttpGet]
		[Route("v1/badrequest/{hello}")]
		public IActionResult Bad(string hello)
		{
			return BadRequest("boom");
		}

		[HttpGet]
		[Route("v1/timeout")]
		public IActionResult Timeout()
		{
			Thread.Sleep(10000);
			return BadRequest("boom");
		}
	}
}
