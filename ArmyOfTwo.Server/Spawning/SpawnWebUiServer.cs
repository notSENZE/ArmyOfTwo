using System.Net;
using System.Text;
using System.Text.Json;
using ArmyOfTwo.Server.Configuration;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ArmyOfTwo.Server.Spawning;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PostLoad + 100010)]
public sealed class SpawnWebUiServer(
    ConfigService configService,
    LocationTable locationTable,
    SpawnService spawnService,
    ISptLogger<SpawnWebUiServer> logger) : IOnLoad
{
    private static readonly (string Id, string Name)[] SupportedMaps =
    [
        ("bigmap", "Customs"),
        ("factory4_day", "Factory · Day"),
        ("factory4_night", "Factory · Night"),
        ("interchange", "Interchange"),
        ("laboratory", "The Lab"),
        ("lighthouse", "Lighthouse"),
        ("rezervbase", "Reserve"),
        ("sandbox", "Ground Zero · Low level"),
        ("sandbox_high", "Ground Zero · High level"),
        ("shoreline", "Shoreline"),
        ("tarkovstreets", "Streets of Tarkov"),
        ("woods", "Woods")
    ];

    private HttpListener? listener;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var config = configService.Load().WebUi;
        if (!config.Enabled)
        {
            return Task.CompletedTask;
        }

        var host = string.IsNullOrWhiteSpace(config.Host) ? "127.0.0.1" : config.Host;
        var port = config.Port is > 0 and <= 65535 ? config.Port : 6971;
        var address = $"http://{host}:{port}/";

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(address);
            listener.Start();
            _ = Task.Run(() => Listen(listener, cancellationToken), cancellationToken);
            logger.Info($"Army of Two: Operations Board available at {address}");
        }
        catch (Exception exception)
        {
            logger.Warning($"Army of Two: Operations Board could not start at {address}: {exception.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task Listen(HttpListener activeListener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && activeListener.IsListening)
        {
            HttpListenerContext? context = null;

            try
            {
                context = await activeListener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                if (!activeListener.IsListening)
                {
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            if (context is not null)
            {
                _ = Task.Run(() => Handle(context), cancellationToken);
            }
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            if (request.RemoteEndPoint is not null
                && !IPAddress.IsLoopback(request.RemoteEndPoint.Address))
            {
                await WriteText(response, "Local access only.", 403).ConfigureAwait(false);
                return;
            }

            var path = request.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;
            if (string.IsNullOrEmpty(path))
            {
                path = "/";
            }

            if (path == "/api/spawns" && request.HttpMethod == "GET")
            {
                await WriteJson(response, configService.Load().Spawns).ConfigureAwait(false);
                return;
            }

            if (path == "/api/spawns" && request.HttpMethod == "POST")
            {
                await SaveSpawns(request, response).ConfigureAwait(false);
                return;
            }

            if (path == "/api/field-data" && request.HttpMethod == "GET")
            {
                await WriteJson(response, CreateFieldData()).ConfigureAwait(false);
                return;
            }

            if (path == "/")
            {
                await ServeFile(response, "ui/index.html", "text/html; charset=utf-8").ConfigureAwait(false);
                return;
            }

            if (path == "/app.css")
            {
                await ServeFile(response, "ui/app.css", "text/css; charset=utf-8").ConfigureAwait(false);
                return;
            }

            if (path == "/app.js")
            {
                await ServeFile(response, "ui/app.client", "text/javascript; charset=utf-8").ConfigureAwait(false);
                return;
            }

            await WriteText(response, "Not found.", 404).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.Warning($"Army of Two: Operations Board request failed: {exception.Message}");
            TryClose(context.Response, 500);
        }
    }

    private async Task SaveSpawns(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (request.ContentLength64 > 1_000_000)
        {
            await WriteText(response, "Payload too large.", 413).ConfigureAwait(false);
            return;
        }

        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        SpawnConfig? spawns;
        try
        {
            spawns = JsonSerializer.Deserialize<SpawnConfig>(body, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException exception)
        {
            await WriteText(response, $"Invalid configuration: {exception.Message}", 400).ConfigureAwait(false);
            return;
        }

        if (spawns is null)
        {
            await WriteText(response, "Configuration is empty.", 400).ConfigureAwait(false);
            return;
        }

        configService.SaveSpawns(spawns);
        spawnService.Apply(spawns);
        await WriteJson(response, new { saved = true }).ConfigureAwait(false);
    }

    private object CreateFieldData()
    {
        var maps = SupportedMaps.Select(map => new
        {
            id = map.Id,
            name = map.Name,
            zones = GetZones(map.Id),
            defaultZones = GetDefaultZones(map.Id)
        });

        return new
        {
            version = ModInfo.Version,
            maps,
            operators = new[]
            {
                new { name = "Rook", role = "Lead · Close assault", weapon = "MPX · 9x19 PBP" },
                new { name = "Tombstone", role = "Overwatch · Marksman", weapon = "SR-25 · 7.62x51 M80" }
            }
        };
    }

    private List<string> GetZones(string mapName)
    {
        var locationKey = locationTable.GetMappedKey(mapName);
        if (!locationTable.GetDictionary().TryGetValue(locationKey, out var location))
        {
            return [];
        }

        return (location.Base.SpawnPointParams ?? [])
            .Select(point => point.BotZoneName)
            .OfType<string>()
            .Where(zone => !string.IsNullOrWhiteSpace(zone))
            .Select(zone => zone.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(zone => zone, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> GetDefaultZones(string mapName)
    {
        var locationKey = locationTable.GetMappedKey(mapName);
        if (!locationTable.GetDictionary().TryGetValue(locationKey, out var location))
        {
            return [];
        }

        var knightSpawn = location.Base.BossLocationSpawn?.FirstOrDefault(spawn => string.Equals(
            spawn.BossName,
            "bossKnight",
            StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(knightSpawn?.BossZone)
            ? []
            : knightSpawn.BossZone
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    private async Task ServeFile(HttpListenerResponse response, string relativePath, string contentType)
    {
        var path = Path.Combine(
            configService.ModPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(path))
        {
            await WriteText(response, "Not found.", 404).ConfigureAwait(false);
            return;
        }

        var bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
        response.StatusCode = 200;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static Task WriteJson<T>(HttpListenerResponse response, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        response.ContentType = "application/json; charset=utf-8";
        return WriteBytes(response, Encoding.UTF8.GetBytes(json), 200);
    }

    private static Task WriteText(HttpListenerResponse response, string text, int statusCode)
    {
        response.ContentType = "text/plain; charset=utf-8";
        return WriteBytes(response, Encoding.UTF8.GetBytes(text), statusCode);
    }

    private static async Task WriteBytes(HttpListenerResponse response, byte[] bytes, int statusCode)
    {
        response.StatusCode = statusCode;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static void TryClose(HttpListenerResponse response, int statusCode)
    {
        try
        {
            response.StatusCode = statusCode;
            response.Close();
        }
        catch
        {
        }
    }
}
