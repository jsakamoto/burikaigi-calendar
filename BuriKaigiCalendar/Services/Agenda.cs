using System.Globalization;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using BuriKaigiCalendar.Models;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

namespace BuriKaigiCalendar.Services;

internal class Agenda
{
    private readonly IHttpClientFactory _httpClientFactory;

    public Agenda(IHttpClientFactory httpClientFactory)
    {
        this._httpClientFactory = httpClientFactory;
    }

    internal async ValueTask<IEnumerable<Session>> GetSessionsAsync()
    {
        static DateTime ParseDateTime(string? time) => DateTime.TryParse($"2024-01-20T{time}+9:00", null, DateTimeStyles.AdjustToUniversal, out var date) ? date : DateTime.MinValue;

        static async ValueTask<string> GetDescriptionAsync(HttpClient httpClient, HtmlParser parser, string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            var response = await httpClient.GetStringAsync(url);
            var document = await parser.ParseDocumentAsync(response);
            var descriptions = document.QuerySelectorAll(".session--info > *:not(h1)")
                .Select(e => e.TextContent)
                .Select(text => text.Replace("\r", ""))
                .Select(text => text.Replace("\n", "<br/>"))
                .ToArray();
            return string.Concat(descriptions.Length < 2 ? descriptions : descriptions.Select(d => $"<p>{d}</p>"));
        }

        var sessionList = new List<Session>();

        // Fetch the Agenda page.
        var baseUrl = "https://burikaigi.dev";
        var httpClient = this._httpClientFactory.CreateClient();
        var response = await httpClient.GetStringAsync(baseUrl);

        // Parse the HTML string by AngleSharp's HtmlParser
        var parser = new HtmlParser();
        var document = await parser.ParseDocumentAsync(response);

        var schedule = document.QuerySelector("#schedule");
        var scheduleContent = schedule?.LastElementChild?.LastElementChild;
        var trackHeaders = scheduleContent?.QuerySelectorAll("h3").AsEnumerable() ?? [];
        foreach (var trackHeader in trackHeaders)
        {
            var trackName = trackHeader.TextContent;
            var roomName = trackHeader.NextElementSibling?.TextContent;

            var sessionsContainer = trackHeader.ParentElement?.NextElementSibling;
            var sessions = sessionsContainer?.QuerySelectorAll("li").AsEnumerable() ?? [];
            foreach (var session in sessions)
            {
                var speakerElement = session.QuerySelector("h4");
                var speaker = speakerElement?.TextContent ?? "";
                if (speaker == "休憩") continue;

                var times = session.QuerySelectorAll("time").AsEnumerable()
                    .Select(t => ParseDateTime(t.TextContent)) ?? [];

                var startTime = times.FirstOrDefault();
                var endTime = times.Skip(1).FirstOrDefault();
                if (endTime == DateTime.MinValue) endTime = startTime.AddMinutes(10);

                var titleElement = speakerElement?.NextElementSibling;
                var title = titleElement?.ChildElementCount == 0 ? titleElement.TextContent : "" ?? "";
                var url = titleElement is IHtmlAnchorElement anchor ? baseUrl + anchor.PathName : "";

                var organizationElement = titleElement?.NextElementSibling;
                var organization = organizationElement?.ChildElementCount == 0 ? organizationElement.TextContent : default;

                var descriptions = await GetDescriptionAsync(httpClient, parser, url);

                sessionList.Add(new Session
                {
                    Speaker = (title == "" ? "" : speaker) + (organization != null ? $" ({organization})" : ""),
                    Title = title == "" ? speaker : title,
                    StartTime = startTime,
                    EndTime = endTime,
                    Description = descriptions,
                    Location = $"{trackName} ({roomName})",
                });
            }
        }

        return sessionList;
    }

    internal async ValueTask<string> GetSessionsAsICalAsync()
    {
        var sessionList = await this.GetSessionsAsync();
        var calendar = new Ical.Net.Calendar();
        calendar.AddProperty("X-WR-CALNAME", "BuriKaigi");
        calendar.AddProperty("X-WR-CALDESC", "北陸ITエンジニアカンファレンス");
        foreach (var session in sessionList)
        {
            var icalEvent = new CalendarEvent
            {
                IsAllDay = false,
                Uid = session.GetHashForUID(),
                DtStart = new CalDateTime(session.StartTime) { HasTime = true },
                DtEnd = new CalDateTime(session.EndTime) { HasTime = true },
                Summary = session.Title,
                Description = $"<b>Speaker:</b>\r\n{session.Speaker}\r\n\r\n<b>Description:</b>\r\n{session.Description}",
                Location = session.Location,
            };
            calendar.Events.Add(icalEvent);
        }

        var serializer = new CalendarSerializer(new SerializationContext());
        return serializer.SerializeToString(calendar);
    }
}