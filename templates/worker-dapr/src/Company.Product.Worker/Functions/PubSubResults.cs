namespace Company.Product.Worker.Functions;

/// <summary>
/// Responses Dapr understands from a pub/sub subscriber. Any 2xx without a body means success; a 5xx means retry
/// (with the resiliency policy); <see cref="Drop"/> means "never retry this message" (it goes to the dead-letter
/// topic when one is configured).
/// </summary>
public static class PubSubResults
{
    /// <summary>The message was handled.</summary>
    /// <returns>200 OK.</returns>
    public static IResult Success() => Results.Ok(new { status = "SUCCESS" });

    /// <summary>The message can never succeed (unknown order, invalid content). Don't retry it.</summary>
    /// <returns>200 OK with <c>{"status":"DROP"}</c>.</returns>
    public static IResult Drop() => Results.Ok(new { status = "DROP" });
}
