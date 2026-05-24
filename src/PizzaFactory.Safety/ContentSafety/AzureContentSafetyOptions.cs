namespace PizzaFactory.Safety.ContentSafety;

/// <summary>Configuration for the Azure AI Content Safety guard. Bound from the "ContentSafety" section.</summary>
public sealed class AzureContentSafetyOptions
{
    public const string SectionName = "ContentSafety";

    /// <summary>Resource endpoint, e.g. https://&lt;your-content-safety&gt;.cognitiveservices.azure.com/. Auth is key-less (AAD).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>Block when any moderation category meets or exceeds this severity (0,2,4,6 in the four-level scale).</summary>
    public int SeverityThreshold { get; set; } = 2;

    public string ApiVersion { get; set; } = "2024-09-01";
}
