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
using Nuke.Common.ChangeLog;
using Nuke.Common.Tools.GitHub;
using Octokit.Internal;
using Octokit;
using System.IO;
using System.Threading.Tasks;

[GitHubActions(
    "continuous",
    GitHubActionsImage.WindowsLatest,
    //GitHubActionsImage.UbuntuLatest,
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

    [Nuke.Common.Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;
    
    static AbsolutePath SourceDirectory => RootDirectory / "src";
    static AbsolutePath OutputDirectory => RootDirectory / "output";
    static AbsolutePath ArtifactsDirectory => OutputDirectory / "artifacts";
    
    static string PackageContentType => "application/octet-stream";
    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";

    [GitVersion]
    GitVersion GitVersion;

    [GitRepository]
    GitRepository GitRepository;

    static GitHubActions GitHubActions => GitHubActions.Instance;

    Target Clean => _ => _
        .Before(Compile)
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
        //.Produces(ArtifactsDirectory)
        .Triggers(CreateRelease)
        .Executes(() =>
        {
            var name = Solution.WinMemoryCleaner_Service.Name;
            var publishedPackagePath = OutputDirectory / "publish" / name;
            var package = ArtifactsDirectory / $"WindowsService_{GitVersion.SemVer}.zip";

            DotNetPublish(s => s
                .SetProject(Solution.WinMemoryCleaner_Service)
                .SetConfiguration(Configuration)
                //.EnablePublishSingleFile()
                //.EnablePublishTrimmed()
                //.EnablePublishReadyToRun()
                //.EnableSelfContained()
                
                // .SetRuntime("win-x64")
                .SetOutput(publishedPackagePath)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetNoLogo(true)
            );
            DeleteFile(package);
            EnsureExistingDirectory(ArtifactsDirectory);
            ZipFile.CreateFromDirectory(publishedPackagePath, package);
        });

    Target CreateRelease => _ => _
       .Description($"Creating release for the publishable version.")
       .Requires(() => Configuration.Equals(Configuration.Release))
       .Requires(() => GitHubActions.Token)
       // .OnlyWhenStatic(() => GitRepository.IsOnMainOrMasterBranch() || GitRepository.IsOnReleaseBranch())
       .Executes(async () =>
       {
            var credentials = new Credentials(GitHubActions.Token);
            GitHubTasks.GitHubClient = new GitHubClient(new ProductHeaderValue(nameof(NukeBuild)),
               new InMemoryCredentialStore(credentials));

            var (owner, name) = (GitRepository.GetGitHubOwner(), GitRepository.GetGitHubName());

            var releaseTag = GitVersion.NuGetVersionV2;
            var changeLogSectionEntries = ChangelogTasks.ExtractChangelogSectionNotes(ChangeLogFile);
            var latestChangeLog = changeLogSectionEntries
               .Aggregate((c, n) => c + Environment.NewLine + n);

            var newRelease = new NewRelease(releaseTag)
            {
                TargetCommitish = GitVersion.Sha,
                Draft = true,
                Name = $"v{releaseTag}",
                Prerelease = !string.IsNullOrEmpty(GitVersion.PreReleaseTag),
                Body = latestChangeLog
            };

            var createdRelease = await GitHubTasks
                .GitHubClient
                .Repository
                .Release.Create(owner, name, newRelease);

            PathConstruction
                .GlobFiles(ArtifactsDirectory, "*.zip")
                //.Where(x => !x.EndsWith(ExcludedArtifactsType))
                .ForEach(async x => await UploadReleaseAssetToGithub(createdRelease, x));

            await GitHubTasks
                .GitHubClient
                .Repository
                .Release
                .Edit(owner, name, createdRelease.Id, new ReleaseUpdate { Draft = false });

           static async Task UploadReleaseAssetToGithub(Release release, string asset)
           {
               await using var artifactStream = File.OpenRead(asset);
               var fileName = Path.GetFileName(asset);
               var assetUpload = new ReleaseAssetUpload
               {
                   FileName = fileName,
                   ContentType = PackageContentType,
                   RawData = artifactStream,
               };
               await GitHubTasks.GitHubClient.Repository.Release.UploadAsset(release, assetUpload);
           }
       });

   
}
