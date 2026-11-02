using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoEnhancer;

/// <summary>按文件路径、大小和修改时间缓存权重预检，不改写用户能力清单。</summary>
internal static class ModelInspectionCache
{
    internal static ModelImportInspection? Get(string path, ModelImportManager manager)
    {
        if (!File.Exists(path) || Path.GetExtension(path).ToLowerInvariant() is not
            (".pth" or ".pt" or ".ckpt" or ".safetensors" or ".onnx")) return null;
        var file = new FileInfo(path);
        var schema = Path.GetExtension(path).Equals(".onnx", StringComparison.OrdinalIgnoreCase) ? "v5" : "v4";
        var key = $"{schema}|{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        var directory = Path.Combine(PortablePaths.CacheRoot, "model-inspections");
        var cache = Path.Combine(directory, hash + ".json");
        if (File.Exists(cache))
        {
            try { return JsonSerializer.Deserialize(File.ReadAllText(cache, Encoding.UTF8), ModelInspectionJsonContext.Default.ModelImportInspection); }
            catch (JsonException) { }
        }
        var inspection = manager.Inspect(path);
        if (!string.IsNullOrWhiteSpace(inspection.Error)) return null;
        Directory.CreateDirectory(directory);
        File.WriteAllText(cache, JsonSerializer.Serialize(inspection, ModelInspectionJsonContext.Default.ModelImportInspection), new UTF8Encoding(false));
        return inspection;
    }
}

[JsonSerializable(typeof(ModelImportInspection))]
internal partial class ModelInspectionJsonContext : JsonSerializerContext;
