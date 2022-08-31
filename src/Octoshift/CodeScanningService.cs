using System;
using System.Collections.Generic;
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
        private readonly string[] _allowedStates = { "open", "dismissed" };

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
            analyses = analyses.ToList();
            
            var successCount = 0;
            var errorCount = 0;
            
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

        public virtual async Task MigrateAlerts(string sourceOrg, string sourceRepo, string targetOrg,
            string targetRepo, string branch)
        {

            var sourceAlertTask = _sourceGithubApi.GetCodeScanningAlertsForRepository(sourceOrg, sourceRepo, branch);
            var targetAlertTask = _targetGithubApi.GetCodeScanningAlertsForRepository(targetOrg, targetRepo, branch);
            await Task.WhenAll(new List<Task>
                {
                    sourceAlertTask,
                    targetAlertTask
                }
            );

             var sourceAlerts = sourceAlertTask.Result.ToList();
             var targetAlerts = targetAlertTask.Result.ToList();
             
             foreach (var sourceAlert in sourceAlerts)
             {
                 if (!_allowedStates.Contains(sourceAlert.State))
                 {
                     return;
                 }
                 
                 
                 var matchingTargetAlert = targetAlerts.Find(targetAlert => areAlertsEqual(sourceAlert, targetAlert));

                 if (matchingTargetAlert == null)
                 {
                     _log.LogWarning($"Could not find target alert for ${sourceAlert.Number} (${sourceAlert.Url})");
                     return;
                 }
                 
                 // Todo: Add this to a queue to parallelize alert updates
                 await _targetGithubApi.UpdateCodeScanningAlert(
                     targetOrg, 
                     targetRepo, 
                     matchingTargetAlert.Number, 
                     sourceAlert.State, 
                     sourceAlert.DismissedReason, 
                     sourceAlert.DismissedComment
                     );
             }

        }

        private Boolean areAlertsEqual(CodeScanningAlert sourceAlert, CodeScanningAlert targetAlert)
        {
            return sourceAlert.RuleId == targetAlert.RuleId
                   && sourceAlert.Instance.Ref == targetAlert.Instance.Ref
                   && sourceAlert.Instance.CommitSha == targetAlert.Instance.CommitSha
                   && sourceAlert.Instance.Location.Equals(targetAlert.Instance.Location);
        }
    }

}
