using System.Reflection;
using System.Text.Json;

namespace VideoEnhancer;

/// <summary>随程序发布的模型能力清单，避免从文件名猜测已知模型的倍率和输入约束。</summary>
internal static class ModelCapabilityCatalog
{
    private const string ResourceName = "VideoEnhancer.Embedded.model-capabilities.json";
    private static readonly Lazy<CatalogDocument> Document = new(Load);

    internal static IReadOnlyList<ModelCapability> Models => Document.Value.Models;

    internal static bool TryGet(string modelPath, string modelsDirectory, out ModelCapability capability)
    {
        if (!string.IsNullOrWhiteSpace(modelsDirectory)
            && UserModelCatalog.TryGet(modelPath, modelsDirectory, out var user))
        {
            capability = new ModelCapability
            {
                Model = user.RelativePath,
                Scale = user.Scale,
                Architecture = user.Architecture,
                Backends = user.Backends,
                InputMultiple = Math.Max(1, user.InputMultiple),
            };
            return true;
        }
        var key = NormalizeKey(modelPath, modelsDirectory);
        if (Document.Value.ByModel.TryGetValue(key, out capability!))
        {
            return true;
        }
        // TensorRT 缓存文件以源模型名开头，后接 __input-* 等构建参数。
        if (key.StartsWith("TensorRT-Cache/", StringComparison.OrdinalIgnoreCase))
        {
            var cacheStem = key[(key.LastIndexOf('/') + 1)..];
            var separator = cacheStem.IndexOf("__", StringComparison.Ordinal);
            if (separator > 0)
            {
                return Document.Value.ByModel.TryGetValue("PTH/" + cacheStem[..separator], out capability!);
            }
        }
        capability = null!;
        return false;
    }

    internal static string NormalizeKey(string modelPath, string modelsDirectory)
    {
        var value = (modelPath ?? string.Empty).Trim().Trim('"');
        if (Path.IsPathRooted(value) && !string.IsNullOrWhiteSpace(modelsDirectory))
        {
            try
            {
                var relative = Path.GetRelativePath(Path.GetFullPath(modelsDirectory), Path.GetFullPath(value));
                if (!relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                    && !relative.Equals("..", StringComparison.Ordinal))
                {
                    value = relative;
                }
            }
            catch
            {
                // 路径格式异常时保留原值，交给后续文件名回退逻辑处理。
            }
        }

        value = value.Replace('\\', '/').Trim('/');
        var extension = Path.GetExtension(value);
        if (extension.Equals(".pth", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pkl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ckpt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".safetensors", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".onnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".engine", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^extension.Length];
        }
        return value;
    }

    private static CatalogDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("内置模型能力清单不存在：" + ResourceName);
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion != 1)
        {
            throw new InvalidOperationException("不支持的模型能力清单版本：" + schemaVersion);
        }

        var models = new List<ModelCapability>();
        foreach (var item in root.GetProperty("models").EnumerateArray())
        {
            models.Add(new ModelCapability
            {
                Model = item.GetProperty("model").GetString() ?? string.Empty,
                Scale = item.GetProperty("scale").GetInt32(),
                Architecture = item.GetProperty("architecture").GetString() ?? string.Empty,
                Backends = item.GetProperty("backends").EnumerateArray()
                    .Select(value => value.GetString() ?? string.Empty).ToArray(),
                InputMultiple = item.TryGetProperty("inputMultiple", out var multiple)
                    ? multiple.GetInt32() : 1,
            });
        }
        var byModel = new Dictionary<string, ModelCapability>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in models)
        {
            capability.Model = NormalizeKey(capability.Model, string.Empty);
            capability.InputMultiple = Math.Max(1, capability.InputMultiple);
            if (capability.Model.Length == 0 || capability.Scale < 1 || capability.Backends.Length == 0)
            {
                throw new InvalidOperationException("模型能力清单包含无效条目：" + capability.Model);
            }
            if (!byModel.TryAdd(capability.Model, capability))
            {
                throw new InvalidOperationException("模型能力清单包含重复条目：" + capability.Model);
            }
        }
        return new CatalogDocument(models, byModel);
    }

    private sealed record CatalogDocument(
        IReadOnlyList<ModelCapability> Models,
        IReadOnlyDictionary<string, ModelCapability> ByModel);
}

internal sealed class ModelCapability
{
    public string Model { get; set; } = string.Empty;
    public int Scale { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string[] Backends { get; set; } = [];
    public int InputMultiple { get; set; } = 1;
}
