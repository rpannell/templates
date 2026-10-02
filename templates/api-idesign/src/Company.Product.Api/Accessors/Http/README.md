# HTTP accessors

Accessors that call other APIs go here, one per external service (for example `InventoryApiAccessor`).

- Inject `IHttpClientFactory`, not `HttpClient`. Accessors are Singletons, and a captured `HttpClient` never picks up DNS or handler changes. Call `CreateClient(Name)` per request, and don't dispose the client.
- Register a **named client** in `Accessors/Setup/AccessorsSetup.cs` with a service-discovery base address, such as `https+http://inventory`. Service discovery and the standard resilience handler (retry, circuit breaker, timeouts) are applied to every client there.
- Take and return `Dto` types. The external API's JSON shapes live in `Accessors/Models/` with the `Entity` suffix.
- Wrap transport failures (`HttpRequestException`, Polly rejections, timeouts, bad JSON) in `ResourceAccessException`, which becomes a 503.
