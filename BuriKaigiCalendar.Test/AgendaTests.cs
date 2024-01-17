using System.Text.RegularExpressions;
using BuriKaigiCalendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BuriKaigiCalendar.Test;

public class AgendaTests
{
    [Test]
    public async Task GetSessionsAsync_No_BreakingTime_Test()
    {
        // Given
        using var services = TestHost.GetServiceProvider();
        var agenda = services.GetRequiredService<Agenda>();

        // When
        var sessions = await agenda.GetSessionsAsync();

        // Then
        sessions.Any(session => session.Title == "休憩").IsFalse();
    }

    [Test]
    public async Task GetSessionsAsync_Test()
    {
        static string ToString(DateTime dateTime) => dateTime.AddHours(9).ToString("MM/dd/yyyy HH:mm");

        // Given
        using var services = TestHost.GetServiceProvider();
        var agenda = services.GetRequiredService<Agenda>();

        // When
        var sessions = await agenda.GetSessionsAsync();

        // Then
        var titlesToProve = new[] {
            "受付開始", "オープニング",
            "タイトル A", "タイトル B", "タイトル F", "タイトル P", "タイトル AC",
            "クロージング", "スペシャルイベントタイトル A", "スペシャルイベントタイトル B", "スペシャルイベントタイトル D", "スポンサーセッション",
            "撤収"};
        sessions
            .Where(session => titlesToProve.Contains(session.Title))
            .Select(s => $"{ToString(s.StartTime)} - {ToString(s.EndTime)}, {s.Location}, {s.Title}, {s.Speaker}, {s.Description}")
            .Is([
                "01/20/2024 11:00 - 01/20/2024 11:10, 共通 (DXセンター1F), 受付開始, , ",
                "01/20/2024 11:50 - 01/20/2024 12:00, 共通 (DXセンター1F), オープニング, , ",
                "01/20/2024 12:00 - 01/20/2024 12:15, 共通 (DXセンター1F), タイトル A, スピーカー A, <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 12:30 - 01/20/2024 13:30, Room-Buri (DXセンター1F), タイトル B, スピーカー B, <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 12:50 - 01/20/2024 13:10, Room-Shiroebi (中央棟2F 教室), タイトル F, スピーカー F, <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 12:30 - 01/20/2024 12:50, Room-Hotaruika (中央棟2F 教室), タイトル P, スピーカー P, <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 13:40 - 01/20/2024 14:00, Room-Masu (中央棟2F 教室), タイトル AC, スピーカー AC, <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 16:40 - 01/20/2024 16:50, スペシャルイベント (DXセンター1F), クロージング, , ",
                "01/20/2024 16:50 - 01/20/2024 17:40, スペシャルイベント (DXセンター1F), スペシャルイベントタイトル A, スペシャルイベントスピーカー A, ",
                "01/20/2024 17:40 - 01/20/2024 17:55, スペシャルイベント (DXセンター1F), スペシャルイベントタイトル B, スペシャルイベントスピーカー B (スペシャルイベント所属 B), <p>説明 A</p><p>説明 B</p>",
                "01/20/2024 18:10 - 01/20/2024 18:20, スペシャルイベント (DXセンター1F), スペシャルイベントタイトル D, スペシャルイベントスピーカー D (スペシャルイベント所属 D), ",
                "01/20/2024 18:20 - 01/20/2024 18:30, スペシャルイベント (DXセンター1F), スポンサーセッション, スペシャルイベントスピーカー E, ",
                "01/20/2024 18:30 - 01/20/2024 18:45, スペシャルイベント (DXセンター1F), 撤収, , ",
            ]);
    }

    [Test]
    public async Task GetSessionsAsICalAsync_StartMidnight_Test()
    {
        // Given
        using var services = TestHost.GetServiceProvider();
        var agenda = services.GetRequiredService<Agenda>();

        // When
        var ical = await agenda.GetSessionsAsICalAsync();

        // Then
        ical.Split("\r\n")
            .Where(l => l.StartsWith("DTSTART"))
            .ToList()
            .ForEach(l => l.Is(l => Regex.IsMatch(l, "^DTSTART:20240120T\\d{6}Z$")));
    }
}