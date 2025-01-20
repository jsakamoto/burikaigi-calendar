using System.Globalization;
using System.Text.RegularExpressions;
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
        static DateTime ParseDateTime(string? dateTime) => DateTime.TryParse($"{dateTime}+9:00", null, DateTimeStyles.AdjustToUniversal, out var date) ? date : DateTime.MinValue;
        var sessionList = new List<Session>();

        var authority = "https://fortee.jp";
        var httpClient = this._httpClientFactory.CreateClient();
        var parser = new HtmlParser();

        // Fetch the time table page
        var timetablePage = await httpClient.GetStringAsync("https://fortee.jp/burikaigi-2025/timetable");
        var timetableDoc = await parser.ParseDocumentAsync(timetablePage);

        // Parse the rooms for each track
        var rooms = timetableDoc.QuerySelectorAll(".track-header").Select(e => e.TextContent.Trim()).ToArray();

        // Parse the event start time
        var eventStartTimeText = timetableDoc.QuerySelector("#timetable")?.GetAttribute("data-from");
        if (eventStartTimeText is null) return sessionList;
        var eventStartTime = DateTime.Parse(eventStartTimeText, null, DateTimeStyles.AdjustToUniversal);

        // Traverse each session detail page
        var proposalBlocks = timetableDoc.QuerySelectorAll(".proposal");
        foreach (var proposalBlock in proposalBlocks)
        {
            if (proposalBlock.ClassList.Contains("time-slot"))
            {
                // Start time = 10:00 AM, 15min. = 30px
                var trackText = proposalBlock.ClassList.FirstOrDefault(c => c.StartsWith("track-"));
                if (trackText is null) continue;
                var track = int.Parse(trackText.Substring(6));
                var room = rooms[track - 1];

                var styleText = proposalBlock.GetAttribute("style") ?? "";
                var top = int.TryParse(Regex.Match(styleText, @"top:[ \t]*(\d+)px").Groups[1].Value, out var n) ? n : -1;
                if (top == -1) continue;

                var startTime = eventStartTime.AddMinutes(top / 30 * 5);
                var rawTitleText = proposalBlock.QuerySelector(".title")?.TextContent.Trim() ?? "";
                var m = Regex.Match(rawTitleText, @"^(?<title>(.+))[（\(](?<duration>\d+)分[）\)]");
                if (!m.Success) continue;

                var title = m.Groups["title"].Value.Trim();
                var duration = int.Parse(m.Groups["duration"].Value);

                // Add the session to the list
                sessionList.Add(new Session
                {
                    Speaker = "",
                    Title = title,
                    StartTime = startTime,
                    EndTime = startTime.AddMinutes(duration),
                    Description = "",
                    Location = room,
                });
            }
            else
            {
                var titleElement = proposalBlock.QuerySelector(".title a");
                if (titleElement is null) continue;

                var title = titleElement.TextContent.Trim();

                // Fetch the session detail page
                await Task.Delay(10);
                var sessionUrl = titleElement.GetAttribute("href");
                var sessionPage = await httpClient.GetStringAsync(authority + sessionUrl);
                var sessionDoc = await parser.ParseDocumentAsync(sessionPage);

                // Parse the session detail page
                var sessionInfoBlock = sessionDoc.QuerySelector(".type");
                var location = sessionInfoBlock?.QuerySelector(".track")?.TextContent.Trim() ?? "";
                var startTime = ParseDateTime(sessionInfoBlock?.QuerySelector(".schedule")?.TextContent.Trim().TrimEnd('〜'));
                var durationText = sessionInfoBlock?.QuerySelector(".name")?.TextContent ?? "";
                var duration = Regex.Match(durationText, @"(\d+)分") switch
                {
                    Match m when m.Success => int.Parse(m.Groups[1].Value),
                    _ => 0
                };
                var endTime = startTime.AddMinutes(duration);
                var speakerBlock = sessionDoc.QuerySelector(".speaker");
                var speaker = speakerBlock?.QuerySelector("span")?.TextContent.Trim() ?? "";
                var descriptionBlock = sessionDoc.QuerySelector(".abstract");
                var description = string.Join("\n", (descriptionBlock?.TextContent.Trim() ?? "").Split('\n').Select(s => s.Trim())).Replace("\n\n", "\n");

                // Add the session to the list
                sessionList.Add(new Session
                {
                    Speaker = speaker,
                    Title = title == "" ? speaker : title,
                    StartTime = startTime,
                    EndTime = endTime,
                    Description = description,
                    Location = location,
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