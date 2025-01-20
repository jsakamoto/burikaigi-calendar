using System.Text;
using BuriKaigiCalendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BuriKaigiCalendar.Test;

internal class TestHost
{
    private class AgendaPageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var fileName = request.RequestUri?.AbsoluteUri switch
            {
                "https://fortee.jp/burikaigi-2025/timetable" => "timetable.html",
                string path when path.StartsWith("https://fortee.jp/burikaigi-2025/proposal/session-") => $"session-{path.Last()}.html",
                _ => throw new NotImplementedException()
            };

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var content = File.ReadAllText(Path.Combine(baseDir, "Assets", fileName));
            return Task.FromResult(new HttpResponseMessage()
            {
                Content = new StringContent(content, Encoding.UTF8, "text/html")
            });
        }
    }

    public static ServiceProvider GetServiceProvider()
    {
        return new ServiceCollection()
            .AddHttpClient()
            .ConfigureHttpClientDefaults(builder =>
            {
                builder.ConfigurePrimaryHttpMessageHandler((_) => new AgendaPageHandler());
            })
            .AddSingleton<Agenda>()
            .BuildServiceProvider();
    }
}
