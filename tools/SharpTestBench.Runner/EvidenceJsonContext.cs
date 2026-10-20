using System.Text.Json.Serialization;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(InputManifestEvidence))]
[JsonSerializable(typeof(RunEvidence))]
[JsonSerializable(typeof(FailureEvidence))]
[JsonSerializable(typeof(ProcessEvidence))]
internal partial class EvidenceJsonContext : JsonSerializerContext { }
