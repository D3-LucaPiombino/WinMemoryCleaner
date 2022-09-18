using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.AppVeyor;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;

using static Nuke.Common.ControlFlow;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.ReSharper.ReSharperTasks;
using System.IO.Compression;

[GitHubActions(
    "continuous",
    GitHubActionsImage.UbuntuLatest,
    AutoGenerate = true,
    FetchDepth = 0,
    OnPushBranches = new[] { "master", "feature/**", "releases/**" },
    OnPullRequestBranches = new[] { "releases/**", "feature/**" },
    InvokedTargets = new[] {
        nameof(Compile),
    },
    EnableGitHubToken = true,
    PublishArtifacts = true
    //ImportSecrets = new[] { nameof(MyGetApiKey), nameof(NuGetApiKey) }
)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath PackagesDirectory => OutputDirectory / "packages";

    [GitVersion]
    GitVersion GitVersion;

    Target Clean => _ => _
        //.Before(Compile)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("*/bin", "*/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Compile => _ => _
        .DependsOn(Clean)
        .Triggers(Publish)
        .Executes(() =>
        {
            DotNetBuild(s => s
              .SetProjectFile(Solution)
              .SetConfiguration(Configuration)
              .SetVersion(GitVersion.NuGetVersionV2)
              .SetAssemblyVersion(GitVersion.AssemblySemVer)
              .SetFileVersion(GitVersion.AssemblySemFileVer)
              .SetInformationalVersion(GitVersion.InformationalVersion)
              .SetCopyright($"Copyright {DateTime.Now.Year}")
              .SetNoLogo(true)
            );
        });

    Target Publish => _ => _
        .Requires(() => Configuration.Equals(Configuration.Release))
        //.Requires(() => GitHubAuthenticationToken)
        //.OnlyWhenDynamic(() => GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
        .Produces(PackagesDirectory)
        .Executes(() =>
        {
            var name = Solution.WinMemoryCleaner_Service.Name;
            var publishedPackagePath = OutputDirectory / "publish" / name;
            var package = PackagesDirectory / $"WindowsService_{GitVersion.SemVer}.zip";

            DotNetPublish(s => s
                .SetProject(Solution.WinMemoryCleaner_Service)
                .SetConfiguration(Configuration)
                .EnablePublishSingleFile()
                .EnablePublishTrimmed()
                .EnablePublishReadyToRun()
                .EnableSelfContained()
                
                .SetRuntime("win-x64")
                .SetOutput(publishedPackagePath)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetNoLogo(true)
            );
            DeleteFile(package);
            EnsureExistingDirectory(PackagesDirectory);
            ZipFile.CreateFromDirectory(publishedPackagePath, package);
        });
}
