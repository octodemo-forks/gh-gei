using System.Globalization;

namespace Octoshift.Models;

// These models only contains the fields relevant for the current GHAS Migration Tasks.
public class CodeScanningAlert
{
    public int Number { get; set; }
    public string Url { get; set; }
    public string State { get; set; }
    public string FixedAt { get; set; }
    public string DismissedAt { get; set; }
    public string DismissedReason { get; set; }
    public string DismissedComment { get; set; }
    public string DismissedByLogin { get; set; }
    public CodeScanningAlertInstance Instance { get; set; }
}

public class CodeScanningAlertInstance
{
    public string Ref { get; set; }
    public string AnalysisKey { get; set; }
    public string State { get; set; }
    public string CommitSha { get; set; }
    public CodeScanningAlertLocation Location { get; set; }
}

public class CodeScanningAlertLocation
{
    public string Path { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartColumn { get; set; }
    public int EndColumn { get; set; }
}

/*{
"number": 4,
"created_at": "2020-02-13T12:29:18Z",
"url": "https://api.github.com/repos/octocat/hello-world/code-scanning/alerts/4",
"html_url": "https://github.com/octocat/hello-world/code-scanning/4",
"state": "open",
"fixed_at": null,
"dismissed_by": null,
"dismissed_at": null,
"dismissed_reason": null,
"dismissed_comment": null,
"most_recent_instance": {
    "ref": "refs/heads/main",
    "analysis_key": ".github/workflows/codeql-analysis.yml:CodeQL-Build",
    "environment": "{}",
    "state": "open",
    "commit_sha": "39406e42cb832f683daa691dd652a8dc36ee8930",
    "message": {
        "text": "This path depends on a user-provided value."
    },
    "location": {
        "path": "spec-main/api-session-spec.ts",
        "start_line": 917,
        "end_line": 917,
        "start_column": 7,
        "end_column": 18
    },
    "classifications": [
    "test"
        ]
},
"instances_url": "https://api.github.com/repos/octocat/hello-world/code-scanning/alerts/4/instances"
},*/
