using System.Linq;
using System.Threading.Tasks;
using Octoshift.Models;
using OctoshiftCLI;

namespace Octoshift
{
    public class CodeScanningService
    {
        private readonly GithubApi _sourceGithubApi;
        private readonly GithubApi _targetGithubApi;
        private readonly OctoLogger _log;

        public CodeScanningService(GithubApi sourceGithubApi, GithubApi targetGithubApi, OctoLogger octoLogger)
        {
            _sourceGithubApi = sourceGithubApi;
            _targetGithubApi = targetGithubApi;
            _log = octoLogger;
        }

        public virtual async Task MigrateAnalyses(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo)
        {
            _log.LogInformation($"Migrating Code Scanning Analyses from '{sourceOrg}/${sourceRepo}' ${targetOrg}/${targetRepo}");
            var analyses = await _sourceGithubApi.GetCodeScanningAnalysisForRepository(sourceOrg, sourceRepo);
            
            analyses = analyses.OrderBy(a => a.CreatedAt).ToList();
            
            foreach (var analysis in analyses)
            {
                var sarifReport = await _sourceGithubApi.GetSarifReport(sourceOrg, sourceRepo, analysis.Id);
                await _targetGithubApi.UploadSarifReport(targetOrg, targetRepo, new SarifContainer
                {
                    sarif = sarifReport,
                    Ref = analysis.Ref,
                    CommitSha = analysis.CommitSha
                });
            }
        }
        
    }

}
