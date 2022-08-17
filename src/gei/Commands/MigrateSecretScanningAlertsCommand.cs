using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octoshift;
using OctoshiftCLI.Extensions;

namespace OctoshiftCLI.GithubEnterpriseImporter.Commands
{
 
    public class MigrateSecretScanningAlertsCommand : Command
    {
        private readonly OctoLogger _log;
        private readonly ISourceGithubApiFactory _sourceGithubApiFactory;
        private readonly ITargetGithubApiFactory _targetGithubApiFactory;
        
        private readonly EnvironmentVariableProvider _environmentVariableProvider;
        private const int ARCHIVE_GENERATION_TIMEOUT_IN_HOURS = 10;
        private const int CHECK_STATUS_DELAY_IN_MILLISECONDS = 10000; // 10 seconds
        private const string DEFAULT_GITHUB_BASE_URL = "https://github.com";

        public MigrateSecretScanningAlertsCommand(OctoLogger log, ISourceGithubApiFactory sourceGithubApiFactory, ITargetGithubApiFactory targetGithubApiFactory, EnvironmentVariableProvider environmentVariableProvider) : base("migrate-secret-alerts")
        {
            _log = log;
            _sourceGithubApiFactory = sourceGithubApiFactory;
            _targetGithubApiFactory = targetGithubApiFactory;
            _environmentVariableProvider = environmentVariableProvider;

            Description = "Invokes the GitHub APIs to migrate repo secret scanning alert data.";

            var githubSourceOrg = new Option<string>("--github-source-org")
            {
                IsRequired = false,
                Description = "Uses GH_SOURCE_PAT env variable or --github-source-pat option. Will fall back to GH_PAT or --github-target-pat if not set."
            };
            var sourceRepo = new Option<string>("--source-repo")
            {
                IsRequired = true
            };
            var githubTargetOrg = new Option<string>("--github-target-org")
            {
                IsRequired = true,
                Description = "Uses GH_PAT env variable or --github-target-pat option."
            };
            var targetRepo = new Option<string>("--target-repo")
            {
                IsRequired = false,
                Description = "Defaults to the name of source-repo"
            };
            var targetApiUrl = new Option<string>("--target-api-url")
            {
                IsRequired = false,
                Description = "The URL of the target API, if not migrating to github.com. Defaults to https://api.github.com"
            };

            // GHES migration path
            var ghesApiUrl = new Option<string>("--ghes-api-url")
            {
                IsRequired = false,
                Description = "Required if migrating from GHES. The API endpoint for your GHES instance. For example: http(s)://ghes.contoso.com/api/v3"
            };
            var noSslVerify = new Option("--no-ssl-verify")
            {
                IsRequired = false,
                Description = "Only effective if migrating from GHES. Disables SSL verification when communicating with your GHES instance. All other migration steps will continue to verify SSL. If your GHES instance has a self-signed SSL certificate then setting this flag will allow data to be extracted."
            };
            var githubSourcePat = new Option<string>("--github-source-pat")
            {
                IsRequired = false
            };
            var githubTargetPat = new Option<string>("--github-target-pat")
            {
                IsRequired = false
            };
            var verbose = new Option("--verbose")
            {
                IsRequired = false
            };

            AddOption(githubSourceOrg);
            AddOption(sourceRepo);
            AddOption(githubTargetOrg);
            AddOption(targetRepo);
            AddOption(targetApiUrl);

            AddOption(ghesApiUrl);
            AddOption(noSslVerify);
            
            AddOption(githubSourcePat);
            AddOption(githubTargetPat);
            AddOption(verbose);

            Handler = CommandHandler.Create<MigrateSecretScanningAlertsCommandArgs>(Invoke);
        }

        public async Task Invoke(MigrateSecretScanningAlertsCommandArgs args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            _log.Verbose = args.Verbose;

            LogAndValidateOptions(args);

            var sourceGitHubApi = _sourceGithubApiFactory.Create(args.GhesApiUrl, args.GithubSourcePat);
            var targetGithubApi = _targetGithubApiFactory.Create(args.TargetApiUrl, args.GithubTargetPat);

            var migrationService = new SecretScanningAlertService(sourceGitHubApi, targetGithubApi, _log);


            await migrationService.MigrateSecretScanningAlerts(
                args.GithubSourceOrg, 
                args.SourceRepo,
                args.GithubTargetOrg,
                args.TargetRepo);
            
            _log.LogSuccess($"Secret Scanning results completed.");
        }

        private string GetSourceToken(MigrateSecretScanningAlertsCommandArgs args) =>
            args.GithubSourcePat ?? _environmentVariableProvider.SourceGithubPersonalAccessToken();

        private void LogAndValidateOptions(MigrateSecretScanningAlertsCommandArgs args)
        {
            _log.LogInformation("Migrating Repo Secret Scanning Alerts...");
            if (!string.IsNullOrWhiteSpace(args.GithubSourceOrg))
            {
                _log.LogInformation($"GITHUB SOURCE ORG: {args.GithubSourceOrg}");
            }
            _log.LogInformation($"SOURCE REPO: {args.SourceRepo}");
            _log.LogInformation($"GITHUB TARGET ORG: {args.GithubTargetOrg}");
            _log.LogInformation($"TARGET REPO: {args.TargetRepo}");

            if (!string.IsNullOrWhiteSpace(args.TargetApiUrl))
            {
                _log.LogInformation($"TARGET API URL: {args.TargetApiUrl}");
            }

            if (args.GithubSourcePat is not null)
            {
                _log.LogInformation("GITHUB SOURCE PAT: ***");
            }

            if (args.GithubTargetPat is not null)
            {
                _log.LogInformation("GITHUB TARGET PAT: ***");

                if (args.GithubSourcePat is null)
                {
                    args.GithubSourcePat = args.GithubTargetPat;
                    _log.LogInformation("Since github-target-pat is provided, github-source-pat will also use its value.");
                }
            }

            if (string.IsNullOrWhiteSpace(args.TargetRepo))
            {
                _log.LogInformation($"Target repo name not provided, defaulting to same as source repo ({args.SourceRepo})");
                args.TargetRepo = args.SourceRepo;
            }

            if (args.GhesApiUrl.HasValue())
            {
                _log.LogInformation($"GHES API URL: {args.GhesApiUrl}");
            }

            if (args.NoSslVerify)
            {
                _log.LogInformation("SSL verification disabled");
            }
        }
    }

    public class MigrateSecretScanningAlertsCommandArgs
    {
        public string GithubSourceOrg { get; set; }
        public string SourceRepo { get; set; }
        public string GithubTargetOrg { get; set; }
        public string TargetRepo { get; set; }
        public string TargetApiUrl { get; set; }
        public string GhesApiUrl { get; set; }
        public bool NoSslVerify { get; set; }
        public bool Verbose { get; set; }
        public string GithubSourcePat { get; set; }
        public string GithubTargetPat { get; set; }
    }
}
