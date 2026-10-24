using System.Text;
using System.Xml;
using Costasdev.SafeSvg;
using Crm.Application.Dtos.Common;
using Crm.Application.Interfaces;

namespace Crm.Infrastructure.Services.FileValidators;

/// <summary>
/// Validates and sanitizes SVG (Scalable Vector Graphics) files to prevent security risks such as XSS and malicious scripts.
/// </summary>
public class SvgImageValidator : IFormatValidator
{
    /// <inheritdoc />
    /// <summary>
    /// Determines whether the specified extension corresponds to an SVG format.
    /// </summary>
    /// <param name="extension">The file extension to check (e.g., <c>".svg"</c>).</param>
    /// <returns>
    /// <see langword="true"/> if the extension matches <c>".svg"</c> (case-insensitive); otherwise, <see langword="false"/>.
    /// </returns>
    public bool CanHandle(string extension) => extension.Equals(".svg", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    /// <summary>
    /// Reads, sanitizes, and verifies the SVG stream content.
    /// </summary>
    /// <param name="stream">The raw SVG content stream to validate.</param>
    /// <param name="extension">The file extension associated with the file.</param>
    /// <returns>
    /// A <see cref="FileValidationResult"/> containing <see langword="true"/> and a new sanitized <see cref="MemoryStream"/>
    /// if the content is valid; otherwise, a failed <see cref="FileValidationResult"/>.
    /// </returns>
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

            string sanitizedSvg = SvgSanitiser.Sanitise(svgText);

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

    /// <summary>
    /// Validates that the sanitized content is well-formed XML and has an root &lt;svg&gt; element.
    /// </summary>
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
