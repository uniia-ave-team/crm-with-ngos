using System.Text;
using System.Xml;
using Costasdev.SafeSvg;

namespace Crm.Infrastructure.Services.Storage.FileValidators;

/// <summary>
/// Validates and sanitizes SVG (Scalable Vector Graphics) files to prevent security risks such as XSS and malicious scripts.
/// </summary>
public sealed class SvgImageValidator : IFormatValidator
{
    /// <inheritdoc />
    public bool CanHandle(string extension) => extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public FileValidationResult Validate(Stream stream, string extension)
    {
        try
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
            string svgText = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(svgText))
            {
                return new FileValidationResult(IsValid: false);
            }

            string? sanitizedSvg = SvgSanitiser.Sanitise(svgText);

            if (string.IsNullOrWhiteSpace(sanitizedSvg) || !IsValidXmlStructure(sanitizedSvg))
            {
                return new FileValidationResult(IsValid: false);
            }

            var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(sanitizedSvg));

            return new FileValidationResult(IsValid: true, ResultStream: memoryStream);
        }
        catch
        {
            return new FileValidationResult(IsValid: false);
        }
    }

    private static bool IsValidXmlStructure(string content)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            };

            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader, settings);

            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element)
                {
                    return string.Equals(xmlReader.LocalName, "svg", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
