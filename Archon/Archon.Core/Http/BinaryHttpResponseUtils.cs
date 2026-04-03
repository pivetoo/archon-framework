using System.Net.Http;

namespace Archon.Core.Http
{
    public static class BinaryHttpResponseUtils
    {
        public static bool IsBinaryContentType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return false;
            }

            if (contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (contentType.StartsWith("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        public static bool IsPdfContent(byte[] bytes, string? contentType)
        {
            if (!string.IsNullOrWhiteSpace(contentType) &&
                contentType.StartsWith("application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (bytes.Length < 4)
            {
                return false;
            }

            return bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
        }

        public static string GetResponseFileName(HttpResponseMessage response, string? contentType, bool isPdf)
        {
            string? contentDisposition = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName;

            if (!string.IsNullOrWhiteSpace(contentDisposition))
            {
                return contentDisposition.Trim('"');
            }

            if (isPdf)
            {
                return "file.pdf";
            }

            if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains('/'))
            {
                string[] parts = contentType.Split('/');
                string extension = parts.Length > 1 ? parts[1] : "bin";

                if (string.Equals(extension, "octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    extension = "bin";
                }

                return $"file.{extension}";
            }

            return "file.bin";
        }
    }
}
