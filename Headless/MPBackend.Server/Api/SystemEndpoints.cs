using System.Text.RegularExpressions;
using MissionPlanner.Utilities;
using MPBackend.Core;
using MPBackend.Core.Gps;

namespace MPBackend.Server.Api;

public sealed record SurveyInDto(int Duration = 60, double Accuracy = 2.0, bool M8p130Plus = false);

public sealed record FixedBaseDto(double Lat, double Lng, double Alt, bool M8p130Plus = false);

public sealed record SettingDto(string? Value);

/// <summary>
/// RTK injection, ADS-B and the settings store (Milestone 5).
/// </summary>
public static class SystemEndpoints
{
    // values never returned by GET /settings (the backend has no auth)
    private static readonly Regex Secret = new("pass|secret|apikey|api_key|token", RegexOptions.IgnoreCase);

    public static void MapSystemApi(this RouteGroupBuilder api)
    {
        var rtk = api.MapGroup("/rtk").WithTags("RTK");

        rtk.MapGet("/", (Backend b) => b.Rtk.GetStatus())
            .WithSummary("Injection status: rates, RTCM message counts, last seen per constellation, base position");

        rtk.MapPost("/start", (Backend b, RtkStartRequest req) => b.Rtk.Start(req))
            .WithSummary("Start injecting corrections from NTRIP (ntrip://user:pass@host:port/mount), serial, TCP or UDP");

        rtk.MapPost("/stop", (Backend b) => b.Rtk.Stop());

        rtk.MapPost("/ublox/survey-in", (Backend b, SurveyInDto dto) =>
            {
                b.Rtk.UbloxSurveyIn(dto.Duration, dto.Accuracy, dto.M8p130Plus);
                return b.Rtk.GetStatus();
            })
            .WithSummary("Configure a u-blox base on the serial source for survey-in (duration s, accuracy m)");

        rtk.MapPost("/ublox/fixed", (Backend b, FixedBaseDto dto) =>
            {
                b.Rtk.UbloxFixedBase(dto.Lat, dto.Lng, dto.Alt, dto.M8p130Plus);
                return b.Rtk.GetStatus();
            })
            .WithSummary("Configure a u-blox base with a known position");

        var adsb = api.MapGroup("/adsb").WithTags("ADS-B");

        adsb.MapGet("/aircraft", (Backend b) => b.Adsb.List())
            .WithSummary("Known aircraft (vehicle-reported and external feed), nearest first");

        adsb.MapGet("/config", (Backend b) => b.Adsb.Config);

        adsb.MapPut("/config", (Backend b, AdsbConfig config) => b.Adsb.Configure(config))
            .WithSummary("External feed (SBS/AVR host or ADSBExchange-style API URL) and forwarding to the vehicle");

        var settings = api.MapGroup("/settings").WithTags("Settings");

        settings.MapGet("/", () => Settings.Instance.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(k => k, k => Secret.IsMatch(k) ? "***" : Settings.Instance[k]))
            .WithSummary("Mission Planner settings store (config.xml in the data directory); secrets are masked");

        settings.MapGet("/{key}", (string key) =>
            Settings.Instance.ContainsKey(key) && !Secret.IsMatch(key)
                    ? Results.Ok(new SettingDto(Settings.Instance[key]))
                    : Results.NotFound())
            .Produces<SettingDto>().Produces(404);

        settings.MapPut("/{key}", (string key, SettingDto dto) =>
            {
                Settings.Instance[key] = dto.Value ?? "";
                Settings.Instance.Save();
                return Results.NoContent();
            })
            .WithSummary("Set a value (the GUI can keep its own preferences here too)");

        settings.MapDelete("/{key}", (string key) =>
        {
            if (!Settings.Instance.ContainsKey(key))
                return Results.NotFound();
            Settings.Instance.Remove(key);
            Settings.Instance.Save();
            return Results.NoContent();
        });
    }
}
