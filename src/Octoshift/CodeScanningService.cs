using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
            
            // As the number of analyses can get massive within pull requests (on created for every CodeQL Action Run),
            // we currently only support migrating analyses from the default branch to prevent hitting API Rate Limits.
            var defaultBranch = await _sourceGithubApi.GetDefaultBranch(sourceOrg, sourceRepo);
            var analyses = await _sourceGithubApi.GetCodeScanningAnalysisForRepository(sourceOrg, sourceRepo, defaultBranch);
            
            var successCount = 0;
            var errorCount = 0;
            
            analyses = analyses
                // TODO: We can avoid manual ordering once https://github.com/github/github/pull/232832 is merged 
                // This will bring the ordering to the API Level - which means we can then also change this to a streaming-based
                // approach so we do not need to store all analyses in memory before acting on them
                .OrderBy(a => a.CreatedAt)
                .ToList();
            
            _log.LogVerbose($"Found {analyses.Count()} analyses to migrate.");
            
            foreach (var analysis in analyses)
            {
                var sarifReport = await _sourceGithubApi.GetSarifReport(sourceOrg, sourceRepo, analysis.Id);
                _log.LogVerbose($"Downloaded SARIF report for analysis {analysis.Id}");
                try
                {
                    await _targetGithubApi.UploadSarifReport(targetOrg, targetRepo,
                        new SarifContainer { sarif = sarifReport, Ref = analysis.Ref, CommitSha = analysis.CommitSha });
                    ++successCount;
                    _log.LogInformation($"Successfully Migrated report for analysis {analysis.Id}");
                }
                catch (HttpRequestException httpException)
                {
                    if (httpException.StatusCode.Equals(HttpStatusCode.NotFound)) 
                    {
                      _log.LogVerbose($"No commit found on target. Skipping Analysis {analysis.Id}");  
                    } 
                    else 
                    {
                        _log.LogWarning($"Http Error {httpException.StatusCode} while migrating analysis {analysis.Id}: ${httpException.Message}");
                    }
                    ++errorCount;
                }
                catch (Exception exception)
                {
                    _log.LogWarning($"Fatal Error while uploading SARIF report for analysis {analysis.Id}: \n {exception.Message}");
                    _log.LogError(exception);
                    // Todo Maybe throw another exception here?
                    throw exception;
                }
                _log.LogInformation($"Handled {successCount + errorCount} / {analyses.Count()} Analyses.");
            }
            
            _log.LogInformation($"Code Scanning Analyses done!\nSuccess-Count: {successCount}\nError-Count: {errorCount}\nOverall: {analyses.Count()}.");
        }
    }

}
