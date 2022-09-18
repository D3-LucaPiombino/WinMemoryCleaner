using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Octokit;
using Octokit.Internal;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[GitHubActions(
    "continuous",
    GitHubActionsImage.WindowsLatest,
    //GitHubActionsImage.UbuntuLatest,
    AutoGenerate = true,
    FetchDepth = 0,
    OnPushBranches = new[] { "master", "feature/**", "release/**" },
    OnPullRequestBranches = new[] { "release/**", "feature/**" },
    InvokedTargets = new[] {
        nameof(Compile),
    },
    EnableGitHubToken = true,
    PublishArtifacts = true,
    OnPullRequestTags = new[] { "publish/**" },
    OnPushTags = new[] { "" }
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

    [GitVersion]
    readonly GitVersion GitVersion;

    [GitRepository]
    readonly GitRepository GitRepository;

    static AbsolutePath SourceDirectory => RootDirectory / "src";
    static AbsolutePath OutputDirectory => RootDirectory / "output";
    static AbsolutePath ArtifactsDirectory => OutputDirectory / "artifacts";
    static GitHubActions GitHubActions => GitHubActions.Instance;
    static string PackageContentType => "application/octet-stream";
    static string ChangeLogFile => RootDirectory / "CHANGELOG.md";
    

    Target Clean => _ => _
        .Description($"Clean.")
        .Before(Compile)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("*/bin", "*/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Compile => _ => _
        .Description($"Build artifacts.")
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
        .Description($"Publish artifacts.")
        .Requires(() => Configuration.Equals(Configuration.Release))
        .Triggers(CreateRelease)
        .Executes(() =>
        {
            var name = Solution.WinMemoryCleaner_Service.Name;
            var publishedPackagePath = OutputDirectory / "publish" / name;
            var artifact = ArtifactsDirectory / $"WindowsService_{GitVersion.SemVer}.zip";

            DotNetPublish(s => s
                .SetProject(Solution.WinMemoryCleaner_Service)
                .SetConfiguration(Configuration)
                .SetOutput(publishedPackagePath)
                .EnableNoBuild()
                .EnableNoRestore()
                .SetNoLogo(true)
            );
            EnsureExistingDirectory(ArtifactsDirectory);
            ZipFile.CreateFromDirectory(publishedPackagePath, artifact);
        });

    Target CreateRelease => _ => _
        .Description($"Creating release for publishable artifacts.")
        .DependsOn(Publish)
        .Requires(() => Configuration.Equals(Configuration.Release))
        .OnlyWhenStatic(() => GitHubActions.Token != null)

        //.OnlyWhenStatic(() =>
        //    GitRepository.IsOnMainOrMasterBranch() ||
        //    GitRepository.IsOnReleaseBranch()
        //)
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

            GlobFiles(ArtifactsDirectory, "*.zip")
                //.Where(x => !x.EndsWith(ExcludedArtifactsType))
                .ForEach(async x => await UploadReleaseAssetToGithub(createdRelease, x));

            if(!GitHubActions.IsPullRequest)
            {
                await GitHubTasks
                    .GitHubClient
                    .Repository
                    .Release
                    .Edit(owner, name, createdRelease.Id, new ReleaseUpdate { Draft = false });
            }

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
