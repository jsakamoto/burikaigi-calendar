using System.Text.RegularExpressions;
using BuriKaigiCalendar.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BuriKaigiCalendar.Test;

public class AgendaTests
{
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
        sessions
            .OrderBy(s => s.StartTime)
            .Select(s => $"{ToString(s.StartTime)} - {ToString(s.EndTime)}, {s.Location}, {s.Title}, {s.Speaker}, {s.Description}")
            .Is([
                "02/01/2025 10:40 - 02/01/2025 11:10, ルーム: ホタルイカ, セッション A, スピーカー X, 説明1行目\n説明2行目",
                "02/01/2025 12:40 - 02/01/2025 13:00, ルーム: ブリ, オープニング, , ",
                "02/01/2025 14:20 - 02/01/2025 14:50, ルーム: シロエビ, セッション B, スピーカー Y, セッションの説明",
                "02/01/2025 18:40 - 02/01/2025 19:50, 懇親会会場, 懇親会 (〜20:00), , ",
                "02/01/2025 19:30 - 02/01/2025 19:35, 懇親会会場, セッション C, スピーカー Z, 説明1段落目\n\n説明2段落目",
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
            .ForEach(l => l.Is(l => Regex.IsMatch(l, "^DTSTART:20250201T\\d{6}Z$")));
    }
}