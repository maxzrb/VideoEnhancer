using System;
using System.IO;

namespace VideoEnhancer.Testing;

internal static class RepositoryPaths
{
    // 显式参数和环境变量优先；默认从测试程序目录逐级定位仓库。
    internal static string ResolveRoot(string explicitRoot = null)
    {
        var configured = string.IsNullOrWhiteSpace(explicitRoot)
            ? Environment.GetEnvironmentVariable("VIDEOENHANCER_REPOSITORY_ROOT") : explicitRoot;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var root = Path.GetFullPath(configured);
            if (!File.Exists(Path.Combine(root, "VideoEnhancer.slnx")))
                throw new DirectoryNotFoundException("仓库根目录缺少 VideoEnhancer.slnx：" + root);
            return root;
        }
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "VideoEnhancer.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("请传入仓库根目录，或设置 VIDEOENHANCER_REPOSITORY_ROOT。");
    }
}
