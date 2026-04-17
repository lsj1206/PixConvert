namespace PixConvert.Models;

public sealed record FileSignatureResult(
    string Format,
    bool IsAnimation,
    bool IsUnsupported);
