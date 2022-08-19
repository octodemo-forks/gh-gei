using System;
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
            _log.LogInformation($"Migrating Code Scanning Analyses from '{sourceOrg}/{sourceRepo}' to '{targetOrg}/{targetRepo}'");
            var analyses = await _sourceGithubApi.GetCodeScanningAnalysisForRepository(sourceOrg, sourceRepo);
            var count = 0;
            var errorCount = 0;
            
            analyses = analyses.OrderBy(a => a.CreatedAt).ToList();
            _log.LogVerbose($"Found {analyses.Count()} analyses to migrate.");
            
            foreach (var analysis in analyses)
            {
                var sarifReport = await _sourceGithubApi.GetSarifReport(sourceOrg, sourceRepo, analysis.Id);
                _log.LogVerbose($"Downloaded SARIF report for analysis {analysis.Id}");
                try
                {
                    await _targetGithubApi.UploadSarifReport(targetOrg, targetRepo,
                        new SarifContainer { sarif = sarifReport, Ref = analysis.Ref, CommitSha = analysis.CommitSha });
                    count++;
                    _log.LogInformation($"Successfully Migrated report for analysis {analysis.Id}");
                }
                catch (Exception exception)
                {
                    _log.LogWarning($"Error while uploading SARIF report for analysis {analysis.Id}: \n {exception.Message}");
                    _log.LogError(exception);
                    errorCount++;
                }
                
                _log.LogInformation($"Code Scanning Analyses done!\nSuccess-Count: {count}\nError-Count: {errorCount}\nOverall: {analyses.Count()}.");
            }
        }
    }

}
