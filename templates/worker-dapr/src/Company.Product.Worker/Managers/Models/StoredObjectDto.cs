namespace Company.Product.Worker.Managers.Models;

/// <summary>An object (file) that was created in object storage.</summary>
/// <param name="Container">Bucket or container name from the notification.</param>
/// <param name="Key">Object key or blob name, unescaped (for example <c>imports/orders-001.csv</c>).</param>
/// <param name="Size">Size in bytes, when the notification includes it.</param>
public sealed record StoredObjectDto(string Container, string Key, long? Size);
