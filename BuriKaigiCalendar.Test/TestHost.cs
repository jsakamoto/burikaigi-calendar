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
                "https://burikaigi.dev/" => "burikaigi.dev.html",
                string path when path.StartsWith("https://burikaigi.dev/speakers/") => "burikaigi.dev.speakers.x.html",
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
