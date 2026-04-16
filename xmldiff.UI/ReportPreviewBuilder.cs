using System.Net;

internal static class ReportPreviewBuilder
{
    public static string CreateTextPreview(string title, string content)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>
        body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; background: #ffffff; }
        h1 { margin-top: 0; font-size: 1.4rem; }
        pre { white-space: pre-wrap; word-break: break-word; background: #111827; color: #f9fafb; padding: 16px; border-radius: 8px; }
        .note { color: #6b7280; }
        </style>
        </head>
        <body>
        <h1>{{WebUtility.HtmlEncode(title)}}</h1>
        <pre>{{WebUtility.HtmlEncode(content)}}</pre>
        </body>
        </html>
        """;
    }

    public static string CreateEmptyState(string message)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <title>xmldiff UI</title>
        <style>
        body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2937; background: #f9fafb; }
        .card { max-width: 720px; padding: 24px; border: 1px solid #d1d5db; border-radius: 12px; background: #ffffff; }
        h1 { margin-top: 0; font-size: 1.4rem; }
        p { margin-bottom: 0; line-height: 1.5; }
        </style>
        </head>
        <body>
        <div class="card">
        <h1>xmldiff UI</h1>
        <p>{{WebUtility.HtmlEncode(message)}}</p>
        </div>
        </body>
        </html>
        """;
    }
}
