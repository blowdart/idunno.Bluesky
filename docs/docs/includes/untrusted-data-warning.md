> [!WARNING]
> Treat data received from Bluesky, AT Protocol services, and streams such as Jetstream as untrusted. This includes post and message text, profile and
> list metadata, labels, and raw JSON. Before displaying it, encode it for the output context (for example, HTML-encode it for a web page), and validate
> URLs and other values before using them in links or other active contexts.
>
> The sample code on this page is for illustration. It does not sanitize or otherwise validate the input it uses; apply the validation and encoding
> appropriate for your application.
