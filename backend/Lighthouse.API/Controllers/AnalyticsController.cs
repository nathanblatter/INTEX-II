using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace Lighthouse.API.Controllers;

[ApiController]
[Route("a")]
public class AnalyticsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _umamiUrl;
    private readonly string _websiteId;

    public AnalyticsController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _umamiUrl = config["UmamiBaseUrl"] ?? "http://docker-services-umami-1:3000";
        _websiteId = config["UmamiWebsiteId"] ?? "952ef81f-6f69-4c1d-a9b4-dc4f6b874e92";
    }

    [HttpGet("script.js")]
    public async Task<IActionResult> GetScript()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var res = await client.GetAsync($"{_umamiUrl}/script.js");
            var body = await res.Content.ReadAsStringAsync();
            return Content(body, "application/javascript");
        }
        catch
        {
            return NoContent();
        }
    }

    [HttpPost("api/send")]
    public async Task<IActionResult> SendEvent()
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            using var reader = new System.IO.StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync();

            var ip = Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                  ?? Request.Headers["X-Forwarded-For"].FirstOrDefault()
                  ?? HttpContext.Connection.RemoteIpAddress?.ToString()
                  ?? "127.0.0.1";

            var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_umamiUrl}/api/send")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            reqMsg.Headers.TryAddWithoutValidation("User-Agent", Request.Headers["User-Agent"].ToString());
            reqMsg.Headers.TryAddWithoutValidation("X-Forwarded-For", ip);
            reqMsg.Headers.TryAddWithoutValidation("X-Real-IP", ip);

            var res = await client.SendAsync(reqMsg);
            var respBody = await res.Content.ReadAsStringAsync();
            return Content(respBody, "application/json");
        }
        catch
        {
            return NoContent();
        }
    }
}
