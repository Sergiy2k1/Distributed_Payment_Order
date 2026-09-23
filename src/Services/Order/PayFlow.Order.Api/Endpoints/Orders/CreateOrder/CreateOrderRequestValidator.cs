namespace PayFlow.Order.Api.Endpoints.Orders.CreateOrder;

public static class CreateOrderRequestValidator
{
    public static Dictionary<string, string[]> Validate(
        CreateOrderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new Dictionary<string, List<string>>(
            StringComparer.Ordinal);

        if (request.CustomerId == Guid.Empty)
        {
            AddError(
                errors,
                nameof(request.CustomerId),
                "CustomerId must be a non-empty GUID.");
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            AddError(
                errors,
                nameof(request.Items),
                "At least one order item is required.");

            return ToValidationErrors(errors);
        }

        var position = 0;

        foreach (var item in request.Items)
        {
            var prefix = $"{nameof(request.Items)}[{position}]";

            if (item is null)
            {
                AddError(
                    errors,
                    prefix,
                    "Order item is required.");

                position++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Sku))
            {
                AddError(
                    errors,
                    $"{prefix}.{nameof(item.Sku)}",
                    "SKU is required.");
            }
            else if (item.Sku.Trim().Length > 128)
            {
                AddError(
                    errors,
                    $"{prefix}.{nameof(item.Sku)}",
                    "SKU cannot exceed 128 characters.");
            }

            if (item.Quantity <= 0)
            {
                AddError(
                    errors,
                    $"{prefix}.{nameof(item.Quantity)}",
                    "Quantity must be greater than zero.");
            }

            if (item.UnitPrice <= 0m)
            {
                AddError(
                    errors,
                    $"{prefix}.{nameof(item.UnitPrice)}",
                    "UnitPrice must be greater than zero.");
            }

            if (!IsThreeLetterCurrency(item.Currency))
            {
                AddError(
                    errors,
                    $"{prefix}.{nameof(item.Currency)}",
                    "Currency must be a three-letter alphabetic code.");
            }

            position++;
        }

        return ToValidationErrors(errors);
    }

    private static bool IsThreeLetterCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return false;
        }

        var normalized = currency.Trim();

        return normalized.Length == 3
            && normalized.All(
                static character => char.IsAsciiLetter(character));
    }

    private static void AddError(
        IDictionary<string, List<string>> errors,
        string key,
        string message)
    {
        if (!errors.TryGetValue(key, out var messages))
        {
            messages = [];
            errors[key] = messages;
        }

        messages.Add(message);
    }

    private static Dictionary<string, string[]> ToValidationErrors(
        Dictionary<string, List<string>> errors)
    {
        return errors.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
