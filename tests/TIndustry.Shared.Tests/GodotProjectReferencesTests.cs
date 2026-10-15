using System.Text.RegularExpressions;
using Xunit;

namespace TIndustry.Shared.Tests;

public sealed class GodotProjectReferencesTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tIndustry.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("tIndustry.sln not found above the test binaries.");
    }

    private static string ResPath(string root, string res) =>
        Path.Combine(root, "godot", res["res://".Length..].Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void MainSceneAndItsScriptsExist()
    {
        var root = RepoRoot();
        var project = File.ReadAllText(Path.Combine(root, "godot", "project.godot"));
        var main = Regex.Match(project, "run/main_scene=\"(res://[^\"]+)\"");
        Assert.True(main.Success, "run/main_scene not found in project.godot");

        var scenePath = ResPath(root, main.Groups[1].Value);
        Assert.True(File.Exists(scenePath), $"main scene missing: {scenePath}");

        var scripts = Regex.Matches(File.ReadAllText(scenePath), @"\[ext_resource type=""Script""[^\]]*?path=""(res://[^""]+)""");
        Assert.NotEmpty(scripts);
        foreach (Match m in scripts)
        {
            var path = ResPath(root, m.Groups[1].Value);
            Assert.True(File.Exists(path), $"script missing: {path}");
        }
    }
}
