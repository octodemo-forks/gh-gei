using Octoshift;

namespace OctoshiftCLI.GithubEnterpriseImporter;

public sealed class GitHubCodeScanningAlertServiceFactory: ICodeScanningAlertServiceFactory
{
    private readonly OctoLogger _octoLogger;

    public GitHubCodeScanningAlertServiceFactory(OctoLogger octoLogger)
    {
        _octoLogger = octoLogger;
    }
    
    public CodeScanningAlertService Create(GithubApi sourceApi, GithubApi targetApi) => new (sourceApi, targetApi, _octoLogger);
}
