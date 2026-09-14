namespace FactoryReport.Client.Configuration;

/// <summary>
/// 移动 PWA 调用的报表 API 基地址（示例不含内网域名或机密）。
/// </summary>
public sealed class ClientApiOptions
{
    public const string SectionName = "FactoryReportClient";

    /// <summary>
    /// 报表 API 根 URL，须以 / 结尾或不含路径后缀。
    /// </summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5161";
}
