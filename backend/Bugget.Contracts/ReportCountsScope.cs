using System.Text.Json.Serialization;

namespace Bugget.Contracts.Reports.Generated;

// Строгость по неизвестным полям — см. ReportCountsBatchRequest.cs.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public partial class ReportCountsScope;
