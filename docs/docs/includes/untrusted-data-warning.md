> [!WARNING]
> Treat data received from Bluesky, AT Protocol services, and streams such as Jetstream and the firehose as untrusted. This includes post and message text, profile and
> list metadata, labels, error details such as `AtErrorDetail`, and raw JSON. Before displaying it, encode it for the output context (for example, HTML-encode it for a web page). Before using
> a URL in a link or other active context, check its scheme against an allow-list of the schemes your application expects, such as `https`; a
> well-formed URL can still use an active scheme such as `javascript:`, which can lead to [cross-site scripting](https://owasp.org/www-community/attacks/xss/).
> Validate other values before using them in active contexts.
>
> The sample code on this page is for illustration. It does not sanitize or otherwise validate the input it uses; apply the validation and encoding
> appropriate for your application.
