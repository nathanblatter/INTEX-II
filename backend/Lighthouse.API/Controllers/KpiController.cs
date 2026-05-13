using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Lighthouse.API.Controllers;

[ApiController]
[Route("api/kpi")]
public class KpiController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public KpiController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public async Task<IActionResult> GetKpi()
    {
        var expectedKey = _config["KpiApiKey"];
        if (!Request.Headers.TryGetValue("X-KPI-Api-Key", out var providedKey) || providedKey != expectedKey)
            return Unauthorized(new { error = "Unauthorized" });

        var umamiBase = _config["UmamiBaseUrl"] ?? "http://100.79.61.79:3333";
        var websiteId = _config["UmamiWebsiteId"];
        var username = _config["UmamiUsername"] ?? "admin";
        var password = _config["UmamiPassword"] ?? "umami";

        var visitorKpis = new
        {
            weekly_unique_visitors = 0,
            weekly_pageviews = 0,
            bounce_rate = 0.0,
            avg_time_on_site_seconds = 0,
            pages_per_session = 0.0,
        };

        try
        {
            var client = _httpClientFactory.CreateClient();

            // Authenticate with Umami
            var loginBody = JsonSerializer.Serialize(new { username, password });
            var loginRes = await client.PostAsync(
                $"{umamiBase}/api/auth/login",
                new StringContent(loginBody, Encoding.UTF8, "application/json")
            );
            var loginJson = JsonDocument.Parse(await loginRes.Content.ReadAsStringAsync());
            var token = loginJson.RootElement.GetProperty("token").GetString();

            // Get stats for last 7 days
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var weekAgoMs = nowMs - 7L * 24 * 60 * 60 * 1000;
            var statsReq = new HttpRequestMessage(
                HttpMethod.Get,
                $"{umamiBase}/api/websites/{websiteId}/stats?startAt={weekAgoMs}&endAt={nowMs}"
            );
            statsReq.Headers.Add("Authorization", $"Bearer {token}");
            var statsRes = await client.SendAsync(statsReq);
            var statsJson = JsonDocument.Parse(await statsRes.Content.ReadAsStringAsync());
            var root = statsJson.RootElement;

            int uniques = root.TryGetProperty("uniques", out var u) ? u.GetProperty("value").GetInt32() : 0;
            int pageviews = root.TryGetProperty("pageviews", out var p) ? p.GetProperty("value").GetInt32() : 0;
            int bounces = root.TryGetProperty("bounces", out var b) ? b.GetProperty("value").GetInt32() : 0;
            int totalTime = root.TryGetProperty("totaltime", out var t) ? t.GetProperty("value").GetInt32() : 0;

            visitorKpis = new
            {
                weekly_unique_visitors = uniques,
                weekly_pageviews = pageviews,
                bounce_rate = uniques > 0 ? Math.Round((double)bounces / uniques * 100, 1) : 0.0,
                avg_time_on_site_seconds = uniques > 0 ? totalTime / uniques : 0,
                pages_per_session = uniques > 0 ? Math.Round((double)pageviews / uniques, 2) : 0.0,
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Umami unavailable: {ex.Message}");
        }

        return Ok(new
        {
            project = "intex_ii",
            generated_at = DateTime.UtcNow.ToString("o"),
            kpis = new
            {
                weekly_unique_visitors = new { value = visitorKpis.weekly_unique_visitors, label = "Weekly Unique Visitors", unit = "visitors" },
                weekly_pageviews = new { value = visitorKpis.weekly_pageviews, label = "Weekly Pageviews", unit = "pageviews" },
                bounce_rate = new { value = visitorKpis.bounce_rate, label = "Bounce Rate", unit = "%" },
                avg_time_on_site_seconds = new { value = visitorKpis.avg_time_on_site_seconds, label = "Avg Time on Site", unit = "seconds" },
                pages_per_session = new { value = visitorKpis.pages_per_session, label = "Pages per Session", unit = "pages" },
            }
        });
    }
}
