using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SecondSonLauncher.Core;

/// user\custom_configs\CUSA00004.json: the emulator's per-game settings, which win over
/// user\config.json (src/core/emulator_settings.h, make_override). The launcher only writes the
/// keys it shows and keeps the rest of the file.
public sealed class GameConfig
{
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    readonly JsonObject _game;
    readonly JsonObject? _global;

    GameConfig(JsonObject game, JsonObject? global)
    {
        _game = game;
        _global = global;
    }

    public static GameConfig Load()
    {
        var game = Parse(AppPaths.GameConfig) ?? Parse(AppPaths.DefaultGameConfig) ?? new JsonObject();
        return new GameConfig(game, Parse(AppPaths.GlobalConfig));
    }

    public T Get<T>(string section, string key, T fallback)
    {
        var node = _game[section]?[key] ?? _global?[section]?[key];
        if (node is not JsonValue value)
            return fallback;
        try
        {
            return value.GetValue<T>();
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException)
        {
            return fallback;
        }
    }

    public void Set(string section, string key, JsonNode? value)
    {
        if (_game[section] is not JsonObject obj)
        {
            obj = new JsonObject();
            _game[section] = obj;
        }
        obj[key] = value;
    }

    public void Save() => AtomicFile.WriteAllText(AppPaths.GameConfig, _game.ToJsonString(Indented));

    static JsonObject? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
