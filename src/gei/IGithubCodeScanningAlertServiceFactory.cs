using Octoshift;

namespace OctoshiftCLI.GithubEnterpriseImporter
{
    public interface ICodeScanningAlertServiceFactory
    {
        CodeScanningAlertService Create(GithubApi sourceApi, GithubApi targetApi);
    }
}
