using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Octoshift;
using Octoshift.Models;
using Xunit;

namespace OctoshiftCLI.Tests;

public class CodeScanningServiceTest
{
        private readonly Mock<GithubApi> _mockSourceGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<GithubApi> _mockTargetGithubApi = TestHelpers.CreateMock<GithubApi>();
        private readonly Mock<OctoLogger> _mockOctoLogger = TestHelpers.CreateMock<OctoLogger>();
        
        private readonly CodeScanningService _service;

        private const string SOURCE_ORG = "SOURCE-ORG";
        private const string SOURCE_REPO = "SOURCE-REPO";
        private const string TARGET_ORG = "TARGET-ORG";
        private const string TARGET_REPO = "TARGET-REPO";
        
        public CodeScanningServiceTest()
        {
            _service = new CodeScanningService(_mockSourceGithubApi.Object, _mockTargetGithubApi.Object, _mockOctoLogger.Object);
        }
        
        [Fact]
        public async Task migrateAnalyses_migrate_single_analysis()
        {
            var analysisId = 123456;
            var SarifResponse = "MOCK_SARIF_REPORT";
            var CommitSha = "TEST_COMMIT_SHA";
            var Ref = "refs/heads/main";
            var CodeScanningAnalysisResult = new CodeScanningAnalysis
            {
                Id = analysisId,
                Category = "Category",
                CreatedAt = "2022-03-30T00:00:00Z",
                CommitSha = CommitSha,
                Ref = Ref
            };
            _mockSourceGithubApi.Setup(x => x.GetDefaultBranch(SOURCE_ORG, SOURCE_REPO).Result).Returns(Ref);
            _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, Ref).Result).Returns(new [] {CodeScanningAnalysisResult});
            _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysisId).Result).Returns(SarifResponse);
            
            var expectedContainer = new SarifContainer {
                sarif = SarifResponse,
                Ref = Ref,
                CommitSha = CommitSha
            };
            
            await _service.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO);
            
            _mockTargetGithubApi.Verify(
                x => x.UploadSarifReport(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    ItExt.IsDeep(expectedContainer)
                ), 
                Times.Once);
        }
        
        [Fact]
        public async Task migrateAnalyses_migrate_multiple_analysis()
        {
            var Ref = "refs/heads/main";
            var analysis1 = new CodeScanningAnalysis
            {
                Id = 1,
                Category = "Category",
                CreatedAt = "2022-03-30T00:00:00Z",
                CommitSha = "SHA_1",
                Ref = Ref
            };
            var analysis2 = new CodeScanningAnalysis
            {
                Id = 2,
                Category = "Category",
                CreatedAt = "2022-03-31T00:00:00Z",
                CommitSha = "SHA_2",
                Ref = Ref
            };

            const string sarifResponse1 = "SARIF_RESPONSE_1";
            const string sarifResponse2 = "SARIF_RESPONSE_2";
            
            
            _mockSourceGithubApi.Setup(x => x.GetDefaultBranch(SOURCE_ORG, SOURCE_REPO).Result).Returns(Ref);
            _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, Ref).Result).Returns(new [] {analysis1, analysis2});
            _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis1.Id).Result).Returns(sarifResponse1);
            _mockSourceGithubApi.Setup(x => x.GetSarifReport(SOURCE_ORG, SOURCE_REPO, analysis2.Id).Result).Returns(sarifResponse2);

            await _service.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO);
            
            _mockTargetGithubApi.Verify(
                x => x.UploadSarifReport(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<SarifContainer>()
                ), 
                Times.Exactly(2));
            
            _mockTargetGithubApi.Verify(
                x => x.UploadSarifReport(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.Is<SarifContainer>(c => c.CommitSha == analysis1.CommitSha && c.Ref == Ref && c.sarif == sarifResponse1)
                ), 
                Times.Once);
            
            _mockTargetGithubApi.Verify(
                x => x.UploadSarifReport(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.Is<SarifContainer>(c => c.CommitSha == analysis2.CommitSha && c.Ref == Ref && c.sarif == sarifResponse2)
                ), 
                Times.Once);
            
            _mockTargetGithubApi.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task migrateAlerts_matches_dismissed_alert_by_last_instance_and_updates_target()
        {
            var CommitSha = "SHA_1";
            var Ref = "refs/heads/main";
            var lastInstance = new CodeScanningAlertInstance
            {
                Ref = Ref,
                State = "open",
                AnalysisKey = "123456",
                CommitSha = CommitSha,
                Location = new CodeScanningAlertLocation {
                    Path = "path/to/file.cs",
                    StartLine = 3,
                    StartColumn = 4,
                    EndLine = 6,
                    EndColumn = 25
                }
            };

            var sourceAlert = new CodeScanningAlert
            {
                Number = 1,
                RuleId = "java/rule",
                State = "dismissed",
                DismissedAt = "2020-01-01T00:00:00Z",
                DismissedComment = "I was dismissed!",
                DismissedReason = "false positive",
                Instance = lastInstance
            };
            
            var targetAlert = new CodeScanningAlert { Number = 2, State = "open", Instance = lastInstance, RuleId = "java/rule"};
            _mockSourceGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(SOURCE_ORG, SOURCE_REPO, "main").Result).Returns(new [] {sourceAlert});
            _mockTargetGithubApi.Setup(x => x.GetCodeScanningAlertsForRepository(TARGET_ORG, TARGET_REPO, "main").Result).Returns(new [] {targetAlert});
            
            await _service.MigrateAlerts(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO, "main");
            
            _mockTargetGithubApi.Verify(x => x.UpdateCodeScanningAlert(
                TARGET_ORG, 
                TARGET_REPO, 
                2, 
                sourceAlert.State, 
                sourceAlert.DismissedReason, 
                sourceAlert.DismissedComment
            ), Times.Once);
        }
}

// Question David: Is there a better way to deep-assert function parameters? 
public static class ItExt
{
    public static T IsDeep<T>(T expected)
    {
        Func<T, bool> validate = actual =>
        {
            actual.Should().BeEquivalentTo(expected);
            return true;
        };
        return Match.Create<T>(s => validate(s));
    }
}
