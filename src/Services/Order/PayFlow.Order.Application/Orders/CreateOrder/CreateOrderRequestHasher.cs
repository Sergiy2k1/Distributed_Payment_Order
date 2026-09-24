using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PayFlow.Order.Application.Orders.CreateOrder;

public static class CreateOrderRequestHasher
{
    public static string ComputeHash(CreateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Items);

        var canonicalRequest = new StringBuilder();

        canonicalRequest.Append(command.CustomerId.ToString("D"));
        canonicalRequest.Append('|');
        canonicalRequest.Append(
            command.Items.Count.ToString(CultureInfo.InvariantCulture));

        foreach (var item in command.Items)
        {
            ArgumentNullException.ThrowIfNull(item);

            var normalizedSku = NormalizeRequiredText(
                item.Sku,
                nameof(item.Sku));

            var normalizedCurrency = NormalizeRequiredText(
                    item.Currency,
                    nameof(item.Currency))
                .ToUpperInvariant();

            canonicalRequest.Append('|');
            AppendLengthPrefixed(canonicalRequest, normalizedSku);
            canonicalRequest.Append('|');
            canonicalRequest.Append(
                item.Quantity.ToString(CultureInfo.InvariantCulture));
            canonicalRequest.Append('|');
            canonicalRequest.Append(
                item.UnitPrice.ToString(
                    "G29",
                    CultureInfo.InvariantCulture));
            canonicalRequest.Append('|');
            AppendLengthPrefixed(
                canonicalRequest,
                normalizedCurrency);
        }

        var payload = Encoding.UTF8.GetBytes(
            canonicalRequest.ToString());

        return Convert.ToHexString(
            SHA256.HashData(payload));
    }

    private static string NormalizeRequiredText(
        string value,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value,
            parameterName);

        return value.Trim();
    }

    private static void AppendLengthPrefixed(
        StringBuilder builder,
        string value)
    {
        builder.Append(
            value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
    }
}
