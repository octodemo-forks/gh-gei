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
            
            var expectedContainer = new SarifContainer
            {
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
        public async Task migrateAnalyses_migrate_multiple_analysis_in_correct_order()
        {
            var Ref = "refs/heads/main";
            var resultExpectedFirst = new CodeScanningAnalysis
            {
                Id = 1,
                Category = "Category",
                CreatedAt = "2022-03-30T00:00:00Z",
                CommitSha = "SHA_1",
                Ref = Ref
            };
            var resultExpectedSecond = new CodeScanningAnalysis
            {
                Id = 2,
                Category = "Category",
                CreatedAt = "2022-03-31T00:00:00Z",
                CommitSha = "SHA_2",
                Ref = Ref,
            };
            
            var resultExpectedThird = new CodeScanningAnalysis
            {
                Id = 3,
                Category = "Category",
                CreatedAt = "2022-04-01T00:00:00Z",
                CommitSha = "SHA_3",
                Ref = Ref,
            };
            
            _mockSourceGithubApi.Setup(x => x.GetDefaultBranch(SOURCE_ORG, SOURCE_REPO).Result).Returns(Ref);
            _mockSourceGithubApi.Setup(x => x.GetCodeScanningAnalysisForRepository(SOURCE_ORG, SOURCE_REPO, Ref).Result).Returns(new [] {resultExpectedThird, resultExpectedSecond, resultExpectedFirst});

            // Question David: Is there a better way to verify the order of the calls?
            var callOrder = 1;
            _mockTargetGithubApi.Setup(x => 
                x.UploadSarifReport( TARGET_ORG, TARGET_REPO, It.Is<SarifContainer>(c => c.CommitSha == "SHA_1")
                )).Callback(() => Assert.Equal(1, callOrder++));
            _mockTargetGithubApi.Setup(x => 
                x.UploadSarifReport( TARGET_ORG, TARGET_REPO, It.Is<SarifContainer>(c => c.CommitSha == "SHA_2")
                )).Callback(() => Assert.Equal(2, callOrder++));
            _mockTargetGithubApi.Setup(x => 
                x.UploadSarifReport( TARGET_ORG, TARGET_REPO, It.Is<SarifContainer>(c => c.CommitSha == "SHA_3")
                )).Callback(() => Assert.Equal(3, callOrder++));
            
            await _service.MigrateAnalyses(SOURCE_ORG, SOURCE_REPO, TARGET_ORG, TARGET_REPO);
            
            _mockTargetGithubApi.VerifyAll();
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
