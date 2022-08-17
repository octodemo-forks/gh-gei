using System.Threading.Tasks;
using OctoshiftCLI;

namespace Octoshift
{
    public class SecretScanningAlertService
    {
        private readonly GithubApi _sourceGithubApi;
        private readonly GithubApi _targetGithubApi;
        private readonly OctoLogger _log;

        public SecretScanningAlertService(GithubApi sourceGithubApi, GithubApi targetGithubApi, OctoLogger logger)
        {
            _sourceGithubApi = sourceGithubApi;
            _targetGithubApi = targetGithubApi;
            _log = logger;
        }

        public virtual async Task MigrateSecretScanningAlerts(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo)
        {
            _log.LogWarning($"Migrating Secret Scanning Alerts from '{sourceOrg}/${sourceRepo}' ${targetOrg}/${targetRepo}");
        }
    }
}


